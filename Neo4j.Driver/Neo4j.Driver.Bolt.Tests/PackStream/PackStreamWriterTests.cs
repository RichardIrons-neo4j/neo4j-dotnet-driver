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
using Neo4j.Driver.Bolt.PackStream.Implementations;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests.PackStream;

[TestFixture]
internal class PackStreamWriterTests
{
    private static byte[] Encode(Action<PackStreamWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        write(new PackStreamWriter(buffer));
        return buffer.WrittenSpan.ToArray();
    }

    [Test]
    public void WriteNullMatchesSpec()
    {
        Encode(w => w.WriteNull()).Should().Equal(0xC0);
    }

    [Test]
    public void WriteBooleanMatchesSpec()
    {
        Encode(w => w.WriteBoolean(true)).Should().Equal(0xC3);
        Encode(w => w.WriteBoolean(false)).Should().Equal(0xC2);
    }

    [Test]
    public void WriteIntegerTinyMatchesSpec()
    {
        Encode(w => w.WriteInteger(0)).Should().Equal(0x00);
        Encode(w => w.WriteInteger(42)).Should().Equal(0x2A);
        Encode(w => w.WriteInteger(-1)).Should().Equal(0xFF);
    }

    [Test]
    public void WriteIntegerInt8MatchesSpec()
    {
        Encode(w => w.WriteInteger(-17)).Should().Equal(0xC8, 0xEF);
    }

    [Test]
    public void WriteIntegerInt16MatchesSpec()
    {
        Encode(w => w.WriteInteger(200)).Should().Equal(0xC9, 0x00, 0xC8);
    }

    [Test]
    public void WriteIntegerInt32MatchesSpec()
    {
        Encode(w => w.WriteInteger(40000)).Should().Equal(0xCA, 0x00, 0x00, 0x9C, 0x40);
    }

    [Test]
    public void WriteIntegerInt64MatchesSpec()
    {
        Encode(w => w.WriteInteger(10_000_000_000L))
            .Should()
            .Equal(0xCB, 0x00, 0x00, 0x00, 0x02, 0x54, 0x0B, 0xE4, 0x00);
    }

    [Test]
    public void WriteFloat64MatchesSpec()
    {
        Encode(w => w.WriteFloat64(0.0)).Should().Equal(0xC1, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00);
    }

    [Test]
    public void WriteStringTinyMatchesSpec()
    {
        Encode(w => w.WriteString("hi")).Should().Equal(0x82, (byte)'h', (byte)'i');
    }

    [Test]
    public void WriteUtf8StringMatchesSpec()
    {
        Encode(w => w.WriteUtf8String("ab"u8)).Should().Equal(0x82, (byte)'a', (byte)'b');
    }

    [Test]
    public void WriteBytesTinyMatchesSpec()
    {
        Encode(w => w.WriteBytes([0xAA, 0xBB])).Should().Equal(0xCC, 0x02, 0xAA, 0xBB);
    }

    [Test]
    public void WriteStructHeaderTinyMatchesSpec()
    {
        Encode(w => w.WriteStructHeader(0x10, 1)).Should().Equal(0xB1, 0x10);
    }

    [Test]
    public void WriteStructHeaderStruct8MatchesSpec()
    {
        Encode(w => w.WriteStructHeader(0x2A, 20)).Should().Equal(0xDC, 0x14, 0x2A);
    }

    [Test]
    public void WriteListTinyMatchesSpec()
    {
        Encode(w => w.WriteList([1, 2, 3])).Should().Equal(0x93, 0x01, 0x02, 0x03);
    }

    [Test]
    public void WriteMapTinyMatchesSpec()
    {
        var map = new Dictionary<string, object?> { ["n"] = 1 };
        Encode(w => w.WriteMap(map)).Should().Equal(0xA1, 0x81, (byte)'n', 0x01);
    }

    [Test]
    public void WriteObjectNestedMatchesSpec()
    {
        var map = new Dictionary<string, object?> { ["a"] = new List<object?> { 1, 2 } };
        Encode(w => w.WriteMap(map)).Should().Equal(0xA1, 0x81, (byte)'a', 0x92, 0x01, 0x02);
    }

    [Test]
    public void WriteStructHeaderTooManyFieldsThrowsProtocolException()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new PackStreamWriter(buffer);
        var act = () => writer.WriteStructHeader(0x01, short.MaxValue + 1);
        act.Should().Throw<Neo4j.Driver.ProtocolException>();
    }

    [Test]
    public void WriteStringNullThrowsArgumentNullException()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new PackStreamWriter(buffer);
        var act = () => writer.WriteString(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
