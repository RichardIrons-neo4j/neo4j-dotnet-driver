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
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;
using Marker = Neo4j.Driver.Internal.IO.PackStream;

namespace Neo4j.Driver.Bolt.PackStream.ValueDecoders;

public class BooleanDecoder : IValueDecoder
{
    public bool CanDecode(byte markerByte)
    {
        return markerByte is Marker.True or Marker.False;
    }

    public PackStreamValue Decode(ReadOnlySequence<byte> buffer)
    {
        if(buffer.IsEmpty)
        {
            throw new InvalidOperationException("Buffer is empty. Cannot decode boolean value.");
        }

        return buffer.FirstSpan[0] switch
        {
            Marker.True => PackStreamValue.Boolean(true),
            Marker.False => PackStreamValue.Boolean(false),
            _ => throw new InvalidOperationException($"Unknown marker byte: 0x{buffer.FirstSpan[0]:X2}")
        };
    }
}
