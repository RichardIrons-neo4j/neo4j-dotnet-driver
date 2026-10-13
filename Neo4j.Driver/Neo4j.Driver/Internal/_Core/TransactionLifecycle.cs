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

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Neo4j.Driver.Internal.MessageHandling;

namespace Neo4j.Driver.Internal;

internal class TransactionLifecycle : ITransactionLifecycle
{
    private Exception? _error;
    private IResponsePipelineError? _pendingError;
    private TransactionState _state = TransactionState.Created;

    public TransactionState GetState()
    {
        return _state;
    }

    public bool TryGetError([NotNullWhen(true)] out Exception? error)
    {
        error = _error;
        return error is not null;
    }

    public void Activate()
    {
        _state = TransactionState.Active;
    }

    public void EnsureCanRun()
    {
        EnsureUsable("run query in this transaction");
    }

    public void EnsureCanCommit()
    {
        EnsureUsable("commit this transaction");
    }

    public void EnsureCanRollback()
    {
        if (_state == TransactionState.Failed)
        {
            return;
        }

        EnsureUsable("rollback this transaction");
    }

    public void Committed()
    {
        _state = TransactionState.Committed;
    }

    public void RolledBack()
    {
        _state = TransactionState.RolledBack;
    }

    public void Fail(Exception error)
    {
        _error ??= error;
        _state = TransactionState.Failed;
    }

    public void FailDeferred(IResponsePipelineError pendingError)
    {
        if (_error is null)
        {
            _pendingError = pendingError;
        }

        Fail(pendingError.Exception);
    }

    private void EnsureUsable(string action)
    {
        _pendingError?.EnsureThrown();

        switch (_state)
        {
            case TransactionState.Failed:
                throw new TransactionTerminatedException(_error!);
            case TransactionState.Committed:
                throw new TransactionClosedException($"Cannot {action}, because it has already been committed.");
            case TransactionState.RolledBack:
                throw new TransactionClosedException($"Cannot {action}, because it has already been rolled back.");
        }
    }
}
