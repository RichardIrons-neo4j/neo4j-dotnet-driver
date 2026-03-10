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
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Neo4j.Driver.Bolt.PackStream;
using Neo4j.Driver.Bolt.PackStream.Abstractions;
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;
using Neo4j.Driver.Bolt.PackStream.Implementations;
using Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;
using Neo4j.Driver.Bolt.Transport.Abstractions;
using NUnit.Framework;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Neo4j.Driver.Bolt.Tests.PackStream.Integration;

[TestFixture]
internal class PackStreamDecoderIntegrationTests
{
    private AutoMocker _autoMocker = new();
    private IPackStreamDecoder Subject => _autoMocker.CreateInstance<PackStreamDecoder>();

    [SetUp]
    public void SetUp()
    {
        _autoMocker = new AutoMocker();
        var logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Debug()
            .CreateLogger();

        var frameworkLogger = new SerilogLoggerProvider(logger).CreateLogger("Neo4j.Driver.Bolt.Tests");
        _autoMocker.Use(frameworkLogger);

        var decoders = new IValueDecoder[]
        {
            _autoMocker.CreateInstance<NullDecoder>(),
            _autoMocker.CreateInstance<BooleanDecoder>(),
            _autoMocker.CreateInstance<TinyIntDecoder>(),
            _autoMocker.CreateInstance<IntegerDecoder>(),
            _autoMocker.CreateInstance<FloatDecoder>(),
            _autoMocker.CreateInstance<StringDecoder>(),
            _autoMocker.CreateInstance<BytesDecoder>(),
            _autoMocker.CreateInstance<ListDecoder>(),
            _autoMocker.CreateInstance<MapDecoder>(),
            _autoMocker.CreateInstance<StructDecoder>(),
        };

        _autoMocker.Use(decoders);
        var provider = new ValueDecoderProvider(decoders, frameworkLogger);
        _autoMocker.Use<IValueDecoderProvider>(provider);
        _autoMocker.Use<IChunkAssembler>(Mock.Of<IChunkAssembler>());
    }

    [Test]
    public void Decodes_nested_list_and_map_mixed_types()
    {
        // PackStream encodes a single value: a list.
        // The list length is given in the low nibble of the marker (2 items).
        byte[] data =
        [
            // List marker: TinyList, 2 items
            0x92,
            // First item: a tiny int, value 42
            0x2A,
            // Second item: a map; the entry count is in the low nibble (2 entries)
            0xA2,
            // First entry: key is a string; the length is in the low nibble (1 byte)
            0x81, 0x61,
            // Value: a tiny int, 1
            0x01,
            // Second entry: key is a string of length 1, 'b'
            0x81, 0x62,
            // Value: a list; length in low nibble (2 items)
            0x92,
            // Two tiny ints: 2, 3
            0x02, 0x03
        ];

        var buffer = new ReadOnlySequence<byte>(data);
        var result = Subject.Decode(buffer);

        result.BytesConsumed.Should().Be(data.Length);
        result.Value.Type.Should().Be(PackStreamType.List);
        var list = result.Value.ListValue;
        list.Count.Should().Be(2);

        var items = list.ToEnumerable().ToList();
        items[0].IntValue.Should().Be(42);
        items[1].Type.Should().Be(PackStreamType.Map);
        var entries = items[1].MapValue.ToEnumerable().ToList();
        entries[0].Key.StringValue.ToString().Should().Be("a");
        entries[0].Value.IntValue.Should().Be(1);
        entries[1].Key.StringValue.ToString().Should().Be("b");
        entries[1].Value.ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([2, 3]);
    }

    [Test]
    public void Decodes_list_containing_nulls_and_mixed_types()
    {
        // A list of four items: null, a tiny int, a string, and a boolean true.
        byte[] data =
        [
            // List marker: TinyList, 4 items
            0x94,
            // First item: null (single marker byte)
            0xC0,
            // Second item: tiny int 7
            0x07,
            // Third item: a string; length in low nibble (5 bytes) then UTF-8 "hello"
            0x85, 0x68, 0x65, 0x6C, 0x6C, 0x6F,
            // Fourth item: boolean true
            0xC3
        ];

        var buffer = new ReadOnlySequence<byte>(data);
        var result = Subject.Decode(buffer);

        result.BytesConsumed.Should().Be(data.Length);
        result.Value.ListValue.Count.Should().Be(4);

        var items = result.Value.ListValue.ToEnumerable().ToList();
        items[0].IsNull.Should().BeTrue();
        items[1].IntValue.Should().Be(7);
        items[2].StringValue.ToString().Should().Be("hello");
        items[3].BooleanValue.Should().BeTrue();
    }

