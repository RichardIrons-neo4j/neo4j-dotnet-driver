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
using System.Runtime.CompilerServices;

namespace Neo4j.Driver.Bolt.Transport;

public class ChunkAssembler : IChunkAssembler
{
    public async IAsyncEnumerable<ReadOnlySequence<byte>> ReadMessagesAsync(
        PipeReader pipeReader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var unread = ReadOnlySequence<byte>.Empty;
        var allConsumed = false;
        SequencePosition consumed = default;
        SequencePosition examined = default;
        var advance = false;
        try
        {
            while (!allConsumed)
            {
                if (advance)
                {
                    pipeReader.AdvanceTo(consumed, examined);
                }

                short bytesNeeded = 0;
                var readResult = await pipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var buffer = readResult.Buffer;
                var seqReader = new SequenceReader<byte>(buffer);
                var message = ReadOnlySequence<byte>.Empty;

                do
                {
                    if (bytesNeeded == 0)
                    {
                        // if not enough remaining, break for next read
                        if (seqReader.Remaining >= sizeof(short))
                        {
                            if (!seqReader.TryReadBigEndian(out bytesNeeded))
                            {
                                throw new ProtocolException(
                                    $"Failed to read chunk header, expected {sizeof(short)} bytes " +
                                    $"but only {seqReader.Remaining} available.");
                            }
                        }
                    }

                    // if no more bytes, break and read again
                    if (seqReader.Remaining < bytesNeeded)
                    {
                        break;
                    }

                    var bytesToRead = (short)Math.Min(seqReader.Remaining, bytesNeeded);
                    if (!seqReader.TryReadExact(bytesToRead, out message))
                    {
                        throw new ProtocolException(
                            $"Failed to read chunk header, expected {sizeof(short)} bytes " +
                            $"but only {seqReader.Remaining} available.");
                    }

                    bytesNeeded -= bytesToRead;
                } while (bytesNeeded > 0);

                examined = buffer.End;

                if (!message.IsEmpty)
                {
                    yield return message;
                    consumed = buffer.GetPosition(sizeof(short) + message.Length);
                }

                advance = true;
                allConsumed = readResult.IsCompleted && consumed.Equals(buffer.End);
            }
        }
        finally
        {
           await pipeReader.CompleteAsync().ConfigureAwait(false);
        }
    }

    private ReadOnlySequence<byte> ConcatSequences(params ReadOnlySequence<byte>[] sequences)
    {
        var start = new TestSequenceSegment(ReadOnlyMemory<byte>.Empty);
        var end = start;

        foreach (var sequence in sequences)
        {
            foreach (var segment in sequence)
            {
                end = end.Append(segment);
            }
        }

        return new ReadOnlySequence<byte>(start, 0, end, end.Memory.Length);
    }

    private class TestSequenceSegment : ReadOnlySequenceSegment<byte>
    {
        public TestSequenceSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public TestSequenceSegment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new TestSequenceSegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };

            Next = next;
            return next;
        }

    }
}
