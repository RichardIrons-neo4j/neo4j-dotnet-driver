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
using System.Threading.Tasks;

namespace Neo4j.Driver.Internal.QueryApi;

internal class QueryApiSession : IInternalAsyncSession
{
    private readonly IAutoCommitRunner _autoCommitRunner;
    private readonly IBookmarkTracker _bookmarkTracker;
    private readonly Func<TransactionConfigBuilder> _configBuilderFactory;
    private readonly ILogger _logger;
    private readonly IAsyncRetryLogic _retryLogic;
    private readonly IQueryApiTransactionFactory _transactionFactory;

    private bool _closed;
    private IResultCursor? _lastCursor;
    private IInternalAsyncTransaction? _openTransaction;

    public QueryApiSession(
        SessionConfig sessionConfig,
        IAutoCommitRunner autoCommitRunner,
        IQueryApiTransactionFactory transactionFactory,
        IAsyncRetryLogic retryLogic,
        IBookmarkTracker bookmarkTracker,
        Func<TransactionConfigBuilder> configBuilderFactory,
        ILogger logger)
    {
        SessionConfig = sessionConfig;
        _autoCommitRunner = autoCommitRunner;
        _transactionFactory = transactionFactory;
        _retryLogic = retryLogic;
        _bookmarkTracker = bookmarkTracker;
        _configBuilderFactory = configBuilderFactory;
        _logger = logger;
    }

    public Bookmarks LastBookmarks => _bookmarkTracker.CurrentBookmarks;

    public SessionConfig SessionConfig { get; }

    public event AsyncEventHandler? Disposed;

    public async Task<IResultCursor> RunAsync(
        Query query,
        Action<TransactionConfigBuilder> action,
        bool disposeUnconsumedSessionResult)
    {
        await EnsureCanRunMoreAsync(disposeUnconsumedSessionResult).ConfigureAwait(false);
        _logger.LogDebug("Session auto-commit: {query}", query.Text);
        var cursor = await _autoCommitRunner.RunAsync(query, BuildTransactionConfig(action)).ConfigureAwait(false);
        _lastCursor = cursor;
        return cursor;
    }

    public Task<IResultCursor> RunAsync(Query query, Action<TransactionConfigBuilder> action)
    {
        return RunAsync(query, action, true);
    }

    public Task<IResultCursor> RunAsync(Query query)
    {
        return RunAsync(query, null!);
    }

    public Task<IResultCursor> RunAsync(string query, Action<TransactionConfigBuilder> action)
    {
        return RunAsync(new Query(query), action);
    }

    public Task<IResultCursor> RunAsync(
        string query,
        IDictionary<string, object> parameters,
        Action<TransactionConfigBuilder> action)
    {
        return RunAsync(new Query(query, parameters), action);
    }

    public Task<IResultCursor> RunAsync(string query)
    {
        return RunAsync(new Query(query));
    }

    public Task<IResultCursor> RunAsync(string query, object parameters)
    {
        return RunAsync(new Query(query, parameters));
    }

    public Task<IResultCursor> RunAsync(string query, IDictionary<string, object> parameters)
    {
        return RunAsync(new Query(query, parameters));
    }

    public Task<IAsyncTransaction> BeginTransactionAsync(
        Action<TransactionConfigBuilder> action,
        bool disposeUnconsumedSessionResult)
    {
        return BeginTransactionAsync(SessionConfig.DefaultAccessMode, action, disposeUnconsumedSessionResult);
    }

    public async Task<IAsyncTransaction> BeginTransactionAsync(
        AccessMode mode,
        Action<TransactionConfigBuilder> action,
        bool disposeUnconsumedSessionResult)
    {
        await EnsureCanRunMoreAsync(disposeUnconsumedSessionResult).ConfigureAwait(false);
        _logger.LogDebug("Session beginning {mode} transaction", mode);
        var tx = await _transactionFactory
            .BeginTransactionAsync(new TransactionOptions(mode, BuildTransactionConfig(action)))
            .ConfigureAwait(false);
        _openTransaction = tx;
        return tx;
    }

    private TransactionConfig BuildTransactionConfig(Action<TransactionConfigBuilder>? action)
    {
        if (action is null)
        {
            return TransactionConfig.Default;
        }

        var builder = _configBuilderFactory();
        action(builder);
        return builder.Build();
    }

    private async Task EnsureCanRunMoreAsync(bool disposeUnconsumedSessionResult)
    {
        EnsureSessionIsOpen();
        EnsureNoOpenTransaction();
        if (disposeUnconsumedSessionResult)
        {
            await ConsumeLastCursorAsync().ConfigureAwait(false);
        }
        else
        {
            EnsureLastCursorIsClosed();
        }
    }

    private void EnsureSessionIsOpen()
    {
        if (_closed)
        {
            throw new ClientException(
                "Cannot run more queries in the current session as it has already been disposed. " +
                "Make sure that you do not have a bad reference to a disposed session " +
                "and retry your query in another new session.");
        }
    }

    private void EnsureNoOpenTransaction()
    {
        if (_openTransaction is { IsOpen: true })
        {
            throw new TransactionNestingException(
                "Attempting to nest transactions. A session can only have a single " +
                "transaction open at a time. Commit or rollback the previous transaction before opening the next.");
        }
    }

