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
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;

namespace Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;

/// <summary>
/// Decodes String values from PackStream format.
/// TinyString: marker 0x80-0x8F (length in low nibble)
/// String8: marker 0xD0 + 1 byte length
/// String16: marker 0xD1 + 2 byte big-endian length
/// String32: marker 0xD2 + 4 byte big-endian length
/// </summary>
public class StringDecoder : IValueDecoder
{
    private static readonly byte[] TinyStringMarkers = Enumerable.Range(0x80, 16).Select(i => (byte)i).ToArray();

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
            PackStreamMarker.String8 => ReadString8Length(buffer),
            PackStreamMarker.String16 => ReadString16Length(buffer),
            PackStreamMarker.String32 => ReadString32Length(buffer),
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

    private static (int headerSize, int length) ReadString8Length(ReadOnlySequence<byte> buffer)
    {
        if (buffer.Length < 2)
        {
            throw new InvalidOperationException("Buffer too short. String8 requires at least 2 bytes for header.");
        }

        var lengthByte = buffer.Slice(1, 1).FirstSpan[0];
        return (2, lengthByte);
    }

    private static (int headerSize, int length) ReadString16Length(ReadOnlySequence<byte> buffer)
    {
        if (buffer.Length < 3)
        {
            throw new InvalidOperationException("Buffer too short. String16 requires at least 3 bytes for header.");
        }

        var lengthSlice = buffer.Slice(1, 2);
        ushort length;
        if (lengthSlice.IsSingleSegment)
        {
            length = BinaryPrimitives.ReadUInt16BigEndian(lengthSlice.FirstSpan);
        }
        else
        {
            Span<byte> lengthBytes = stackalloc byte[2];
            lengthSlice.CopyTo(lengthBytes);
            length = BinaryPrimitives.ReadUInt16BigEndian(lengthBytes);
        }

        return (3, length);
    }

    private static (int headerSize, int length) ReadString32Length(ReadOnlySequence<byte> buffer)
    {
        if (buffer.Length < 5)
        {
            throw new InvalidOperationException("Buffer too short. String32 requires at least 5 bytes for header.");
        }

        var lengthSlice = buffer.Slice(1, 4);
        int length;
        if (lengthSlice.IsSingleSegment)
        {
            length = (int)BinaryPrimitives.ReadUInt32BigEndian(lengthSlice.FirstSpan);
        }
        else
        {
            Span<byte> lengthBytes = stackalloc byte[4];
            lengthSlice.CopyTo(lengthBytes);
            length = (int)BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
        }

        return (5, length);
    }
}
