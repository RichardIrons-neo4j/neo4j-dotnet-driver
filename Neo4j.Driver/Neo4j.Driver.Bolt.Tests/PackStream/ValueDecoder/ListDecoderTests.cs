// Copyright (c) "Neo4j"
// Neo4j Sweden AB [https://neo4j.com]
// 
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Buffers;
using System.Text;
using FluentAssertions;
using Moq;
using Neo4j.Driver.Bolt.PackStream;
using Neo4j.Driver.Bolt.PackStream.Abstractions;
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;
using Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;
using Neo4j.Driver.Bolt.Tests.TestHelpers;
using Neo4j.Driver.Bolt.Transport.Abstractions;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests.PackStream.ValueDecoder;

[TestFixture]
internal class ListDecoderTests : UnitTestBase<ListDecoder>
{
    [SetUp]
    public void SetUp()
    {
        AutoMocker.Use<IPackStreamSizeReader>(new PackStreamSizeReader());
        Subject.SetRecursionDecoder(new MockPackStreamDecoder(Subject));
    }
    
    [Test]
    public void HandlesAllListMarkerBytes()
    {
        var validBytes = new ByteArrayBuilder()
            .Range(0x90, 0x10)
            .ExactBytes([PackStreamMarker.List8, PackStreamMarker.List16, PackStreamMarker.List32]);

        Subject.HandledMarkerBytes.Should().BeEquivalentTo(validBytes);
    }

    [Test]
    public void DecodesTinyListEmpty()
    {
        var buffer = new ReadOnlySequence<byte>([0x90]); // TinyList with 0 items

        var result = Subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(0);
        result.BytesConsumed.Should().Be(1);
    }

