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
using System.Runtime.CompilerServices;
using Neo4j.Driver.Internal.IO;
using Marker = Neo4j.Driver.Internal.IO.PackStream;

namespace Neo4j.Driver.Bolt.PackStream;

public readonly struct PackStreamValue
{
    internal PackStreamValue(
        sbyte? tinyIntValue = null,
        sbyte? int8Value = null,
        short? int16Value = null,
        int? int32Value = null,
        long? int64Value = null,
        float? floatValue = null,
        double? doubleValue = null,
        bool? booleanValue = null,
        ReadOnlyMemory<byte>? bytesValue = null,
        IEnumerable<PackStreamValue>? listValue = null,
        IEnumerable<PackStreamKeyValuePair>? mapValue = null,
        PackStreamStruct? structValue = null)
    {
        var nonNullCount = 0;
        nonNullCount += tinyIntValue == null ? 0 : 1;
        nonNullCount += int8Value == null ? 0 : 1;
        nonNullCount += int16Value == null ? 0 : 1;
        nonNullCount += int32Value == null ? 0 : 1;
        nonNullCount += int64Value == null ? 0 : 1;
        nonNullCount += floatValue == null ? 0 : 1;
        nonNullCount += doubleValue == null ? 0 : 1;
        nonNullCount += booleanValue == null ? 0 : 1;
        nonNullCount += bytesValue == null ? 0 : 1;
        nonNullCount += listValue == null ? 0 : 1;
        nonNullCount += mapValue == null ? 0 : 1;
        nonNullCount += structValue == null ? 0 : 1;

        if (nonNullCount > 1)
        {
            throw new ArgumentException("No more than one parameter can be non-null");
        }
        
        // could do `IsNull = nullCount == 12` here, but
        // that could easily be missed when changing the type
        IsNull = 
            tinyIntValue == null
            && int8Value == null
            && int16Value == null
            && int32Value == null
            && int64Value == null
            && floatValue == null
            && doubleValue == null
            && booleanValue == null
            && bytesValue == null
            && listValue == null
            && mapValue == null
            && structValue == null;
        
        _tinyIntValue = tinyIntValue;
        _int8Value = int8Value;
        _int16Value = int16Value;
        _int32Value = int32Value;
        _int64Value = int64Value;
        _floatValue = floatValue;
        _doubleValue = doubleValue;
        _booleanValue = booleanValue;
        _bytesValue = bytesValue;
        ListValue = listValue;
        MapValue = mapValue;
        StructValue = structValue;
    }

    public bool IsNull { get; }

    private readonly sbyte? _tinyIntValue;
    public sbyte TinyIntValue => _tinyIntValue ?? throw new InvalidOperationException("Value is not a TinyInt");
    public static PackStreamValue TinyInt(sbyte value) => new(tinyIntValue: value);

    private readonly sbyte? _int8Value;
    public sbyte Int8Value => _int8Value ?? throw new InvalidOperationException("Value is not an Int8");
    public static PackStreamValue Int8(sbyte value) => new(int8Value: value);

    private readonly short? _int16Value;
    public short Int16Value => _int16Value ?? throw new InvalidOperationException("Value is not an Int16");
    public static PackStreamValue Int16(short value) => new(int16Value: value);

    private readonly int? _int32Value;
    public int Int32Value => _int32Value ?? throw new InvalidOperationException("Value is not an Int32");
    public static PackStreamValue Int32(int value) => new(int32Value: value);

    private readonly long? _int64Value;
    public long Int64Value => _int64Value ?? throw new InvalidOperationException("Value is not an Int64");
    public static PackStreamValue Int64(long value) => new(int64Value: value);

    private readonly float? _floatValue;
    public float FloatValue => _floatValue ?? throw new InvalidOperationException("Value is not a Float");
    public static PackStreamValue Float(float value) => new(floatValue: value);
    
    private readonly double? _doubleValue;
    public double DoubleValue => _doubleValue ?? throw new InvalidOperationException("Value is not a Double");
    public static PackStreamValue Double(double value) => new(doubleValue: value);

    private readonly bool? _booleanValue;
    public bool BooleanValue => _booleanValue ?? throw new InvalidOperationException("Value is not a Boolean");
    public static PackStreamValue Boolean(bool value) => new(booleanValue: value);

    private readonly ReadOnlyMemory<byte>? _bytesValue;
    public ReadOnlyMemory<byte> BytesValue => _bytesValue ?? throw new InvalidOperationException("Value is not Bytes");

    public IEnumerable<PackStreamValue>? ListValue { get; }
    public IEnumerable<PackStreamKeyValuePair>? MapValue { get; }
    public PackStreamStruct? StructValue { get; }

    public static PackStreamValue Null() => new();
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
            Marker.String8 => _bytes.First.Span[0],
            Marker.String16 => (uint)_seqReader.ReadShort(),
            Marker.String32 => (uint)_seqReader.ReadInt(),
            _ => throw new InvalidOperationException(
                $"Marker byte value 0x{_markerByte:X2} is not a valid string marker")
        };

        _seqReader.Advance(Size);

        _markerByte = markerByte;
        _bytes = bytes;
    }

    public uint Size { get; }

    public ReadOnlySequence<byte> Utf8EncodedChars => _seqReader.UnreadSequence;

    public override string ToString()
    {
        throw new NotImplementedException();
    }
}

public readonly ref struct PackStreamKeyValuePair(PackStreamStringValue key, PackStreamValue value)
{
    public PackStreamStringValue Key { get; } = key;
    public PackStreamValue Value { get; } = value;
}
