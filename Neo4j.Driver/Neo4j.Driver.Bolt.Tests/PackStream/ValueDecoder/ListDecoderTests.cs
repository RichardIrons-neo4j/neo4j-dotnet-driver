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
using FluentAssertions;
using Neo4j.Driver.Bolt.PackStream;
using Neo4j.Driver.Bolt.PackStream.Abstractions;
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;
using Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;
using Neo4j.Driver.Bolt.Tests.TestHelpers;
using Neo4j.Driver.Bolt.Transport.Abstractions;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests.PackStream.ValueDecoder;

[TestFixture]
internal class ListDecoderTests
{
    private ListDecoder _subject = null!;
    private TestPackStreamDecoder _decoder = null!;

    [SetUp]
    public void SetUp()
    {
        _decoder = new TestPackStreamDecoder();
        _subject = new ListDecoder(_decoder, new PackStreamSizeReader());
    }

    [Test]
    public void HandlesAllListMarkerBytes()
    {
        var validBytes = new ByteArrayBuilder()
            .Range(0x90..0xA0)
            .ExactBytes([PackStreamMarker.List8, PackStreamMarker.List16, PackStreamMarker.List32]);

        _subject.HandledMarkerBytes.Should().BeEquivalentTo(validBytes);
    }

    #region Empty Lists

    [Test]
    public void DecodesTinyListEmpty()
    {
        var buffer = new ReadOnlySequence<byte>([0x90]); // TinyList with 0 items

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(0);
        result.BytesConsumed.Should().Be(1);
    }

