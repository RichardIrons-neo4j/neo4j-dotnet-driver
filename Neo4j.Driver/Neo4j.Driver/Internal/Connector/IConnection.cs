﻿// Copyright (c) "Neo4j"
// Neo4j Sweden AB [http://neo4j.com]
// 
// This file is part of Neo4j.
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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver.Auth;
using Neo4j.Driver.Internal.MessageHandling;
using Neo4j.Driver.Internal.Messaging;
using Neo4j.Driver.Internal.Util;

namespace Neo4j.Driver.Internal.Connector;

internal interface IConnection : IConnectionDetails, IConnectionRunner
{
    IBoltProtocol BoltProtocol { get; }
    bool ReAuthorizationRequired { get; set; }

    void ConfigureMode(AccessMode? mode);
    void Configure(string database, AccessMode? mode);

    Task InitAsync(
        INotificationsConfig notificationsConfig,
        SessionConfig sessionConfig = null,
        CancellationToken cancellationToken = default);

    Task ReAuthAsync(IAuthToken newAuthToken, CancellationToken cancellationToken = default);

    Task NotifyTokenExpiredAsync();

    // send all and receive all
    Task SyncAsync();

    // send all
    Task SendAsync();

    // receive one
    Task ReceiveOneAsync();

    Task EnqueueAsync(IRequestMessage message, IResponseHandler handler);

    void ClearQueueAsync();

    // Enqueue a reset message
    Task ResetAsync();

    /// <summary>Close and release related resources</summary>
    Task DestroyAsync();

    /// <summary>Close connection</summary>
    Task CloseAsync();

    void UpdateId(string newConnId);

    void UpdateVersion(ServerVersion newVersion);

    void SetReadTimeoutInSeconds(int seconds);

    void SetUseUtcEncodedDateTime();

    public SessionConfig SessionConfig { get; }
}

internal interface IConnectionRunner
{
    Task LoginAsync(
        string userAgent,
        IAuthToken authToken,
        INotificationsConfig notificationsConfig);

    Task LogoutAsync();

    Task<IReadOnlyDictionary<string, object>> GetRoutingTableAsync(
        string database,
        SessionConfig sessionConfig,
        Bookmarks bookmarks);

    Task<IResultCursor> RunInAutoCommitTransactionAsync(
        AutoCommitParams autoCommitParams,
        INotificationsConfig notificationsConfig);

    Task BeginTransactionAsync(
        string database,
        Bookmarks bookmarks,
        TransactionConfig config,
        SessionConfig sessionConfig,
        INotificationsConfig notificationsConfig);

    Task<IResultCursor> RunInExplicitTransactionAsync(Query query, bool reactive, long fetchSize);
    Task CommitTransactionAsync(IBookmarksTracker bookmarksTracker);
    Task RollbackTransactionAsync();
}

internal interface IConnectionDetails
{
    bool IsOpen { get; }
    string Database { get; }
    AccessMode? Mode { get; }
    IServerInfo Server { get; }
    IDictionary<string, string> RoutingContext { get; }
    BoltProtocolVersion Version { get; }
    bool UtcEncodedDateTime { get; }
    IAuthToken AuthToken { get; }
}
