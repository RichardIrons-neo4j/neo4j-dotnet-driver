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
using Neo4j.Driver.Internal.IO;
using Marker = Neo4j.Driver.Internal.IO.PackStream;

namespace Neo4j.Driver.Bolt.PackStream;

internal class PackStreamDecoder : IPackStreamDecoder
{
    private readonly IValueDecoder[] _decoders;

    public PackStreamDecoder(IValueDecoder[] decoders)
    {
        _decoders = decoders;
    }

    public IEnumerable<PackStreamValue> Decode(ReadOnlySequence<byte> buffer, int valueCount)
    {
        var processed = 0;
        var count = 0;
        while (processed < buffer.Length && count < valueCount)
        {
            var decoded = DecodeInternal(buffer.Slice(processed));
            count++;
            yield return decoded;
        }

        if (count < valueCount)
        {
            throw new InvalidOperationException($"Expected {valueCount} values, but only got {count}.");
        }
    }
    
    private PackStreamValue DecodeInternal(ReadOnlySequence<byte> sequence)
    {
        var markerByte = sequence.FirstSpan[0];
        var decoder = _decoders.FirstOrDefault(d => d.CanDecode(markerByte));
        return decoder != null 
            ? decoder.Decode(sequence) 
            : throw new InvalidOperationException($"No decoder found for marker byte: 0x{markerByte:X2}");
    }
}
