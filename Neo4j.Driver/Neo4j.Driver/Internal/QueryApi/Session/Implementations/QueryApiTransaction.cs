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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Neo4j.Driver.Internal.QueryApi;

internal class QueryApiTransaction : IScopedTransaction
{
    private readonly ITransactionBeginner _beginner;
    private readonly ITransactionCommitter _committer;
    private readonly ITransactionLifecycle _lifecycle;
    private readonly ILogger _logger;
    private readonly ITransactionRollback _rollback;
    private readonly ITransactionRunner _runner;

    private bool _disposed;

    public QueryApiTransaction(
        ITransactionBeginner beginner,
        ITransactionRunner runner,
        ITransactionCommitter committer,
        ITransactionRollback rollback,
        ITransactionLifecycle lifecycle,
        ILogger logger)
    {
        _beginner = beginner;
        _runner = runner;
        _committer = committer;
        _rollback = rollback;
        _lifecycle = lifecycle;
        _logger = logger;
    }

    public event AsyncEventHandler? Disposed;

    public TransactionConfig TransactionConfig => TransactionConfig.Default;

    public bool IsOpen
    {
        get { return _lifecycle.GetState() == TransactionState.Active; }
    }

    public bool IsErrored(out Exception ex)
    {
        var errored = _lifecycle.TryGetError(out var error);
        ex = error!;
        return errored;
    }

    public async Task BeginAsync(TransactionOptions options, CancellationToken cancellationToken = default)
    {
        await _beginner.BeginAsync(options, cancellationToken).ConfigureAwait(false);
        _lifecycle.Activate();
    }

    public async Task CommitAsync()
    {
        _lifecycle.EnsureCanCommit();
        _logger.LogDebug("Committing transaction");
        try
        {
            await _committer.CommitAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _lifecycle.Fail(ex);
            throw;
        }

        _lifecycle.Committed();
    }

    public async Task RollbackAsync()
    {
        _lifecycle.EnsureCanRollback();
        if (_lifecycle.GetState() == TransactionState.Failed)
        {
            return;
        }

        _logger.LogDebug("Rolling back transaction");
        await SendRollbackAsync().ConfigureAwait(false);
    }

    public Task<IResultCursor> RunAsync(string query) => RunAsync(new Query(query));

    public Task<IResultCursor> RunAsync(string query, object parameters) =>
        RunAsync(new Query(query, parameters));

    public Task<IResultCursor> RunAsync(string query, IDictionary<string, object> parameters) =>
        RunAsync(new Query(query, parameters));

    public async Task<IResultCursor> RunAsync(Query query)
    {
        _lifecycle.EnsureCanRun();
        try
        {
            return await _runner.RunAsync(query).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.HasServerErrorCode())
        {
            _lifecycle.Fail(ex);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (IsOpen)
        {
            _logger.LogDebug("Disposing open transaction — rolling back");
            await SendRollbackAsync().ConfigureAwait(false);
        }

        await Disposed.FireAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private async Task SendRollbackAsync()
    {
        try
        {
            await _rollback.RollbackAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _lifecycle.Fail(ex);
            throw;
        }

        _lifecycle.RolledBack();
    }
}