    private void EnsureLastCursorIsClosed()
    {
        if (_lastCursor is { IsOpen: true })
        {
            throw new ClientException(
                "Please consume the current query result before running " +
                "more queries/transaction in the same session.");
        }
    }

    private async Task ConsumeLastCursorAsync()
    {
        var cursor = _lastCursor;
        _lastCursor = null;
        if (cursor is not null)
        {
            await cursor.ConsumeAsync().ConfigureAwait(false);
        }
    }

    public Task<IAsyncTransaction> BeginTransactionAsync()
    {
        return BeginTransactionAsync(null!);
    }

    public Task<IAsyncTransaction> BeginTransactionAsync(Action<TransactionConfigBuilder> action)
    {
        return BeginTransactionAsync(action, true);
    }

    public Task<IAsyncTransaction> BeginTransactionAsync(AccessMode mode)
    {
        return BeginTransactionAsync(mode, null!);
    }

    public Task<IAsyncTransaction> BeginTransactionAsync(AccessMode mode, Action<TransactionConfigBuilder> action)
    {
        return BeginTransactionAsync(mode, action, true);
    }

    public Task<TResult> ExecuteReadAsync<TResult>(
        Func<IAsyncQueryRunner, Task<TResult>> txFuncAsync,
        Action<TransactionConfigBuilder>? action = null)
    {
        return RunTransactionWithRetryAsync(AccessMode.Read, txFuncAsync, BuildTransactionConfig(action));
    }

    public Task ExecuteReadAsync(
        Func<IAsyncQueryRunner, Task> txFuncAsync,
        Action<TransactionConfigBuilder>? action = null)
    {
        return RunTransactionWithRetryAsync(AccessMode.Read, Adapt(txFuncAsync), BuildTransactionConfig(action));
    }

    public Task<TResult> ExecuteWriteAsync<TResult>(
        Func<IAsyncQueryRunner, Task<TResult>> txFuncAsync,
        Action<TransactionConfigBuilder>? action = null)
    {
        return RunTransactionWithRetryAsync(AccessMode.Write, txFuncAsync, BuildTransactionConfig(action));
    }

    public Task ExecuteWriteAsync(
        Func<IAsyncQueryRunner, Task> txFuncAsync,
        Action<TransactionConfigBuilder>? action = null)
    {
        return RunTransactionWithRetryAsync(AccessMode.Write, Adapt(txFuncAsync), BuildTransactionConfig(action));
    }

    public Task<EagerResult<T>> PipelinedExecuteReadAsync<T>(
        Func<IAsyncQueryRunner, Task<EagerResult<T>>> func,
        TransactionConfig config)
    {
        return RunTransactionWithRetryAsync(AccessMode.Read, func, config);
    }

    public Task<EagerResult<T>> PipelinedExecuteWriteAsync<T>(
        Func<IAsyncQueryRunner, Task<EagerResult<T>>> func,
        TransactionConfig config)
    {
        return RunTransactionWithRetryAsync(AccessMode.Write, func, config);
    }

    private static Func<IAsyncQueryRunner, Task<bool>> Adapt(Func<IAsyncQueryRunner, Task> txFuncAsync)
    {
        return Adapter;

        async Task<bool> Adapter(IAsyncQueryRunner runner)
        {
            await txFuncAsync(runner).ConfigureAwait(false);
            return true;
        }
    }

    public Task CloseAsync() => DisposeAsync().AsTask();

    public async ValueTask DisposeAsync()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        try
        {
            await DisposeOpenTransactionAsync().ConfigureAwait(false);
            await ConsumeLastCursorAsync().ConfigureAwait(false);
        }
        finally
        {
            await Disposed.FireAsync().ConfigureAwait(false);
        }
    }

    private async Task DisposeOpenTransactionAsync()
    {
        if (_openTransaction is not { IsOpen: true } transaction)
        {
            return;
        }

        try
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            throw new ClientException(
                (e as Neo4jException)?.Code,
                $"Error when disposing unclosed transaction in session: {e.Message}",
                e);
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private Task<TResult> RunTransactionWithRetryAsync<TResult>(
        AccessMode mode,
        Func<IAsyncQueryRunner, Task<TResult>> txFuncAsync,
        TransactionConfig config)
    {
        return _retryLogic.RetryAsync(() => RunTransactionAsync(new TransactionOptions(mode, config), txFuncAsync));
    }

    private async Task<TResult> RunTransactionAsync<TResult>(
        TransactionOptions options,
        Func<IAsyncQueryRunner, Task<TResult>> txFuncAsync)
    {
        await EnsureCanRunMoreAsync(true).ConfigureAwait(false);
        var tx = await _transactionFactory
            .BeginTransactionAsync(options)
            .ConfigureAwait(false);

        _openTransaction = tx;

        try
        {
            _logger.LogDebug("Session beginning work", options.AccessMode);
            var result = await txFuncAsync(tx).ConfigureAwait(false);
            await tx.CommitAsync().ConfigureAwait(false);
            return result;
        }
        catch
        {
            try
            {
                await tx.RollbackIfOpenAsync().ConfigureAwait(false);
            }
            catch
            {
                /* best-effort; don't mask the original error */
            }

            throw;
        }
    }
}