    [Test]
    public void DecodesList8Empty()
    {
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.List8, 0x00]);

        var result = Subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(0);
        result.BytesConsumed.Should().Be(2);
    }

    [Test]
    public void DecodesTinyListWithOneItem()
    {
        // TinyList with 1 item: [1]
        var buffer = new ReadOnlySequence<byte>([0x91, 0x01]);

        var result = Subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(1);
        result.BytesConsumed.Should().Be(2);
        result.Value.ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([1]);
    }

    [Test]
    public void DecodesTinyListWithMultipleItems()
    {
        // TinyList with 3 items: [1, 2, 3]
        var buffer = new ReadOnlySequence<byte>([0x93, 0x01, 0x02, 0x03]);

        var result = Subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(3);
        result.BytesConsumed.Should().Be(4);

        var items = new List<long>();
        foreach (var item in result.Value.ListValue)
        {
            items.Add(item.IntValue);
        }

        items.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Test]
    public void DecodesTinyListMaxSize()
    {
        // TinyList with 15 items (max for TinyList)
        byte[] payload = [1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1, 2, 3, 4, 5];
        var bytes = new ByteArrayBuilder()
            .ExactBytes([0x9F]) // TinyList marker for 15 items
            .ExactBytes(payload)
            .Bytes;

        var buffer = new ReadOnlySequence<byte>(bytes);

        var result = Subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(15);
        result.BytesConsumed.Should().Be(16);
        result.Value.ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo(payload);
    }

    [Test]
    public void DecodesList8()
    {
        // List8 with 2 items: [5, 6]
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.List8, 0x02, 0x05, 0x03]);

        var result = Subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(2);
        result.BytesConsumed.Should().Be(4);
        result.Value.ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([5, 3]);
    }

    [Test]
    public void DecodesList16()
    {
        // List16 with 2 items: [7, 8]
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.List16, 0x00, 0x02, 0x01, 0x02]);

        var result = Subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(2);
        result.BytesConsumed.Should().Be(5);
        result.Value.ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([1, 2]);
    }

    [Test]
    public void DecodesList32()
    {
        // List32 with 2 items: [9, 10]
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.List32, 0x00, 0x00, 0x00, 0x02, 0x01, 0x02]);

        var result = Subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(2);
        result.BytesConsumed.Should().Be(7);
        result.Value.ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([1, 2]);
    }

    [Test]
    public void DecodesListWithStringItems()
    {
        // TinyList with 2 items: ["Hello", "World"]
        // Using mock bytes 0x20 and 0x21 which the mock decoder maps to "Hello" and "World"
        var buffer = new ReadOnlySequence<byte>([0x92, 0x20, 0x21]);

        var result = Subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(2);
        result.BytesConsumed.Should().Be(3);
        result.Value.ListValue.ToEnumerable()
            .Select(v => v.StringValue.ToString())
            .Should()
            .BeEquivalentTo(["Hello", "World"]);
    }

    [Test]
    public void ThrowsOnEmptyBuffer()
    {
        Action act = () => Subject.Decode(ReadOnlySequence<byte>.Empty);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ThrowsOnUnknownMarker()
    {
        // 0xC0 is Null marker, not a list marker
        Action act = () => Subject.Decode(new ReadOnlySequence<byte>([0xC0]));

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ThrowsOnList8WithInsufficientHeaderBytes()
    {
        // List8 marker alone without the size byte
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.List8]);

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ThrowsOnList16WithInsufficientHeaderBytes()
    {
        // List16 marker with only 1 size byte instead of 2
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.List16, 0x00]);

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ThrowsOnList32WithInsufficientHeaderBytes()
    {
        // List32 marker with only 3 size bytes instead of 4
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.List32, 0x00, 0x00, 0x00]);

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ThrowsOnTinyListWithInsufficientData()
    {
        // TinyList claims 3 items but only has 2 bytes of data
        var buffer = new ReadOnlySequence<byte>([0x93, 0x01, 0x02]);

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ThrowsOnList8WithInsufficientData()
    {
        // List8 with count of 5 but only 3 items
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.List8, 0x05, 0x01, 0x02, 0x03]);

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ThrowsOnNestedListWithInsufficientData()
    {
        // Outer list claims 2 items, first is a list [1, 2], second claims to be a list but data is truncated
        // [0x92, 0x92, 0x01, 0x02, 0x92] - second nested list has no items
        var buffer = new ReadOnlySequence<byte>([0x92, 0x92, 0x01, 0x02, 0x91]);

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ToEnumerableSupportsLinq()
    {
        var buffer = new ReadOnlySequence<byte>([0x95, 0x01, 0x02, 0x03, 0x04, 0x05]);

        var result = Subject.Decode(buffer);

        var sum = result.Value.ListValue.ToEnumerable().Sum(v => v.IntValue);

        sum.Should().Be(15);
    }

    private class MockPackStreamDecoder : IPackStreamDecoder
    {
        private readonly IRecursiveValueDecoder _listDecoder;

        public MockPackStreamDecoder(IRecursiveValueDecoder listDecoder)
        {
            _listDecoder = listDecoder;
            _listDecoder.SetRecursionDecoder(this);
        }

        public IAsyncEnumerable<PackStreamValue> Decode(IByteReader byteReader, int valueCount) =>
            throw new NotImplementedException();

        private static ReadOnlySequence<byte> GetUtfBytes(string str) => new(Encoding.UTF8.GetBytes(str));

        public ValueDecoderResult Decode(ReadOnlySequence<byte> buffer)
        {
            var array = buffer.ToArray();
            return array[0] switch
            {
                0x01 => new(PackStreamValue.Int(1), 1),
                0x02 => new(PackStreamValue.Int(2), 1),
                0x03 => new(PackStreamValue.Int(3), 1),
                0x04 => new(PackStreamValue.Int(4), 1),
                0x05 => new(PackStreamValue.Int(5), 1),
                0x11 => new(PackStreamValue.Float(0.1f), 1),
                0x12 => new(PackStreamValue.Float(0.2f), 1),
                0x13 => new(PackStreamValue.Float(0.3f), 1),
                0x14 => new(PackStreamValue.Float(0.4f), 1),
                0x15 => new(PackStreamValue.Float(0.5f), 1),
                0x20 => new(PackStreamValue.String(GetUtfBytes("Hello")), 1),
                0x21 => new(PackStreamValue.String(GetUtfBytes("World")), 1),
                >= 0x90 and <= 0x9F
                    or PackStreamMarker.List8
                    or PackStreamMarker.List16
                    or PackStreamMarker.List32 => _listDecoder.Decode(buffer),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(buffer),
                    $"No mock value defined for bytes: {BitConverter.ToString(array)}")
            };
        }
    }
}
