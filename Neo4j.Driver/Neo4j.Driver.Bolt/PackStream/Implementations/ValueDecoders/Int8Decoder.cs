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
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;

namespace Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;

/// <summary>
/// Decodes Int8 values from PackStream format.
/// Int8 is a two-byte encoding: marker byte 0xC8 followed by a signed byte value.
/// Used for values in the range -128 to -17 (values -16 to 127 use TinyInt instead).
/// </summary>
public class Int8Decoder : IValueDecoder
{
    public byte[] HandledMarkerBytes => [PackStreamMarker.Int8];

    public ValueDecoderResult Decode(ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new InvalidOperationException("Buffer is empty. Cannot decode Int8 value.");
        }

        if (buffer.FirstSpan[0] != PackStreamMarker.Int8)
        {
            throw new InvalidOperationException($"Unknown marker byte: 0x{buffer.FirstSpan[0]:X2}");
        }

        if (buffer.Length < 2)
        {
            throw new InvalidOperationException("Buffer too short. Int8 requires 2 bytes.");
        }

        var value = (sbyte)buffer.Slice(1).FirstSpan[0];
        return new ValueDecoderResult(PackStreamValue.Int(value), 2);
    }
}

