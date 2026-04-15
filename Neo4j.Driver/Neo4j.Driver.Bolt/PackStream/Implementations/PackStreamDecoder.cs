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
using Microsoft.Extensions.Logging;
using Neo4j.Driver.Bolt.PackStream.Abstractions;
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;
using Neo4j.Driver.Bolt.PackStream.Ephemeral;
using Neo4j.Driver.Bolt.Transport.Abstractions;

namespace Neo4j.Driver.Bolt.PackStream.Implementations;

internal class PackStreamDecoder : IPackStreamDecoder
{
    private readonly IChunkAssembler _chunkAssembler;
    private readonly IValueDecoderProvider _valueDecoderProvider;
    private readonly ILogger _logger;

    public PackStreamDecoder(
        IValueDecoder[] decoders,
        IChunkAssembler chunkAssembler,
        IValueDecoderProvider valueDecoderProvider,
        ILogger logger)
    {
        _chunkAssembler = chunkAssembler ?? throw new ArgumentNullException(nameof(chunkAssembler));
        _valueDecoderProvider = valueDecoderProvider;
        _logger = logger;

        if (decoders is null or { Length: 0 })
        {
            throw new ArgumentNullException(nameof(decoders), "At least one decoder must be provided.");
        }
    }

    /// <inheritdoc />
    public ValueDecoderResult Decode(ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new InvalidOperationException("Buffer is empty. Cannot decode value.");
        }

        var markerByte = buffer.First.Span[0];

        var decoder = _valueDecoderProvider.GetDecoder(markerByte, this);
        return decoder.Decode(buffer);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<PackStreamValueView> Decode(IByteReader byteReader, int valueCount)
    {
        var count = 0;

        _logger.LogDebug("Beginning PackStream decode from stream (expecting {ValueCount} values)", valueCount);
        await foreach (var buffer in _chunkAssembler.ReadMessagesAsync(byteReader))
        {
            _logger.LogTrace("Processing message chunk ({Bytes} bytes)", buffer.Length);
            var bufferPosition = 0;
            while (bufferPosition < buffer.Length && count < valueCount)
            {
                var remaining = buffer.Slice(bufferPosition);
                var markerByte = remaining.First.Span[0];
                _logger.LogTrace("Decoding value {Index}/{Total}, marker 0x{Marker:X2}", count + 1, valueCount, markerByte);

                var decoder = _valueDecoderProvider.GetDecoder(markerByte, this);
                var decoderResult = decoder.Decode(remaining);
                _logger.LogTrace(
                    "Decoded value {Index}/{Total}: {Value} (consumed {BytesConsumed} bytes)",
                    count + 1,
                    valueCount,
                    decoderResult.Value,
                    decoderResult.BytesConsumed);

                bufferPosition += decoderResult.BytesConsumed;
                count++;
                yield return decoderResult.Value;
            }
        }

        if (count < valueCount)
        {
            throw new InvalidOperationException($"Expected {valueCount} values, but only got {count}.");
        }

        _logger.LogDebug("Completed PackStream decode: {Count} values", count);
    }
}
