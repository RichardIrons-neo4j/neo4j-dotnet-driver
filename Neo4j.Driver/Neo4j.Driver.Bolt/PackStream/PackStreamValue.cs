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

namespace Neo4j.Driver.Bolt.PackStream;

public struct PackStreamValue
{
    private readonly byte _typeByte;
    private readonly ReadOnlySequence<byte> _bytes;
    
    public byte? ByteValue { get; }
    public short? ShortValue { get; }
    public int? IntValue { get; }
    public long? LongValue { get; }
    public float? FloatValue { get; }
    public double? DoubleValue { get; }
    public string? StringValue { get; }
    public bool? BooleanValue { get; }
    public List<PackStreamValue>? ListValue { get; }
    public Dictionary<string, PackStreamValue>? DictionaryValue { get; }
    public PackStreamStruct? StructValue { get; }

    private PackStreamValue(ReadOnlySequence<byte> bytes)
    {
        _bytes = bytes;
        _typeByte = bytes.First.Span[0];
    }
}

public struct PackStreamStruct
{
    public byte Tag { get; }
    
}
