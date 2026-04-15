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
using Neo4j.Driver.Bolt.Messages.Abstractions;
using Neo4j.Driver.Bolt.Messages.Implementations.Decoding;
using Neo4j.Driver.Bolt.PackStream;
using Neo4j.Driver.Bolt.PackStream.Abstractions;
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;
using Neo4j.Driver.Bolt.PackStream.Ephemeral;
using Neo4j.Driver.Bolt.PackStream.Implementations;
using Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoding;
using Neo4j.Driver.Bolt.Transport.Abstractions;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests.Messages;

[TestFixture]
internal class MessageDecoderTests
{
    private static ILogger Logger => Mock.Of<ILogger>();

    [Test]
    public void SuccessMessageDecoder_HandledTag_matches_MessageKind_Success()
    {
        var decoder = new SuccessMessageDecoder(Logger);
        decoder.HandledTag.Should().Be((byte)MessageKind.Success).And.Be(0x70);
    }

    [Test]
    public void RecordMessageDecoder_HandledTag_matches_MessageKind_Record()
    {
        var decoder = new RecordMessageDecoder(Logger);
        decoder.HandledTag.Should().Be((byte)MessageKind.Record).And.Be(0x71);
    }

    [Test]
    public void FailureMessageDecoder_HandledTag_matches_MessageKind_Failure()
    {
        var decoder = new FailureMessageDecoder(Logger);
        decoder.HandledTag.Should().Be((byte)MessageKind.Failure).And.Be(0x7F);
    }

    [Test]
    public void IgnoredMessageDecoder_HandledTag_matches_MessageKind_Ignored()
    {
        var decoder = new IgnoredMessageDecoder(Logger);
        decoder.HandledTag.Should().Be((byte)MessageKind.Ignored).And.Be(0x7E);
    }

    [Test]
    public void MessageDecoderProvider_decodes_Success_struct_into_BoltMessage()
    {
        var structView = CreateStructView(0x70, 1, new StubPackStreamDecoder()); // SUCCESS, 1 field (metadata)
        var provider = CreateProvider();

        var message = provider.Decode(structView);

        message.Kind.Should().Be(MessageKind.Success);
        message.AsSuccess(); // view is constructible
    }

    [Test]
    public void MessageDecoderProvider_decodes_Record_struct_into_BoltMessage()
    {
        var structView = CreateStructView(0x71, 1, new StubPackStreamDecoder()); // RECORD, 1 field (list)
        var provider = CreateProvider();

        var message = provider.Decode(structView);

        message.Kind.Should().Be(MessageKind.Record);
        message.AsRecord(); // view is constructible
    }

    [Test]
    public void MessageDecoderProvider_decodes_Failure_struct_into_BoltMessage()
    {
        var structView = CreateStructView(0x7F, 1, new StubPackStreamDecoder()); // FAILURE, 1 field (map)
        var provider = CreateProvider();

        var message = provider.Decode(structView);

        message.Kind.Should().Be(MessageKind.Failure);
        message.AsFailure(); // view is constructible
    }

    [Test]
    public void MessageDecoderProvider_decodes_Ignored_struct_into_BoltMessage()
    {
        var structView = CreateStructView(0x7E, 0, new StubPackStreamDecoder()); // IGNORED, 0 fields
        var provider = CreateProvider();

        var message = provider.Decode(structView);

        message.Kind.Should().Be(MessageKind.Ignored);
        message.AsIgnored();
    }

    [Test]
    public void MessageDecoderProvider_throws_for_unknown_tag()
    {
        var structView = CreateStructView(0x99, 0, new StubPackStreamDecoder());
        var provider = CreateProvider();

        var act = () => provider.Decode(structView);

        act.Should().Throw<KeyNotFoundException>();
    }

    [Test]
    public void Success_message_view_exposes_metadata_from_struct_field()
    {
        // PackStream: SUCCESS struct (tag 0x70) with one field = map {"server" -> "Neo4j/5.0"}
        // Tiny struct 1 field: 0xB1, tag 0x70, then map 1 entry
        // Tiny map 1: 0xA1, key "server" (6 chars): 0x86 + UTF-8, value "Neo4j/5.0" (9 chars): 0x89 + UTF-8
        byte[] successStructBytes =
        [
            0xB1, 0x70, // struct 1 field, tag SUCCESS
            0xA1, // map 1 entry
            0x86, 0x73, 0x65, 0x72, 0x76, 0x65, 0x72, // "server" (6 bytes)
            0x89, 0x4E, 0x65, 0x6F, 0x34, 0x6A, 0x2F, 0x35, 0x2E, 0x30, // "Neo4j/5.0" (9 bytes)
        ];

        var packStreamDecoder = CreatePackStreamDecoder();
        var result = packStreamDecoder.Decode(new ReadOnlySequence<byte>(successStructBytes));
        result.Value.Type.Should().Be(PackStreamType.Struct);

        var structView = result.Value.StructValue;
        var message = CreateProvider().Decode(structView);
        message.Kind.Should().Be(MessageKind.Success);

        var success = message.AsSuccess();
        success.Metadata.Count.Should().Be(1);
        var metadata = success.Metadata.ToEnumerable().ToList();
        metadata.Should().HaveCount(1);
        metadata[0].Key.Type.Should().Be(PackStreamType.String);
        metadata[0].Key.StringValue.ToString().Should().Be("server");
        metadata[0].Value.Type.Should().Be(PackStreamType.String);
        metadata[0].Value.StringValue.ToString().Should().Be("Neo4j/5.0");

        var dict = success.Metadata.ToEnumerable().ToDictionary(kv => kv.Key.StringValue.ToString(), kv => kv.Value);
        dict.Should().ContainKey("server").WhoseValue.StringValue.ToString().Should().Be("Neo4j/5.0");
    }

