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

using Neo4j.Driver.Bolt.Transport.Types;

namespace Neo4j.Driver.Bolt.Transport.Abstractions;

/// <summary>
/// Abstraction over a byte source for reading streaming data.
/// </summary>
public interface IByteReader
{
    /// <summary>
    /// Asynchronously reads bytes from the source.
    /// </summary>
    ValueTask<ByteReadResult> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the reader past the consumed data.
    /// </summary>
    /// <param name="consumed">The position up to which data has been consumed.</param>
    /// <param name="examined">The position up to which data has been examined.</param>
    void AdvanceTo(SequencePosition consumed, SequencePosition examined);
}