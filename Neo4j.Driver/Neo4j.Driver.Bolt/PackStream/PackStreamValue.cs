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
using Neo4j.Driver.Internal.IO;
using PackStreamMarker = Neo4j.Driver.Internal.IO.PackStream;

namespace Neo4j.Driver.Bolt.PackStream;

public readonly ref struct PackStreamValue
{
    private readonly byte _markerByte;
    private readonly ReadOnlySequence<byte> _bytes;
    private readonly PackStreamType _type;

    private PackStreamValue(ReadOnlySequence<byte> bytes)
    {
        _markerByte = bytes.First.Span[0];
        _bytes = bytes.Slice(1);

        _type = _markerByte switch
        {
            PackStreamMarker.Null => PackStreamType.Null,
            PackStreamMarker.True or PackStreamMarker.False => PackStreamType.Boolean,
            PackStreamMarker.Float64 or PackStreamMarker.Float32 => PackStreamType.Float,
            PackStreamMarker.Bytes8 or PackStreamMarker.Bytes16 or PackStreamMarker.Bytes32 => PackStreamType.Bytes,
            PackStreamMarker.String8 or PackStreamMarker.String16 or PackStreamMarker.String32 => PackStreamType.String,
            PackStreamMarker.List8 or PackStreamMarker.List16 or PackStreamMarker.List32 => PackStreamType.List,
            PackStreamMarker.Map8 or PackStreamMarker.Map16 or PackStreamMarker.Map32 => PackStreamType.Map,
            PackStreamMarker.Struct8 or PackStreamMarker.Struct16 => PackStreamType.Struct,
            PackStreamMarker.Int8 
                or PackStreamMarker.Int16 
                or PackStreamMarker.Int32 
                or PackStreamMarker.Int64 => PackStreamType.Integer,
            <= 0x7F or >= 0xF0 => PackStreamType.Integer, // Tiny [negative] int
            var b when (b & 0xF0) == PackStreamMarker.TinyString => PackStreamType.String, // Tiny string
            var b when (b & 0xF0) == PackStreamMarker.TinyList => PackStreamType.List, // Tiny list
            var b when (b & 0xF0) == PackStreamMarker.TinyMap => PackStreamType.Map, // Tiny map
            var b when (b & 0xF0) == PackStreamMarker.TinyStruct => PackStreamType.Struct, // Tiny struct
            _ => throw new InvalidOperationException($"Unknown marker byte: 0x{_markerByte:X2}")
        };
    }
    
    // Factory method
    public static PackStreamValue Create(ReadOnlySequence<byte> bytes) => new(bytes);

    public sbyte ByteValue
    {
        get
        {
            if (_markerByte is >= 0xF0 or <= 0x7F)
            {
                return (sbyte)_markerByte;
            }

            throw new InvalidOperationException($"Marker byte value 0x{_markerByte:X2} is not a byte");
        }
    }

    public sbyte Int8Value =>
        (_markerByte == PackStreamMarker.Int8)
            ? (sbyte)_bytes.First.Span[0]
            : throw new InvalidOperationException($"Marker byte value 0x{_markerByte:X2} is not an Int8");

    public short Int16Value
    {
        get
        {
            if (_markerByte == PackStreamMarker.Int16)
            {
                var sequenceReader = new SequenceReader<byte>(_bytes);
                return sequenceReader.ReadShort();
            }

            throw new InvalidOperationException($"Marker byte value 0x{_markerByte:X2} is not a TinyInt");
        }
    }

    public int Int32Value
    {
        get
        {
            if (_markerByte == PackStreamMarker.Int32)
            {
                var sequenceReader = new SequenceReader<byte>(_bytes);
                return sequenceReader.ReadInt();
            }

            throw new InvalidOperationException($"Marker byte value 0x{_markerByte:X2} is not an Int32");
        }
    }

    public long LongValue
    {
        get
        {
            if (_markerByte == PackStreamMarker.Int64)
            {
                var sequenceReader = new SequenceReader<byte>(_bytes);
                return sequenceReader.ReadLong();
            }

            throw new InvalidOperationException($"Marker byte value 0x{_markerByte:X2} is not an Int64");
        }
    }

    public float FloatValue
    {
        get
        {
            if (_markerByte == PackStreamMarker.Float32)
            {
                var sequenceReader = new SequenceReader<byte>(_bytes);
                return BitConverter.Int32BitsToSingle(sequenceReader.ReadInt());
            }

            throw new InvalidOperationException($"Marker byte value 0x{_markerByte:X2} is not a Float32");
        }
    }

    public double DoubleValue
    {
        get
        {
            if (_markerByte == PackStreamMarker.Float64)
            {
                var sequenceReader = new SequenceReader<byte>(_bytes);
                return BitConverter.Int64BitsToDouble(sequenceReader.ReadLong());
            }

            throw new InvalidOperationException($"Marker byte value 0x{_markerByte:X2} is not a Float64");
        }
    }

    public PackStreamStringValue StringValue => new(_markerByte, _bytes);

    public bool BooleanValue
    {
        get
        {
            return _markerByte switch
            {
                0xC2 => false,
                0xC3 => true,
                _ => throw new InvalidOperationException($"Marker byte value 0x{_markerByte:X2} is not a bool")
            };
        }
    }
    
    public bool IsNull => _markerByte == PackStreamMarker.Null;

    public IEnumerable<PackStreamValue>? ListValue { get; }
    public IEnumerable<PackStreamKeyValuePair>? MapValue { get; }
    public PackStreamStruct? StructValue { get; }
}

public struct PackStreamStruct
{
    public byte Tag { get; }
}

public readonly ref struct PackStreamStringValue
{
    private readonly byte _markerByte;
    private readonly ReadOnlySequence<byte> _bytes;
    private readonly SequenceReader<byte> _seqReader;

    public PackStreamStringValue(byte markerByte, ReadOnlySequence<byte> bytes)
    {
        (int hi, int lo) markerNibbles = (markerByte & 0xF0, markerByte & 0x0F);
        if (markerNibbles is not ((0x80, _) or (0xD0, >= 0 and <= 2)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(markerByte),
                markerByte,
                "Invalid marker byte for string value");
        }

        _seqReader = new SequenceReader<byte>(_bytes);
        
        Size = _markerByte switch
        {
            PackStreamMarker.String8 => _bytes.First.Span[0],
            PackStreamMarker.String16 => (uint)_seqReader.ReadShort(),
            PackStreamMarker.String32 => (uint)_seqReader.ReadInt(),
            _ => throw new InvalidOperationException(
                $"Marker byte value 0x{_markerByte:X2} is not a valid string marker")
        };
        
        _seqReader.Advance(Size);

        _markerByte = markerByte;
        _bytes = bytes;
    }

    public uint Size { get; }

    public ReadOnlySequence<byte> Utf8EncodedChars => _seqReader.UnreadSequence;
}

public readonly ref struct PackStreamKeyValuePair(PackStreamStringValue key, PackStreamValue value)
{
    public PackStreamStringValue Key { get; } = key;
    public PackStreamValue Value { get; } = value;
}
