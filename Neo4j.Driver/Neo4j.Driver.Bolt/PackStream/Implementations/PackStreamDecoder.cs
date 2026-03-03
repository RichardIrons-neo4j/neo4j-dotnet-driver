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

using Microsoft.Extensions.Logging;
using Neo4j.Driver.Bolt.Extensions;
using Neo4j.Driver.Bolt.PackStream.Abstractions;
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;
using Neo4j.Driver.Bolt.Transport.Abstractions;

namespace Neo4j.Driver.Bolt.PackStream.Implementations;

internal class PackStreamDecoder : IPackStreamDecoder
{
    private readonly IChunkAssembler _chunkAssembler;
    private readonly ILogger _logger;
    private readonly Dictionary<byte, IValueDecoder> _decoders = new();

    public PackStreamDecoder(
        IValueDecoder[] decoders,
        IChunkAssembler chunkAssembler,
        ILogger logger)
    {
        _chunkAssembler = chunkAssembler ?? throw new ArgumentNullException(nameof(chunkAssembler));
        _logger = logger;

        if (decoders is null or { Length: 0 })
        {
            throw new ArgumentNullException(nameof(decoders), "At least one decoder must be provided.");
        }

        foreach (var decoder in decoders)
        {
            foreach (var markerByte in decoder.HandledMarkerBytes)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Registering decoder {decoder} for marker byte: {markerByte}",
                        decoder.GetType().Name,
                        $"0x{markerByte:X2}");
                }

                _decoders[markerByte] = decoder;
            }
        }
    }

    public async IAsyncEnumerable<PackStreamValue> Decode(IByteReader byteReader, int valueCount)
    {
        var processed = 0;
        var count = 0;

        _logger.LogDebug("Beginning PackStream decoding loop");
        await foreach (var buffer in _chunkAssembler.ReadMessagesAsync(byteReader))
        {
            _logger.LogIf(LogLevel.Trace, "Decoding {bytes} bytes", () => [buffer.Length]);
            var bufferPosition = 0;
            while (bufferPosition < buffer.Length && count < valueCount)
            {
                var remaining = buffer.Slice(bufferPosition);
                var markerByte = remaining.First.Span[0];
                _logger.LogIf(LogLevel.Trace, "Decoding marker byte: {markerByte}", () => [markerByte]);

                if (!_decoders.TryGetValue(markerByte, out var decoder))
                {
                    throw new InvalidOperationException($"No decoder found for marker byte: 0x{markerByte:X2}");
                }

                var decoderResult = decoder.Decode(remaining);
                _logger.LogTrace(
                    "Decoded value: {value} (consumed {bytesConsumed} bytes)",
                    decoderResult.Value,
                    decoderResult.BytesConsumed);

                bufferPosition += decoderResult.BytesConsumed;
                count++;
                yield return decoderResult.Value;
            }

            processed += bufferPosition;
        }

        if (count < valueCount)
        {
            throw new InvalidOperationException($"Expected {valueCount} values, but only got {count}.");
        }
    }
}
