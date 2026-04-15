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
/// Decodes Bytes values from PackStream format.
/// Bytes8: marker (0xCC) + 1 byte length + data
/// Bytes16: marker (0xCD) + 2 byte big-endian length + data
/// Bytes32: marker (0xCE) + 4 byte big-endian length + data
/// </summary>
internal class BytesDecoder : IValueDecoder
{
    private readonly IPackStreamSizeReader _sizeReader;

    public BytesDecoder(IPackStreamSizeReader sizeReader)
    {
        _sizeReader = sizeReader ?? throw new ArgumentNullException(nameof(sizeReader));
    }

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
            PackStreamMarker.Bytes8 => _sizeReader.ReadSize8(buffer, "Bytes8"),
            PackStreamMarker.Bytes16 => _sizeReader.ReadSize16(buffer, "Bytes16"),
            PackStreamMarker.Bytes32 => _sizeReader.ReadSize32(buffer, "Bytes32"),
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
}

