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
/// Decodes Int16 values from PackStream format.
/// Int16 is a three-byte encoding: marker byte 0xC9 followed by a big-endian signed 16-bit integer.
/// </summary>
public class Int16Decoder : IValueDecoder
{
    public byte[] HandledMarkerBytes => [PackStreamMarker.Int16];

    public ValueDecoderResult Decode(ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new InvalidOperationException("Buffer is empty. Cannot decode Int16 value.");
        }

        if (buffer.FirstSpan[0] != PackStreamMarker.Int16)
        {
            throw new InvalidOperationException($"Unknown marker byte: 0x{buffer.FirstSpan[0]:X2}");
        }

        if (buffer.Length < 3)
        {
            throw new InvalidOperationException("Buffer too short. Int16 requires 3 bytes.");
        }

        var valueBytes = buffer.Slice(1, 2);
        short value;
        if (valueBytes.IsSingleSegment)
        {
            value = BinaryPrimitives.ReadInt16BigEndian(valueBytes.FirstSpan);
        }
        else
        {
            Span<byte> temp = stackalloc byte[2];
            valueBytes.CopyTo(temp);
            value = BinaryPrimitives.ReadInt16BigEndian(temp);
        }

        return new ValueDecoderResult(PackStreamValue.Int(value), 3);
    }
}

