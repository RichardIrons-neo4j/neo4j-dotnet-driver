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
using System.Buffers.Binary;
using FluentAssertions;
using Neo4j.Driver.Bolt.Handshake;
using Neo4j.Driver.Bolt.Transport.Abstractions;
using Neo4j.Driver.Bolt.Transport.Types;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests.Handshake;

[TestFixture]
internal sealed class BoltHandshakeTests
{
    [Test]
    public void DefaultClientOffers_HasExpectedLengthAndMagic()
    {
        var offers = BoltHandshakeClientOffers.Default;
        offers.Length.Should().Be(20);
        BinaryPrimitives.ReadInt32BigEndian(offers.Span).Should().Be(BoltHandshakeClientOffers.GoGoBolt);
    }

    [Test]
    public async Task NegotiateAsync_LegacyServerResponse_ReturnsVersion()
    {
        // Server agrees 5.8 — packed int layout (minor<<8)|major on the wire as big-endian int32.
        var serverWord = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(serverWord, (8 << 8) | 5);

        var writer = new RecordingByteWriter();
        var reader = new ScriptedReadExactlyReader(serverWord);
        var subject = new BoltHandshake();

        var version = await subject.NegotiateAsync(writer, reader);

        version.Major.Should().Be(5);
        version.Minor.Should().Be(8);
        writer.Written.Should().BeEquivalentTo(BoltHandshakeClientOffers.Default.ToArray());
    }

    [Test]
    public void NegotiateAsync_ManifestMarker_ThrowsNotImplementedException()
    {
        var serverWord = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(serverWord, (1 << 8) | BoltHandshakeVersion.ManifestSchemaMajor);

        var writer = new RecordingByteWriter();
        var reader = new ScriptedReadExactlyReader(serverWord);
        var subject = new BoltHandshake();

        var act = async () => await subject.NegotiateAsync(writer, reader);
        act.Should().ThrowAsync<NotImplementedException>().WithMessage("*Manifest-style*");
    }

    [Test]
    public void NegotiateAsync_NoAgreement_ThrowsProtocolException()
    {
        var serverWord = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(serverWord, 0);

        var writer = new RecordingByteWriter();
        var reader = new ScriptedReadExactlyReader(serverWord);
        var subject = new BoltHandshake();

        var act = async () => await subject.NegotiateAsync(writer, reader);
        act.Should().ThrowAsync<ProtocolException>().WithMessage("*does not support*");
    }

    [Test]
    public void NegotiateAsync_HttpResponse_ThrowsNotSupportedException()
    {
        var serverWord = new byte[] { 0x48, 0x54, 0x54, 0x50 }; // "HTTP"

        var writer = new RecordingByteWriter();
        var reader = new ScriptedReadExactlyReader(serverWord);
        var subject = new BoltHandshake();

        var act = async () => await subject.NegotiateAsync(writer, reader);
        act.Should().ThrowAsync<NotSupportedException>().WithMessage("*http endpoint*");
    }

    private sealed class RecordingByteWriter : IByteWriter
    {
        public List<byte> Written { get; } = [];

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Written.AddRange(buffer.ToArray());
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Satisfies handshake tests without simulating a full PipeReader loop.
    /// </summary>
    private sealed class ScriptedReadExactlyReader : IByteReader
    {
        private readonly Queue<byte[]> _reads;

        public ScriptedReadExactlyReader(byte[] firstResponse) =>
            _reads = new Queue<byte[]>([firstResponse]);

        public ValueTask<ByteReadResult> ReadAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Tests use ReadExactlyAsync.");

        public void AdvanceTo(SequencePosition consumed, SequencePosition examined) =>
            throw new InvalidOperationException("Tests use ReadExactlyAsync.");

        public ValueTask ReadExactlyAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var chunk = _reads.Dequeue();
            chunk.Length.Should().Be(buffer.Length);
            chunk.CopyTo(buffer.Span);
            return ValueTask.CompletedTask;
        }
    }
}
