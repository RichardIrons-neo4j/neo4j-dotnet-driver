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
using System.IO.Pipelines;
using NUnit.Framework;
using FluentAssertions;
using Moq.AutoMock;
using Neo4j.Driver.Bolt.PackStream;
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;
using Neo4j.Driver.Bolt.PackStream.Implementations;
using Neo4j.Driver.Bolt.Transport.Abstractions;

namespace Neo4j.Driver.Bolt.Tests.PackStream;

public class PackStreamDecoderTests
{
    [Test]
    public async Task DecodesSingleValue()
    {
        byte[] packStreamMessage = [0x01, 0x69, 0xEF];
        var pipe = new Pipe();

        var automocker = new AutoMocker();
        var dummyDecoder = new MockDecoder(
            [0x01],
            packStreamMessage,
            PackStreamValue.Int8(-123));

        IValueDecoder[] decoders = [dummyDecoder];
        automocker.Use(decoders);

        ReadOnlySequence<byte>[] messages = [new(packStreamMessage)];
        var chunkAssembler = automocker.GetMock<IChunkAssembler>();
        chunkAssembler
            .Setup(x => x.ReadMessagesAsync(pipe.Reader, CancellationToken.None))
            .Returns(messages.ToAsyncEnumerable());

        var packStreamDecoder = automocker.CreateInstance<PackStreamDecoder>();

        var result = await packStreamDecoder.Decode(pipe.Reader, 1).ToListAsync();
        result.Should().HaveCount(1);
        result.First().Should().Be(PackStreamValue.Int8(-123));
    }

    private IValueDecoder[] CreateMockDecoders()
    {
        return
        [
            
        ];
    }
}

internal class MockDecoder : IValueDecoder
{
    private readonly byte[] _expectedBytes;
    private readonly PackStreamValue _decodeResult;

    public MockDecoder(byte[] validMarkerBytes, byte[] expectedBytes, PackStreamValue decodeResult)
    {
        HandledMarkerBytes = validMarkerBytes;
        _expectedBytes = expectedBytes;
        _decodeResult = decodeResult;
    }

    public byte[] HandledMarkerBytes { get; }

    public ValueDecoderResult Decode(ReadOnlySequence<byte> buffer)
    {
        var actualBytes = buffer.ToArray();
        actualBytes.Should().BeEquivalentTo(_expectedBytes, options => options.WithStrictOrdering());
        return new ValueDecoderResult(_decodeResult, _expectedBytes.Length);
    }
}
