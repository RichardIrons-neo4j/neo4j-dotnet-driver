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

namespace Neo4j.Driver.Bolt.PackStream;

/// <summary>
/// Reads PackStream size headers from a buffer.
/// Used by decoders for String, Bytes, List, and Map types.
/// </summary>
internal interface IPackStreamSizeReader
{
    /// <summary>
    /// Reads a 1-byte size value from the buffer at position 1.
    /// Returns (headerSize: 2, count).
    /// </summary>
    (int HeaderSize, int Count) ReadSize8(ReadOnlySequence<byte> buffer, string typeName);

    /// <summary>
    /// Reads a 2-byte big-endian size value from the buffer at position 1.
    /// Returns (headerSize: 3, count).
    /// </summary>
    (int HeaderSize, int Count) ReadSize16(ReadOnlySequence<byte> buffer, string typeName);

    /// <summary>
    /// Reads a 4-byte big-endian size value from the buffer at position 1.
    /// Returns (headerSize: 5, count).
    /// </summary>
    (int HeaderSize, int Count) ReadSize32(ReadOnlySequence<byte> buffer, string typeName);
}

/// <inheritdoc />
internal class PackStreamSizeReader : IPackStreamSizeReader
{
    /// <inheritdoc />
    public (int HeaderSize, int Count) ReadSize8(ReadOnlySequence<byte> buffer, string typeName)
    {
        if (buffer.Length < 2)
            throw new InvalidOperationException($"Buffer too short. {typeName} requires at least 2 bytes.");

        return (2, buffer.Slice(1, 1).FirstSpan[0]);
    }

    /// <inheritdoc />
    public (int HeaderSize, int Count) ReadSize16(ReadOnlySequence<byte> buffer, string typeName)
    {
        if (buffer.Length < 3)
            throw new InvalidOperationException($"Buffer too short. {typeName} requires at least 3 bytes.");

        var slice = buffer.Slice(1, 2);
        if (slice.IsSingleSegment)
            return (3, BinaryPrimitives.ReadUInt16BigEndian(slice.FirstSpan));

        Span<byte> temp = stackalloc byte[2];
        slice.CopyTo(temp);
        return (3, BinaryPrimitives.ReadUInt16BigEndian(temp));
    }

    /// <inheritdoc />
    public (int HeaderSize, int Count) ReadSize32(ReadOnlySequence<byte> buffer, string typeName)
    {
        if (buffer.Length < 5)
            throw new InvalidOperationException($"Buffer too short. {typeName} requires at least 5 bytes.");

        var slice = buffer.Slice(1, 4);
        if (slice.IsSingleSegment)
            return (5, (int)BinaryPrimitives.ReadUInt32BigEndian(slice.FirstSpan));

        Span<byte> temp = stackalloc byte[4];
        slice.CopyTo(temp);
        return (5, (int)BinaryPrimitives.ReadUInt32BigEndian(temp));
    }
}


