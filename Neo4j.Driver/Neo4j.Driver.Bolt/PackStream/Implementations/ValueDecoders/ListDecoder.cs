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
using Neo4j.Driver.Bolt.PackStream.Abstractions;
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;

namespace Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;

/// <summary>
/// Decodes List values from PackStream format.
/// TinyList: marker 0x90-0x9F (count in low nibble)
/// List8: marker 0xD4 + 1 byte count
/// List16: marker 0xD5 + 2 byte big-endian count
/// List32: marker 0xD6 + 4 byte big-endian count
/// </summary>
internal class ListDecoder : IValueDecoder
{
    private readonly IPackStreamDecoder _decoder;
    private readonly IPackStreamSizeReader _sizeReader;

    public ListDecoder(IPackStreamDecoder decoder, IPackStreamSizeReader sizeReader)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _sizeReader = sizeReader ?? throw new ArgumentNullException(nameof(sizeReader));
    }

    private static readonly byte[] TinyListMarkers = Enumerable.Range(0x90, 16).Select(i => (byte)i).ToArray();

    public byte[] HandledMarkerBytes =>
        [..TinyListMarkers, PackStreamMarker.List8, PackStreamMarker.List16, PackStreamMarker.List32];

    public ValueDecoderResult Decode(ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsEmpty)
            throw new InvalidOperationException("Buffer is empty. Cannot decode List value.");

        var marker = buffer.FirstSpan[0];

        var (headerSize, itemCount) = marker switch
        {
            >= 0x90 and <= 0x9F => (1, marker & 0x0F),
            PackStreamMarker.List8 => _sizeReader.ReadSize8(buffer, "List8"),
            PackStreamMarker.List16 => _sizeReader.ReadSize16(buffer, "List16"),
            PackStreamMarker.List32 => _sizeReader.ReadSize32(buffer, "List32"),
            _ => throw new InvalidOperationException($"Unknown list marker byte: 0x{marker:X2}")
        };

        // Calculate total bytes by decoding each item (but not materializing values)
        var itemsData = buffer.Slice(headerSize);
        var totalItemBytes = CalculateTotalItemBytes(itemsData, itemCount);

        var value = PackStreamValue.List(
            buffer.Slice(headerSize, totalItemBytes),
            itemCount,
            _decoder);

        return new ValueDecoderResult(value, headerSize + totalItemBytes);
    }

    private int CalculateTotalItemBytes(ReadOnlySequence<byte> data, int itemCount)
    {
        var remaining = data;
        var totalBytes = 0;

        for (var i = 0; i < itemCount; i++)
        {
            if (remaining.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Unexpected end of data: expected {itemCount} list items but only found {i}.");
            }

            var result = _decoder.Decode(remaining);

            if (result.BytesConsumed == 0)
            {
                throw new InvalidOperationException(
                    "Decoder returned zero bytes consumed.");
            }

            if (result.BytesConsumed > remaining.Length)
            {
                throw new InvalidOperationException(
                    $"Decoder reports consuming {result.BytesConsumed} bytes but only {remaining.Length} bytes remain.");
            }

            totalBytes += result.BytesConsumed;
            remaining = remaining.Slice(result.BytesConsumed);
        }

        return totalBytes;
    }
}



