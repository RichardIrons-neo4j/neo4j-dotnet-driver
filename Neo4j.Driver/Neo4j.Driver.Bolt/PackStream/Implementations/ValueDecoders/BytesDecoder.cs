﻿// Copyright (c) "Neo4j"
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
/// Decodes Bytes values from PackStream format.
/// Bytes8: marker (0xCC) + 1 byte length + data
/// Bytes16: marker (0xCD) + 2 byte big-endian length + data
/// Bytes32: marker (0xCE) + 4 byte big-endian length + data
/// </summary>
public class BytesDecoder : IValueDecoder
{
    public byte[] HandledMarkerBytes => [PackStreamMarker.Bytes8, PackStreamMarker.Bytes16, PackStreamMarker.Bytes32];

    public ValueDecoderResult Decode(ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new InvalidOperationException("Buffer is empty. Cannot decode Bytes value.");
        }

        var marker = buffer.FirstSpan[0];

        var (headerSize, length) = marker switch
        {
            PackStreamMarker.Bytes8 => ReadBytes8Length(buffer),
            PackStreamMarker.Bytes16 => ReadBytes16Length(buffer),
            PackStreamMarker.Bytes32 => ReadBytes32Length(buffer),
            _ => throw new InvalidOperationException($"Unknown marker byte: 0x{marker:X2}")
        };

        var totalLength = headerSize + length;
        if (buffer.Length < totalLength)
        {
            throw new InvalidOperationException("Buffer too short to read bytes data.");
        }

        var bytesData = buffer.Slice(headerSize, length);
        var value = PackStreamValue.Bytes(bytesData);

        return new ValueDecoderResult(value, totalLength);
    }

    private static (int headerSize, int length) ReadBytes8Length(ReadOnlySequence<byte> buffer)
    {
        if (buffer.Length < 2)
        {
            throw new InvalidOperationException("Buffer too short. Bytes8 requires at least 2 bytes for header.");
        }

        var lengthByte = buffer.Slice(1, 1).FirstSpan[0];
        return (2, lengthByte);
    }

    private static (int headerSize, int length) ReadBytes16Length(ReadOnlySequence<byte> buffer)
    {
        if (buffer.Length < 3)
        {
            throw new InvalidOperationException("Buffer too short. Bytes16 requires at least 3 bytes for header.");
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

    private static (int headerSize, int length) ReadBytes32Length(ReadOnlySequence<byte> buffer)
    {
        if (buffer.Length < 5)
        {
            throw new InvalidOperationException("Buffer too short. Bytes32 requires at least 5 bytes for header.");
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