    [Test]
    public void Decodes_empty_and_nested_containers()
    {
        // A list of three items: an empty list, an empty map, and a list whose only element is an empty map.
        byte[] data =
        [
            // List marker: TinyList, 3 items
            0x93,
            // First item: a list; length in low nibble (0 items)
            0x90,
            // Second item: a map; entry count in low nibble (0 entries)
            0xA0,
            // Third item: a list of 1 item
            0x91,
            // That item is a map with 0 entries
            0xA0
        ];

        var buffer = new ReadOnlySequence<byte>(data);
        var result = Subject.Decode(buffer);

        result.BytesConsumed.Should().Be(data.Length);
        var list = result.Value.ListValue.ToEnumerable().ToList();
        list[0].ListValue.Count.Should().Be(0);
        list[1].MapValue.Count.Should().Be(0);
        list[2].ListValue.Count.Should().Be(1);
        list[2].ListValue.ToEnumerable().First().MapValue.Count.Should().Be(0);
    }

    [Test]
    public void Decodes_struct_with_list_and_map_fields()
    {
        // A structure; the field count is in the low nibble (2 fields). The tag byte follows. Then two fields: a list and a map.
        byte[] data =
        [
            // Struct marker: TinyStruct, 2 fields
            0xB2,
            // Tag byte: 0x4E
            0x4E,
            // First field: a list of 2 tiny ints (1, 2)
            0x92, 0x01, 0x02,
            // Second field: a map with one entry "x" => 3
            0xA1, 0x81, 0x78, 0x03
        ];

        var buffer = new ReadOnlySequence<byte>(data);
        var result = Subject.Decode(buffer);

        result.BytesConsumed.Should().Be(data.Length);
        result.Value.Type.Should().Be(PackStreamType.Struct);
        var st = result.Value.StructValue;
        st.Tag.Should().Be(0x4E);
        st.Fields.Count.Should().Be(2);

        var fields = st.Fields.ToEnumerable().ToList();
        fields[0].ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([1, 2]);
        var mapEntries = fields[1].MapValue.ToEnumerable().ToList();
        mapEntries[0].Key.StringValue.ToString().Should().Be("x");
        mapEntries[0].Value.IntValue.Should().Be(3);
    }

    [Test]
    public void Decodes_map_with_null_values()
    {
        // A map with two entries: "a" => null, "b" => 1.
        byte[] data =
        [
            // Map marker: TinyMap, 2 entries
            0xA2,
            // First entry: key "a", value null
            0x81, 0x61, 0xC0,
            // Second entry: key "b", value tiny int 1
            0x81, 0x62, 0x01
        ];

        var buffer = new ReadOnlySequence<byte>(data);
        var result = Subject.Decode(buffer);

        result.BytesConsumed.Should().Be(data.Length);
        var entries = result.Value.MapValue.ToEnumerable().ToList();
        entries[0].Key.StringValue.ToString().Should().Be("a");
        entries[0].Value.IsNull.Should().BeTrue();
        entries[1].Key.StringValue.ToString().Should().Be("b");
        entries[1].Value.IntValue.Should().Be(1);
    }

    [Test]
    public void Decodes_deeply_nested_list_map_struct()
    {
        // A list of one element: a map with one entry "s" => a struct (tag 0x01, one field: a list of 1).
        byte[] data =
        [
            // List marker: TinyList, 1 item
            0x91,
            // That item: a map; 1 entry
            0xA1,
            // Key: string "s"
            0x81, 0x73,
            // Value: a structure; 1 field, tag 0x01
            0xB1, 0x01,
            // Single field: a list of one tiny int 99
            0x91, 0x63
        ];

        var buffer = new ReadOnlySequence<byte>(data);
        var result = Subject.Decode(buffer);

        result.BytesConsumed.Should().Be(data.Length);
        var outerList = result.Value.ListValue.ToEnumerable().ToList();
        var map = outerList[0].MapValue.ToEnumerable().ToList();
        map[0].Key.StringValue.ToString().Should().Be("s");
        var innerStruct = map[0].Value.StructValue;
        innerStruct.Tag.Should().Be(0x01);
        innerStruct.Fields.Count.Should().Be(1);
        innerStruct.Fields.ToEnumerable().First().ListValue.ToEnumerable().First().IntValue.Should().Be(99);
    }

    [Test]
    public void Decodes_float_and_non_tiny_integer()
    {
        // A list of three items: a 64-bit float (2.5), an Int8 (e.g. -50), an Int16 (1000).
        // Float64: marker 0xC1 then 8 bytes big-endian IEEE 754. 2.5 = 0x4004000000000000.
        // Int8: marker 0xC8 then one signed byte. -50 = 0xCE.
        // Int16: marker 0xC9 then two bytes big-endian. 1000 = 0x03E8.
        byte[] data =
        [
            // List marker: TinyList, 3 items
            0x93,
            // First item: Float64 marker then 8 bytes (2.5)
            0xC1, 0x40, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            // Second item: Int8 marker then one byte (-50)
            0xC8, 0xCE,
            // Third item: Int16 marker then two bytes big-endian (1000)
            0xC9, 0x03, 0xE8
        ];

        var buffer = new ReadOnlySequence<byte>(data);
        var result = Subject.Decode(buffer);

        result.BytesConsumed.Should().Be(data.Length);
        var items = result.Value.ListValue.ToEnumerable().ToList();
        items[0].FloatValue.Should().BeApproximately(2.5, 0.0001);
        items[1].IntValue.Should().Be(-50);
        items[2].IntValue.Should().Be(1000);
    }

