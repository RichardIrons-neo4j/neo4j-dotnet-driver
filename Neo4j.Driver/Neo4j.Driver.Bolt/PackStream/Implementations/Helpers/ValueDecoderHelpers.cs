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
using Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;

namespace Neo4j.Driver.Bolt.PackStream.Implementations.Helpers;

public static class ValueDecoderHelpers
{
    public static int ReadSize(ref SequenceReader<byte> reader, ValueDecoderBase.IntegerSize integerSize)
    {
        return integerSize switch
        {
            // TryRead succeeded
            ValueDecoderBase.IntegerSize.Byte when reader.TryRead(out var b) => b,
            ValueDecoderBase.IntegerSize.Short when reader.TryReadBigEndian(out short s) => (ushort)s,
            ValueDecoderBase.IntegerSize.Int when reader.TryReadBigEndian(out int i) => i,

            // TryRead failed
            ValueDecoderBase.IntegerSize.Byte or ValueDecoderBase.IntegerSize.Short or ValueDecoderBase.IntegerSize.Int
                => throw new InvalidOperationException("Buffer too short to read length."),

            _ => throw new ArgumentOutOfRangeException(nameof(integerSize), integerSize, null)
        };
    }

    public static long ReadInteger(ref SequenceReader<byte> reader, ValueDecoderBase.IntegerSize integerSize)
    {
        return integerSize switch
        {
            // TryRead succeeded
            ValueDecoderBase.IntegerSize.Byte when reader.TryRead(out var b) => (sbyte)b,
            ValueDecoderBase.IntegerSize.Short when reader.TryReadBigEndian(out short s) => s,
            ValueDecoderBase.IntegerSize.Int when reader.TryReadBigEndian(out int i) => i,
            ValueDecoderBase.IntegerSize.Long when reader.TryReadBigEndian(out long l) => l,

            // TryRead failed
            ValueDecoderBase.IntegerSize.Byte or ValueDecoderBase.IntegerSize.Short or ValueDecoderBase.IntegerSize.Int or ValueDecoderBase.IntegerSize.Long
                => throw new InvalidOperationException("Buffer too short to read length."),

            _ => throw new ArgumentOutOfRangeException(nameof(integerSize), integerSize, null)
        };
    }

    public static void EnsureBufferNotEmpty(ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new InvalidOperationException("Buffer is empty.");
        }
    }

    public static void EnsureReaderNotFinished(SequenceReader<byte> reader)
    {
        if (reader.End)
        {
            throw new InvalidOperationException("Unexpected end of buffer.");
        }
    }

    public static byte ReadByte(ref SequenceReader<byte> reader)
    {
        return reader.TryRead(out var b)
            ? b
            : throw new InvalidOperationException("Buffer too short to read byte.");
    }

    public static ReadOnlySequence<byte> ReadExact(ref SequenceReader<byte> reader, int count)
    {
        return reader.TryReadExact(count, out var data)
            ? data
            : throw new InvalidOperationException($"Buffer too short to read {count} bytes.");
    }

    public static double ReadDouble(ref SequenceReader<byte> reader)
    {
        var buffer = ReadExact(ref reader, 8);
        if (buffer.IsSingleSegment)
        {
            return BinaryPrimitives.ReadDoubleBigEndian(buffer.FirstSpan);
        }
        else
        {
            Span<byte> temp = stackalloc byte[8];
            buffer.CopyTo(temp);
            return BinaryPrimitives.ReadDoubleBigEndian(temp);
        }
    }
}
