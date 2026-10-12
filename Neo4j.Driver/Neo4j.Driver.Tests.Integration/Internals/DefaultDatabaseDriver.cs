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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver.Internal;
using Neo4j.Driver.Internal.Routing;

namespace Neo4j.Driver.IntegrationTests.Internals;

internal class DefaultDatabaseDriver : IInternalDriver
{
    private readonly string _database;
    private readonly IInternalDriver _driver;

    public DefaultDatabaseDriver(IInternalDriver driver, string database)
    {
        _driver = driver;
        _database = database;
    }

    public Config Config => _driver.Config;

    public bool Encrypted => _driver.Encrypted;

    public IAsyncSession AsyncSession()
    {
        return _driver.AsyncSession(WithDefaultDatabase(null));
    }

    public IAsyncSession AsyncSession(Action<SessionConfigBuilder> action)
    {
        return _driver.AsyncSession(WithDefaultDatabase(action));
    }

    public IInternalAsyncSession Session(Action<SessionConfigBuilder> action, bool reactive)
    {
        return _driver.Session(WithDefaultDatabase(action), reactive);
    }

    public Task<IServerInfo> GetServerInfoAsync() => _driver.GetServerInfoAsync();

    public Task<bool> TryVerifyConnectivityAsync() => _driver.TryVerifyConnectivityAsync();

    public Task VerifyConnectivityAsync() => _driver.VerifyConnectivityAsync();

    public Task<bool> SupportsMultiDbAsync() => _driver.SupportsMultiDbAsync();

    public Task<bool> SupportsSessionAuthAsync() => _driver.SupportsSessionAuthAsync();

    public IExecutableQuery<IRecord, IRecord> ExecutableQuery(string cypher) => _driver.ExecutableQuery(cypher);

    public Task<bool> VerifyAuthenticationAsync(IAuthToken authToken) => _driver.VerifyAuthenticationAsync(authToken);

    public IBookmarkManager GetExecutableQueryBookmarkManager() => _driver.GetExecutableQueryBookmarkManager();

    public IRoutingTable GetRoutingTable(string database) => _driver.GetRoutingTable(database);

    public Task<IRoutingTable> ForceRoutingTableUpdateAsync(string database, Bookmarks bookmarks)
    {
        return _driver.ForceRoutingTableUpdateAsync(database, bookmarks);
    }

    public Task<EagerResult<TResult>> ExecuteQueryAsync<TResult>(
        Query query,
        Func<IAsyncEnumerable<IRecord>, Task<TResult>> streamProcessor,
        QueryConfig config = null,
        CancellationToken cancellationToken = default)
    {
        return _driver.ExecuteQueryAsync(query, streamProcessor, config, cancellationToken);
    }

    public Task<ExecutionSummary> GetRowsAsync(
        Query query,
        QueryConfig config,
        Action<IRecord> streamProcessor,
        CancellationToken cancellationToken)
    {
        return _driver.GetRowsAsync(query, config, streamProcessor, cancellationToken);
    }

    public void Dispose() => _driver.Dispose();

    public ValueTask DisposeAsync() => _driver.DisposeAsync();

    private Action<SessionConfigBuilder> WithDefaultDatabase(Action<SessionConfigBuilder> action)
    {
        return builder =>
        {
            builder.WithDatabase(_database);
            action?.Invoke(builder);
        };
    }
}
