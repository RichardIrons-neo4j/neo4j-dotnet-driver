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
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests.PackStream;

[TestFixture]
internal class PackStreamSizeReaderTests : UnitTestBase<PackStreamSizeReader>
{
    [Test]
    public void ReadSize8_ReturnsCorrectHeaderSizeAndCount()
    {
        var buffer = new ReadOnlySequence<byte>([0x00, 0x2A]); // marker + count 42

        var result = Subject.ReadSize8(buffer);

        result.HeaderSize.Should().Be(2);
        result.Count.Should().Be(42);
    }

    [Test]
    public void ReadSize8_ReadsMaxValue()
    {
        var buffer = new ReadOnlySequence<byte>([0x00, 0xFF]); // marker + count 255

        var result = Subject.ReadSize8(buffer);

        result.HeaderSize.Should().Be(2);
        result.Count.Should().Be(255);
    }

    [Test]
    public void ReadSize8_ThrowsOnBufferTooShort()
    {
        var buffer = new ReadOnlySequence<byte>([0x00]); // only marker

        Action act = () => Subject.ReadSize8(buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ReadSize16_ReturnsCorrectHeaderSizeAndCount()
    {
        var buffer = new ReadOnlySequence<byte>([0x00, 0x01, 0x00]); // marker + count 256 (big-endian)

        var result = Subject.ReadSize16(buffer);

        result.HeaderSize.Should().Be(3);
        result.Count.Should().Be(256);
    }

    [Test]
    public void ReadSize16_ReadsMaxValue()
    {
        var buffer = new ReadOnlySequence<byte>([0x00, 0xFF, 0xFF]); // marker + count 65535

        var result = Subject.ReadSize16(buffer);

        result.HeaderSize.Should().Be(3);
        result.Count.Should().Be(65535);
    }

    [Test]
    public void ReadSize16_ThrowsOnBufferTooShort()
    {
        var buffer = new ReadOnlySequence<byte>([0x00, 0x01]); // only 2 bytes

        Action act = () => Subject.ReadSize16(buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ReadSize32_ReturnsCorrectHeaderSizeAndCount()
    {
        var buffer = new ReadOnlySequence<byte>([0x00, 0x00, 0x01, 0x00, 0x00]); // marker + count 65536 (big-endian)

        var result = Subject.ReadSize32(buffer);

        result.HeaderSize.Should().Be(5);
        result.Count.Should().Be(65536);
    }

    [Test]
    public void ReadSize32_ReadsLargeValue()
    {
        var buffer = new ReadOnlySequence<byte>([0x00, 0x7F, 0xFF, 0xFF, 0xFF]); // marker + count 2147483647

        var result = Subject.ReadSize32(buffer);

        result.HeaderSize.Should().Be(5);
        result.Count.Should().Be(int.MaxValue);
    }

    [Test]
    public void ReadSize32_ThrowsOnBufferTooShort()
    {
        var buffer = new ReadOnlySequence<byte>([0x00, 0x01, 0x02, 0x03]); // only 4 bytes

        Action act = () => Subject.ReadSize32(buffer);

        act.Should().Throw<InvalidOperationException>();
    }
}