    [Test]
    public void Record_message_view_exposes_fields_list_from_struct_field()
    {
        // PackStream: RECORD struct (tag 0x71) with one field = list [42, "hello"]
        // Tiny struct 1 field: 0xB1, tag 0x71, then list 2 items: 0x92, 0x2A, 0x85 0x68 0x65 0x6C 0x6F
        byte[] recordStructBytes =
        [
            0xB1, 0x71, // struct 1 field, tag RECORD
            0x92, // list 2 items
            0x2A, // 42
            0x85, 0x68, 0x65, 0x6C, 0x6C, 0x6F, // "hello"
        ];

        var packStreamDecoder = CreatePackStreamDecoder();
        var result = packStreamDecoder.Decode(new ReadOnlySequence<byte>(recordStructBytes));
        result.Value.Type.Should().Be(PackStreamType.Struct);

        var structView = result.Value.StructValue;
        var message = CreateProvider().Decode(structView);
        message.Kind.Should().Be(MessageKind.Record);

        var record = message.AsRecord();
        record.Fields.Count.Should().Be(2);
        var fields = record.Fields.ToEnumerable().ToList();
        fields.Should().HaveCount(2);
        fields[0].Type.Should().Be(PackStreamType.Integer);
        fields[0].IntValue.Should().Be(42);
        fields[1].Type.Should().Be(PackStreamType.String);
        fields[1].StringValue.ToString().Should().Be("hello");
    }

    [Test]
    public void Failure_message_view_exposes_metadata_from_struct_field()
    {
        // PackStream: FAILURE struct (tag 0x7F) with one field = map {"code" -> "X", "message" -> "Y"}
        // Tiny struct 1 field: 0xB1, tag 0x7F, then map 2 entries
        byte[] failureStructBytes =
        [
            0xB1, 0x7F, // struct 1 field, tag FAILURE
            0xA2, // map 2 entries
            0x84, 0x63, 0x6F, 0x64, 0x65, 0x81, 0x58, // "code" -> "X"
            0x87, 0x6D, 0x65, 0x73, 0x73, 0x61, 0x67, 0x65, 0x81, 0x59, // "message" -> "Y"
        ];

        var packStreamDecoder = CreatePackStreamDecoder();
        var result = packStreamDecoder.Decode(new ReadOnlySequence<byte>(failureStructBytes));
        result.Value.Type.Should().Be(PackStreamType.Struct);

        var structView = result.Value.StructValue;
        var message = CreateProvider().Decode(structView);
        message.Kind.Should().Be(MessageKind.Failure);

        var failure = message.AsFailure();
        failure.Metadata.Count.Should().Be(2);
        var metadata = failure.Metadata.ToEnumerable()
            .Select(kv => (Key: kv.Key.StringValue.ToString(), Value: kv.Value.StringValue.ToString()))
            .ToDictionary(x => x.Key, x => x.Value);
        metadata.Should().Contain("code", "X");
        metadata.Should().Contain("message", "Y");
    }

    private static IPackStreamDecoder CreatePackStreamDecoder()
    {
        var decoders = new IValueDecoder[]
        {
            new NullDecoder(Logger),
            new BooleanDecoder(Logger),
            new TinyIntDecoder(Logger),
            new IntegerDecoder(Logger),
            new FloatDecoder(Logger),
            new StringDecoder(Logger),
            new BytesDecoder(Logger),
            new ListDecoder(Logger),
            new MapDecoder(Logger),
            new StructDecoder(Logger),
        };
        var provider = new ValueDecoderProvider(decoders, Logger);
        return new PackStreamDecoder(decoders, Mock.Of<IChunkAssembler>(), provider, Logger);
    }

    private static MessageDecoderProvider CreateProvider()
    {
        return new MessageDecoderProvider(
        [
            new SuccessMessageDecoder(Logger),
            new RecordMessageDecoder(Logger),
            new FailureMessageDecoder(Logger),
            new IgnoredMessageDecoder(Logger),
        ]);
    }

    private static PackStreamStructView CreateStructView(byte tag, int fieldCount, IPackStreamDecoder decoder)
    {
        var emptyList = new PackStreamListView(ReadOnlySequence<byte>.Empty, fieldCount, decoder);
        return new PackStreamStructView(tag, emptyList);
    }

    private sealed class StubPackStreamDecoder : IPackStreamDecoder
    {
        public ValueDecoderResult Decode(ReadOnlySequence<byte> buffer) =>
            new(PackStreamValueView.Null(), buffer.IsEmpty ? 0 : 1);

        public IAsyncEnumerable<PackStreamValueView> Decode(IByteReader byteReader, int valueCount) =>
            throw new NotImplementedException();
    }
}