    [Test]
    public void DecodesList8Empty()
    {
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.List8, 0x00]);

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(0);
        result.BytesConsumed.Should().Be(2);
    }

    #endregion

    #region TinyList (0x90-0x9F)

    [Test]
    public void DecodesTinyListWithOneItem()
    {
        // TinyList with 1 item: [1]
        var buffer = new ReadOnlySequence<byte>([0x91, 0x01]);

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(1);
        result.BytesConsumed.Should().Be(2);
        result.Value.ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([1]);
    }

    [Test]
    public void DecodesTinyListWithMultipleItems()
    {
        // TinyList with 3 items: [1, 2, 3]
        var buffer = new ReadOnlySequence<byte>([0x93, 0x01, 0x02, 0x03]);

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(3);
        result.BytesConsumed.Should().Be(4);

        var items = new List<long>();
        foreach (var item in result.Value.ListValue)
            items.Add(item.IntValue);

        items.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Test]
    public void DecodesTinyListMaxSize()
    {
        // TinyList with 15 items (max for TinyList)
        var bytes = new ByteArrayBuilder()
            .ExactBytes([0x9F]) // TinyList marker for 15 items
            .Range(0x01, 15)    // Items 1-15
            .Bytes;

        var buffer = new ReadOnlySequence<byte>(bytes);

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(15);
        result.BytesConsumed.Should().Be(16);
        result.Value.ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo(Enumerable.Range(1, 15));
    }

    #endregion

    #region List8, List16, List32

    [Test]
    public void DecodesList8()
    {
        // List8 with 2 items: [5, 6]
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.List8, 0x02, 0x05, 0x06]);

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(2);
        result.BytesConsumed.Should().Be(4);
        result.Value.ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([5, 6]);
    }

    [Test]
    public void DecodesList16()
    {
        // List16 with 2 items: [7, 8]
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.List16, 0x00, 0x02, 0x07, 0x08]);

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(2);
        result.BytesConsumed.Should().Be(5);
        result.Value.ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([7, 8]);
    }

    [Test]
    public void DecodesList32()
    {
        // List32 with 2 items: [9, 10]
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.List32, 0x00, 0x00, 0x00, 0x02, 0x09, 0x0A]);

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(2);
        result.BytesConsumed.Should().Be(7);
        result.Value.ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([9, 10]);
    }

    #endregion

    #region Nested Lists (2 levels)

    [Test]
    public void DecodesNestedListTwoLevels()
    {
        // [[1, 2], [3, 4]]
        // 0x92 = TinyList(2)
        // 0x92, 0x01, 0x02 = TinyList(2) containing 1, 2
        // 0x92, 0x03, 0x04 = TinyList(2) containing 3, 4
        var buffer = new ReadOnlySequence<byte>([0x92, 0x92, 0x01, 0x02, 0x92, 0x03, 0x04]);

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(2);
        result.BytesConsumed.Should().Be(7);

        var outerList = result.Value.ListValue.ToEnumerable().ToArray();
        outerList[0].ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([1, 2]);
        outerList[1].ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([3, 4]);
    }

    [Test]
    public void DecodesNestedEmptyLists()
    {
        // [[], []]
        var buffer = new ReadOnlySequence<byte>([0x92, 0x90, 0x90]);

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(2);
        result.BytesConsumed.Should().Be(3);

        var outerList = result.Value.ListValue.ToEnumerable().ToArray();
        outerList[0].ListValue.Count.Should().Be(0);
        outerList[1].ListValue.Count.Should().Be(0);
    }

    #endregion

    #region Nested Lists (3+ levels)

    [Test]
    public void DecodesNestedListThreeLevels()
    {
        // [[[1]]]
        // 0x91 = TinyList(1)
        var buffer = new ReadOnlySequence<byte>([0x91, 0x91, 0x91, 0x01]);

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(1);
        result.BytesConsumed.Should().Be(4);

        var level1 = result.Value.ListValue.ToEnumerable().First();
        var level2 = level1.ListValue.ToEnumerable().First();
        var level3 = level2.ListValue.ToEnumerable().First();

        level3.IntValue.Should().Be(1);
    }

    [Test]
    public void DecodesNestedListFourLevels()
    {
        // [
        //   [
        //     [
        //       [1, 2],
        //       [3]
        //     ],
        //     [
        //       [4]
        //     ]
        //   ]
        // ]
        var buffer = new ReadOnlySequence<byte>([
            0x91,                   // Level 0: list of 1
            0x92,                   // Level 1: list of 2
            0x92,                   // Level 2a: list of 2
            0x92, 0x01, 0x02,       // Level 3a: [1, 2]
            0x91, 0x03,             // Level 3b: [3]
            0x91,                   // Level 2b: list of 1
            0x91, 0x04              // Level 3c: [4]
        ]);

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(1);
        result.BytesConsumed.Should().Be(11);

        var level1 = result.Value.ListValue.ToEnumerable().First().ListValue.ToEnumerable().ToArray();
        level1.Should().HaveCount(2);
        level1

        // First item at level 1: [[1, 2], [3]]
        var level2a = level1[0].ListValue.ToEnumerable().ToArray();
        level2a.Should().HaveCount(2);
        level2a[0].ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([1, 2]);
        level2a[1].ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([3]);

        // Second item at level 1: [[4]]
        var level2b = level1[1].ListValue.ToEnumerable().ToArray();
        level2b.Should().HaveCount(1);
        level2b[0].ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([4]);
    }

    #endregion

    #region Heterogeneous Nested Lists

    [Test]
    public void DecodesHeterogeneousNestedList()
    {
        // [1, [2, 3], 4]
        // Mix of integers and nested lists
        var buffer = new ReadOnlySequence<byte>([
            0x93,                   // TinyList(3)
            0x01,                   // Integer 1
            0x92, 0x02, 0x03,       // TinyList(2): [2, 3]
            0x04                    // Integer 4
        ]);

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(3);
        result.BytesConsumed.Should().Be(6);

        var items = result.Value.ListValue.ToEnumerable().ToArray();
        items[0].IntValue.Should().Be(1);
        items[1].ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([2, 3]);
        items[2].IntValue.Should().Be(4);
    }

    [Test]
    public void DecodesHeterogeneousNestedListThreeLevels()
    {
        // [1, [2, [3, 4]], 5]
        var buffer = new ReadOnlySequence<byte>([
            0x93,                           // TinyList(3)
            0x01,                           // Integer 1
            0x92,                           // TinyList(2)
            0x02,                           // Integer 2
            0x92, 0x03, 0x04,               // TinyList(2): [3, 4]
            0x05                            // Integer 5
        ]);

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(3);
        result.BytesConsumed.Should().Be(8);

        var level0 = result.Value.ListValue.ToEnumerable().ToArray();
        level0[0].IntValue.Should().Be(1);
        level0[2].IntValue.Should().Be(5);

        var level1 = level0[1].ListValue.ToEnumerable().ToArray();
        level1[0].IntValue.Should().Be(2);
        level1[1].ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([3, 4]);
    }

    [Test]
    public void DecodesComplexHeterogeneousStructure()
    {
        // [[1, []], [2, [3, [4]]]]
        var buffer = new ReadOnlySequence<byte>([
            0x92,                           // TinyList(2)
            0x92, 0x01, 0x90,               // [1, []]
            0x92,                           // TinyList(2)
            0x02,                           // Integer 2
            0x92, 0x03, 0x91, 0x04          // [3, [4]]
        ]);

        var result = _subject.Decode(buffer);

        result.Value.ListValue.Count.Should().Be(2);
        result.BytesConsumed.Should().Be(10);

        var outerList = result.Value.ListValue.ToEnumerable().ToArray();

        // First item: [1, []]
        var first = outerList[0].ListValue.ToEnumerable().ToArray();
        first[0].IntValue.Should().Be(1);
        first[1].ListValue.Count.Should().Be(0);

        // Second item: [2, [3, [4]]]
        var second = outerList[1].ListValue.ToEnumerable().ToArray();
        second[0].IntValue.Should().Be(2);

        var secondInner = second[1].ListValue.ToEnumerable().ToArray();
        secondInner[0].IntValue.Should().Be(3);
        secondInner[1].ListValue.ToEnumerable().First().IntValue.Should().Be(4);
    }

    #endregion

    #region Error Cases

    [Test]
    public void ThrowsOnEmptyBuffer()
    {
        Action act = () => _subject.Decode(ReadOnlySequence<byte>.Empty);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ThrowsOnUnknownMarker()
    {
        // 0xC0 is Null marker, not a list marker
        Action act = () => _subject.Decode(new ReadOnlySequence<byte>([0xC0]));

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region LINQ Support

    [Test]
    public void ToEnumerableSupportsLinq()
    {
        var buffer = new ReadOnlySequence<byte>([0x95, 0x01, 0x02, 0x03, 0x04, 0x05]);

        var result = _subject.Decode(buffer);

        var sum = result.Value.ListValue.ToEnumerable().Sum(v => v.IntValue);

        sum.Should().Be(15);
    }

    #endregion

    /// <summary>
    /// Test implementation of IPackStreamDecoder that handles TinyInt and TinyList.
    /// </summary>
    private class TestPackStreamDecoder : IPackStreamDecoder
    {
        private ListDecoder? _listDecoder;

        public TestPackStreamDecoder()
        {
            _listDecoder = new ListDecoder(this, new PackStreamSizeReader());
        }

        public ValueDecoderResult Decode(ReadOnlySequence<byte> buffer)
        {
            var marker = buffer.FirstSpan[0];
            return marker switch
            {
                // TinyInt positive (0x00-0x7F)
                >= 0x00 and <= 0x7F => new(PackStreamValue.Int(marker), 1),
                // TinyInt negative (0xF0-0xFF)
                >= 0xF0 and <= 0xFF => new(PackStreamValue.Int((sbyte)marker), 1),
                // TinyList (0x90-0x9F)
                >= 0x90 and <= 0x9F => _listDecoder!.Decode(buffer),
                _ => throw new InvalidOperationException($"Unsupported marker: 0x{marker:X2}")
            };
        }

        public IAsyncEnumerable<PackStreamValue> Decode(IByteReader byteReader, int valueCount)
        {
            throw new NotImplementedException();
        }
    }
}


