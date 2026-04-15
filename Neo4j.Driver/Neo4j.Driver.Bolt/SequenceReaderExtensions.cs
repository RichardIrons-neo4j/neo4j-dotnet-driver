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

namespace Neo4j.Driver.Bolt;

public static class SequenceReaderExtensions
{
    extension(SequenceReader<byte> sequenceReader) 
    {
        public int ReadInt()
        {
            return sequenceReader.TryReadBigEndian(out int value)
                ? value
                : throw new ProtocolException("Failed to read integer from memory buffer");
        }

        public short ReadShort()
        {
            return sequenceReader.TryReadBigEndian(out short value)
                ? value
                : throw new ProtocolException("Failed to read short from memory buffer");
        }

        public long ReadLong()
        {
            return sequenceReader.TryReadBigEndian(out long value)
                ? value
                : throw new ProtocolException("Failed to read long from memory buffer");
        }
    }
}
