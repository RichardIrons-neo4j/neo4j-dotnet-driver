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
using System.Collections.Concurrent;
using System.IO.Pipelines;
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;
using Neo4j.Driver.Bolt.Transport.Abstractions;

namespace Neo4j.Driver.Bolt.PackStream.Implementations;

internal class PackStreamDecoder : IPackStreamDecoder
{
    private readonly IChunkAssembler _chunkAssembler;
    private readonly Dictionary<byte, IValueDecoder> _decoders = new();

    public PackStreamDecoder(IValueDecoder[] decoders, IChunkAssembler chunkAssembler)
    {
        _chunkAssembler = chunkAssembler ?? throw new ArgumentNullException(nameof(chunkAssembler));
        foreach (var decoder in decoders)
        {
            foreach (var markerByte in decoder.HandledMarkerBytes)
            {
                _decoders[markerByte] = decoder;
            }
        }
    }

    public async IAsyncEnumerable<PackStreamValue> Decode(PipeReader pipeReader, int valueCount)
    {
        var processed = 0;
        var count = 0;

        await foreach (var buffer in _chunkAssembler.ReadMessagesAsync(pipeReader))
        {
            while (processed < buffer.Length && count < valueCount)
            {
                var remaining = buffer.Slice(processed);
                var markerByte = remaining.First.Span[0];

                if (!_decoders.TryGetValue(markerByte, out var decoder))
                {
                    throw new InvalidOperationException($"No decoder found for marker byte: 0x{markerByte:X2}");
                }

                var decoderResult = decoder.Decode(remaining);
                processed += decoderResult.BytesConsumed;
                count++;
                yield return decoderResult.Value;
            }
        }

        if (count < valueCount)
        {
            throw new InvalidOperationException($"Expected {valueCount} values, but only got {count}.");
        }
    }
}
