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

using System.IO.Pipelines;

namespace Neo4j.Driver.Bolt.Transport;

/// <summary>
/// Abstraction for the source of bytes from the network. Exposes a <see cref="PipeReader"/> so that
/// the chunk assembler and downstream layers can consume data without depending on a concrete stream
/// or socket. In tests, implement with an in-memory <see cref="Pipe"/> whose writer is fed canned data.
/// </summary>
public interface IBytePipeSource
{
    /// <summary>
    /// The pipe reader from which bytes are read. Caller does not own the reader; do not complete it
    /// unless the connection is being closed.
    /// </summary>
    PipeReader PipeReader { get; }
}
