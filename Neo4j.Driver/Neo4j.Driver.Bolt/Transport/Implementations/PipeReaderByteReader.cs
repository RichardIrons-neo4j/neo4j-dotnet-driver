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
using Neo4j.Driver.Bolt.Transport.Abstractions;

namespace Neo4j.Driver.Bolt.Transport.Implementations;

/// <summary>
/// Adapts a <see cref="PipeReader"/> to the <see cref="IByteReader"/> interface.
/// </summary>
public sealed class PipeReaderByteReader : IByteReader
{
    private readonly PipeReader _pipeReader;

    public PipeReaderByteReader(PipeReader pipeReader)
    {
        _pipeReader = pipeReader ?? throw new ArgumentNullException(nameof(pipeReader));
    }

    public async ValueTask<ByteReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        var result = await _pipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return new ByteReadResult
        {
            Buffer = result.Buffer,
            IsCompleted = result.IsCompleted
        };
    }

    public void AdvanceTo(SequencePosition consumed, SequencePosition examined)
    {
        _pipeReader.AdvanceTo(consumed, examined);
    }
}

