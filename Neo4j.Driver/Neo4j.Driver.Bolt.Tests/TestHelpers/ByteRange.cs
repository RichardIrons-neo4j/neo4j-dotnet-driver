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

using System.Collections;

namespace Neo4j.Driver.Bolt.Tests.TestHelpers;

public class ByteRange : IEnumerable<byte>
{
    private static readonly byte[] AllBytes = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();
    private readonly List<byte> _theseBytes = [];
    
    public ByteRange(params Range[] ranges)
    {
        foreach (var range in ranges)
        {
            if (range.End.Value is > 255 or < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ranges), "Range end must be between 0 and 255.");
            }
            
            _theseBytes.AddRange(AllBytes.AsSpan(range));
        }
    }

    public ByteRange Add(params byte[] bytes)
    {
        _theseBytes.AddRange(bytes);
        return this;
    }

    public ByteRange Add(params Range[] ranges)
    {
        foreach (var range in ranges)
        {
            if (range.End.Value is > 255 or < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ranges), "Range end must be between 0 and 255.");
            }

            _theseBytes.AddRange(AllBytes.AsSpan(range));
        }

        return this;
    }
    
    public IEnumerator<byte> GetEnumerator()
    {
        return _theseBytes.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_theseBytes).GetEnumerator();
    }
}
