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
using Moq;
using NUnit.Framework;
using FluentAssertions;
using Neo4j.Driver.Bolt.PackStream;
using Neo4j.Driver.Bolt.PackStream.Abstractions;
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;
using Neo4j.Driver.Bolt.PackStream.Implementations;
using Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;
using Neo4j.Driver.Bolt.Transport.Abstractions;

namespace Neo4j.Driver.Bolt.Tests.PackStream;

internal class PackStreamDecoderTests : UnitTestBase<PackStreamDecoder>
{
    [Test]
    public async Task DecodesSingleValue()
    {
        byte[] packStreamMessage = [0x01, 0x69, 0xEF];
        var byteReader = new Mock<IByteReader>();

        var dummyDecoder = new MockDecoder(
            [0x01],
            [0x01],
            PackStreamValue.Integer(-123));
        
        AutoMocker.GetMock<IValueDecoderProvider>()
            .Setup(x => x.GetDecoder(It.IsAny<byte>(), It.IsAny<IPackStreamDecoder>()))
            .Returns(dummyDecoder);

        ReadOnlySequence<byte>[] messages = [new(packStreamMessage)];
        var chunkAssembler = AutoMocker.GetMock<IChunkAssembler>();
        chunkAssembler
            .Setup(x => x.ReadMessagesAsync(byteReader.Object, CancellationToken.None))
            .Returns(messages.ToAsyncEnumerable());

        var result = await Subject.Decode(byteReader.Object, 1).ToListAsync();
        result.Should().HaveCount(1);
        result.First().Should().Be(PackStreamValue.Integer(-123));
    }

    [Test]
    public async Task DecodesMultipleValues()
    {
        Dictionary<byte[], PackStreamValue> packStreamMessages = new()
        {
            // not real packstream messages
            [[0x01,0x02, 0x03]] = PackStreamValue.Integer(12345),
            [[0x32, 0xFF, 0xFF, 0xFF]] = PackStreamValue.Integer(123456789),
            [[0xFF, 0x00]] = PackStreamValue.Float(123.456)
        };

        foreach (var (bytes, packStreamValue) in packStreamMessages)
        {
            AutoMocker.GetMock<IValueDecoderProvider>()
                .Setup(x => x.GetDecoder(bytes[0], It.IsAny<IPackStreamDecoder>()))
                .Returns(new MockDecoder([bytes[0]], bytes, packStreamValue));
        }

        var messages = packStreamMessages.Select(kvp => new ReadOnlySequence<byte>(kvp.Key)).ToArray();
        var chunkAssembler = AutoMocker.GetMock<IChunkAssembler>();
        var byteReader = new Mock<IByteReader>();
        chunkAssembler
            .Setup(x => x.ReadMessagesAsync(byteReader.Object, CancellationToken.None))
            .Returns(messages.ToAsyncEnumerable());

        var result = await Subject.Decode(byteReader.Object, 3).ToListAsync();
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(packStreamMessages.Values);
    }

    private class MockDecoder : IValueDecoder
    {
        private readonly PackStreamValue _decodeResult;
        private readonly int _messageLength;

        public MockDecoder(byte[] validMarkerBytes, IReadOnlyCollection<byte> message, PackStreamValue decodeResult)
        {
            HandledMarkerBytes = validMarkerBytes;
            _decodeResult = decodeResult;
            _messageLength = message.Count;
        }

        public byte[] HandledMarkerBytes { get; }

        public ValueDecoderResult Decode(ReadOnlySequence<byte> buffer)
        {
            HandledMarkerBytes.Should().Contain(buffer.First.Span[0]);
            return new ValueDecoderResult(_decodeResult, _messageLength);
        }
    }
}
