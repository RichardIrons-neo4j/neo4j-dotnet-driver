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
/// Decodes Int32 values from PackStream format.
/// Int32 is a five-byte encoding: marker byte 0xCA followed by a big-endian signed 32-bit integer.
/// </summary>
public class Int32Decoder : IValueDecoder
{
    public byte[] HandledMarkerBytes => [PackStreamMarker.Int32];

    public ValueDecoderResult Decode(ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new InvalidOperationException("Buffer is empty. Cannot decode Int32 value.");
        }

        if (buffer.FirstSpan[0] != PackStreamMarker.Int32)
        {
            throw new InvalidOperationException($"Unknown marker byte: 0x{buffer.FirstSpan[0]:X2}");
        }

        if (buffer.Length < 5)
        {
            throw new InvalidOperationException("Buffer too short. Int32 requires 5 bytes.");
        }

        var valueBytes = buffer.Slice(1, 4);
        int value;
        if (valueBytes.IsSingleSegment)
        {
            value = BinaryPrimitives.ReadInt32BigEndian(valueBytes.FirstSpan);
        }
        else
        {
            Span<byte> temp = stackalloc byte[4];
            valueBytes.CopyTo(temp);
            value = BinaryPrimitives.ReadInt32BigEndian(temp);
        }

        return new ValueDecoderResult(PackStreamValue.Int(value), 5);
    }
}

