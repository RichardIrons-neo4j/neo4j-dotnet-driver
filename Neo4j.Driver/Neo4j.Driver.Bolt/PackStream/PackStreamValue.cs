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
using Neo4j.Driver.Bolt.PackStream.Abstractions;

namespace Neo4j.Driver.Bolt.PackStream;

public readonly struct PackStreamValue
{
    private readonly PackStreamType _type;
    private readonly long? _intValue;
    private readonly double? _floatValue;
    private readonly bool? _boolValue;
    private readonly ReadOnlySequence<byte>? _bytesValue;
    private readonly ReadOnlySequence<byte>? _stringBytes;
    private readonly PackStreamListValue? _listValue;
    private readonly PackStreamMapValue? _mapValue;

    private PackStreamValue(
        PackStreamType type,
        long? intValue = null,
        double? floatValue = null,
        bool? boolValue = null,
        ReadOnlySequence<byte>? bytesValue = null,
        ReadOnlySequence<byte>? stringBytes = null,
        PackStreamListValue? listValue = null,
        PackStreamMapValue? mapValue = null)
    {
        _type = type;
        _intValue = intValue;
        _floatValue = floatValue;
        _boolValue = boolValue;
        _bytesValue = bytesValue;
        _stringBytes = stringBytes;
        _listValue = listValue;
        _mapValue = mapValue;
    }

    public PackStreamType Type => _type;

    public bool IsNull => _type == PackStreamType.Null;

    // Integer
    public long IntValue => _intValue ?? throw new InvalidOperationException($"Cannot read IntValue from {_type}");
    public static PackStreamValue Integer(long value) => new(PackStreamType.Integer, intValue: value);

    // Float
    public double FloatValue =>
        _floatValue ?? throw new InvalidOperationException($"Cannot read FloatValue from {_type}");

    public static PackStreamValue Float(double value) => new(PackStreamType.Float, floatValue: value);

    // Boolean
    public bool BooleanValue =>
        _boolValue ?? throw new InvalidOperationException($"Cannot read BooleanValue from {_type}");

    public static PackStreamValue Boolean(bool value) => new(PackStreamType.Boolean, boolValue: value);

    // Bytes
    public ReadOnlySequence<byte> BytesValue =>
        _bytesValue ?? throw new InvalidOperationException($"Cannot read BytesValue from {_type}");

    public static PackStreamValue Bytes(ReadOnlySequence<byte> value) => new(PackStreamType.Bytes, bytesValue: value);

    // String
    public PackStreamStringValue StringValue => _stringBytes.HasValue
        ? new PackStreamStringValue(_stringBytes.Value)
        : throw new InvalidOperationException($"Cannot read StringValue from {_type}");

    public static PackStreamValue String(ReadOnlySequence<byte> utf8Bytes) =>
        new(PackStreamType.String, stringBytes: utf8Bytes);

    // List
    public PackStreamListValue ListValue =>
        _listValue ?? throw new InvalidOperationException($"Cannot read ListValue from {_type}");

    internal static PackStreamValue List(
        ReadOnlySequence<byte> itemsData,
        int itemCount,
        IPackStreamDecoder decoder) =>
        new(PackStreamType.List, listValue: new PackStreamListValue(itemsData, itemCount, decoder));

    // Map
    public PackStreamMapValue MapValue =>
        _mapValue ?? throw new InvalidOperationException($"Cannot read MapValue from {_type}");

    internal static PackStreamValue Map(
        ReadOnlySequence<byte> entriesData,
        int entryCount,
        IPackStreamDecoder decoder) =>
        new(PackStreamType.Map, mapValue: new PackStreamMapValue(entriesData, entryCount, decoder));

    // Null
    public static PackStreamValue Null() => new(PackStreamType.Null);

    // TODO: Struct

    public override string ToString() => _type switch
    {
        PackStreamType.Integer => $"INT {_intValue}",
        PackStreamType.Float => $"FLOAT {_floatValue}",
        PackStreamType.Boolean => $"BOOL {_boolValue}",
        PackStreamType.Bytes => $"BYTES[{_bytesValue?.Length ?? 0}]",
        PackStreamType.String => $"STRING[{_stringBytes?.Length ?? 0}]",
        PackStreamType.List => $"LIST[{_listValue?.Count ?? 0}]",
        PackStreamType.Map => $"MAP[{_mapValue?.Count ?? 0}]",
        PackStreamType.Null => "NULL",
        _ => "UNKNOWN"
    };
}
