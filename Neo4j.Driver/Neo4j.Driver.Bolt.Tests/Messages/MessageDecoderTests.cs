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
using Neo4j.Driver.Bolt.Messages;
using Neo4j.Driver.Bolt.Messages.Decoding;
using Neo4j.Driver.Bolt.PackStream.Abstractions;
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;
using Neo4j.Driver.Bolt.PackStream.Ephemeral;
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
