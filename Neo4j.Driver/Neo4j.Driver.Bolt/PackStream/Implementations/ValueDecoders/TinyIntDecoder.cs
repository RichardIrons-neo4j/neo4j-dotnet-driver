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
/// Decodes TinyInt values from PackStream format.
/// TinyInt is a single-byte integer in the range -16 to 127.
/// Values 0x00-0x7F represent 0 to 127.
/// Values 0xF0-0xFF represent -16 to -1.
/// </summary>
public class TinyIntDecoder : IValueDecoder
{
    // TinyInt marker bytes: 0x00-0x7F (0 to 127) and 0xF0-0xFF (-16 to -1)
    public byte[] HandledMarkerBytes { get; } = CreateHandledMarkerBytes();

    private static byte[] CreateHandledMarkerBytes()
    {
        // 0x00-0x7F (128 values) + 0xF0-0xFF (16 values) = 144 values
        var markers = new byte[144];
        var index = 0;
        
        // 0x00-0x7F (0 to 127)
        for (var i = 0x00; i <= 0x7F; i++)
        {
            markers[index++] = (byte)i;
        }
        
        // 0xF0-0xFF (-16 to -1)
        for (var i = 0xF0; i <= 0xFF; i++)
        {
            markers[index++] = (byte)i;
        }
        
        return markers;
    }

    public ValueDecoderResult Decode(ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new InvalidOperationException("Buffer is empty. Cannot decode TinyInt value.");
        }

        var marker = buffer.FirstSpan[0];
        
        if (!IsTinyInt(marker))
        {
            throw new InvalidOperationException($"Unknown marker byte: 0x{marker:X2}");
        }

        // The marker byte IS the value (as a signed byte)
        var value = (sbyte)marker;
        return new ValueDecoderResult(PackStreamValue.Int(value), 1);
    }

    private static bool IsTinyInt(byte marker)
    {
        // 0x00-0x7F or 0xF0-0xFF
        return marker <= 0x7F || marker >= 0xF0;
    }
}

