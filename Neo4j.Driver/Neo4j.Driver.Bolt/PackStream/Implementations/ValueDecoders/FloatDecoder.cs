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
/// Decodes Float64 values from PackStream format.
/// Float64 is a nine-byte encoding: marker byte 0xC1 followed by a big-endian IEEE 754 double-precision float.
/// </summary>
public class FloatDecoder : IValueDecoder
{
    public byte[] HandledMarkerBytes => [PackStreamMarker.Float64];

    public ValueDecoderResult Decode(ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new InvalidOperationException("Buffer is empty. Cannot decode Float value.");
        }

        if (buffer.FirstSpan[0] != PackStreamMarker.Float64)
        {
            throw new InvalidOperationException($"Unknown marker byte: 0x{buffer.FirstSpan[0]:X2}");
        }

        if (buffer.Length < 9)
        {
            throw new InvalidOperationException("Buffer too short. Float64 requires 9 bytes.");
        }

        var valueBytes = buffer.Slice(1, 8);
        double value;
        if (valueBytes.IsSingleSegment)
        {
            value = BinaryPrimitives.ReadDoubleBigEndian(valueBytes.FirstSpan);
        }
        else
        {
            Span<byte> temp = stackalloc byte[8];
            valueBytes.CopyTo(temp);
            value = BinaryPrimitives.ReadDoubleBigEndian(temp);
        }

        return new ValueDecoderResult(PackStreamValue.Float(value), 9);
    }
}