    [Test]
    public void Decodes_string_and_bytes()
    {
        // A list of two items: a string "AB" (TinyString length 2), and a Bytes value (Bytes8: length given as one byte, then raw bytes).
        byte[] data =
        [
            // List marker: TinyList, 2 items
            0x92,
            // First item: string; length in low nibble (2), then UTF-8 "AB"
            0x82, 0x41, 0x42,
            // Second item: Bytes8; marker 0xCC, then one-byte length (3), then three bytes
            0xCC, 0x03, 0x01, 0x02, 0x03
        ];

        var buffer = new ReadOnlySequence<byte>(data);
        var result = Subject.Decode(buffer);

        result.BytesConsumed.Should().Be(data.Length);
        var items = result.Value.ListValue.ToEnumerable().ToList();
        items[0].StringValue.ToString().Should().Be("AB");
        items[1].Type.Should().Be(PackStreamType.Bytes);
        var bytes = items[1].BytesValue.ToArray();
        bytes.Should().BeEquivalentTo([0x01, 0x02, 0x03]);
    }

    [Test]
    public void Decodes_boolean_false_and_negative_tiny_int()
    {
        // A list: boolean false, then a negative tiny int (-1). Negative tiny ints use markers 0xF0..0xFF for -16..-1.
        byte[] data =
        [
            // List marker: TinyList, 2 items
            0x92,
            // Boolean false (single marker)
            0xC2,
            // Negative tiny int -1 (marker 0xFF)
            0xFF
        ];

        var buffer = new ReadOnlySequence<byte>(data);
        var result = Subject.Decode(buffer);

        result.BytesConsumed.Should().Be(data.Length);
        var items = result.Value.ListValue.ToEnumerable().ToList();
        items[0].BooleanValue.Should().BeFalse();
        items[1].IntValue.Should().Be(-1);
    }

    [Test]
    public void Decodes_list8_sized_form()
    {
        // List8: the length of the list is given as a single byte after the marker (0xD4). Here we use 2 items to stay small.
        byte[] data =
        [
            // List8 marker, then one-byte length (2)
            0xD4, 0x02,
            // Two tiny ints
            0x01, 0x02
        ];

        var buffer = new ReadOnlySequence<byte>(data);
        var result = Subject.Decode(buffer);

        result.BytesConsumed.Should().Be(4);
        result.Value.ListValue.Count.Should().Be(2);
        result.Value.ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([1, 2]);
    }

    [Test]
    public void Decodes_map8_sized_form()
    {
        // Map8: the entry count is given as a single byte after the marker (0xD8). One entry "k" => 1.
        byte[] data =
        [
            // Map8 marker, then one-byte entry count (1)
            0xD8, 0x01,
            // Key: string "k", value: tiny int 1
            0x81, 0x6B, 0x01
        ];

        var buffer = new ReadOnlySequence<byte>(data);
        var result = Subject.Decode(buffer);

        result.BytesConsumed.Should().Be(5);
        var entries = result.Value.MapValue.ToEnumerable().ToList();
        entries[0].Key.StringValue.ToString().Should().Be("k");
        entries[0].Value.IntValue.Should().Be(1);
    }

    [Test]
    public void Decodes_struct8_sized_form()
    {
        // Struct8: the field count is given as a single byte after the marker (0xDC). Tag byte then one field (tiny int 10).
        byte[] data =
        [
            // Struct8 marker, then one-byte field count (1)
            0xDC, 0x01,
            // Tag byte
            0x00,
            // One field: tiny int 10
            0x0A
        ];

        var buffer = new ReadOnlySequence<byte>(data);
        var result = Subject.Decode(buffer);

        result.BytesConsumed.Should().Be(4);
        result.Value.StructValue.Tag.Should().Be(0x00);
        result.Value.StructValue.Fields.Count.Should().Be(1);
        result.Value.StructValue.Fields.ToEnumerable().First().IntValue.Should().Be(10);
    }

    [Test]
    public void Decodes_string8_sized_form()
    {
        // String8: the length of the string is given as a single byte after the marker (0xD0). Empty string.
        byte[] data =
        [
            // String8 marker, then one-byte length (0)
            0xD0, 0x00
        ];

        var buffer = new ReadOnlySequence<byte>(data);
        var result = Subject.Decode(buffer);

        result.BytesConsumed.Should().Be(2);
        result.Value.StringValue.ToString().Should().Be("");
    }
}
