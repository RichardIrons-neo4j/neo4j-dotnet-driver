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
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;

namespace Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;

/// <summary>
/// Decodes String values from PackStream format.
/// TinyString: marker 0x80-0x8F (length in low nibble)
/// String8: marker 0xD0 + 1 byte length
/// String16: marker 0xD1 + 2 byte big-endian length
/// String32: marker 0xD2 + 4 byte big-endian length
/// </summary>
internal class StringDecoder : IValueDecoder
{
    private readonly IPackStreamSizeReader _sizeReader;

    private static readonly byte[] TinyStringMarkers = Enumerable.Range(0x80, 16).Select(i => (byte)i).ToArray();

    public StringDecoder(IPackStreamSizeReader sizeReader)
    {
        _sizeReader = sizeReader ?? throw new ArgumentNullException(nameof(sizeReader));
    }

    public byte[] HandledMarkerBytes => [..TinyStringMarkers, PackStreamMarker.String8, PackStreamMarker.String16, PackStreamMarker.String32];

    public ValueDecoderResult Decode(ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new InvalidOperationException("Buffer is empty. Cannot decode String value.");
        }

        var marker = buffer.FirstSpan[0];

        var (headerSize, length) = marker switch
        {
            >= 0x80 and <= 0x8F => (1, marker & 0x0F),
            PackStreamMarker.String8 => _sizeReader.ReadSize8(buffer, "String8"),
            PackStreamMarker.String16 => _sizeReader.ReadSize16(buffer, "String16"),
            PackStreamMarker.String32 => _sizeReader.ReadSize32(buffer, "String32"),
            _ => throw new InvalidOperationException($"Unknown marker byte: 0x{marker:X2}")
        };

        var totalLength = headerSize + length;
        if (buffer.Length < totalLength)
        {
            throw new InvalidOperationException("Buffer too short to read string data.");
        }

        var stringData = buffer.Slice(headerSize, length);
        var value = PackStreamValue.String(stringData);

        return new ValueDecoderResult(value, totalLength);
    }
}
