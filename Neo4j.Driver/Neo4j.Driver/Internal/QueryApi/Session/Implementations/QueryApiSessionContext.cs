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

using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Neo4j.Driver.Internal.QueryApi;

internal class QueryApiSessionContext : ISessionContext
{
    private static readonly Regex AcceptedDatabaseName = new(@"\A[a-zA-Z0-9.-]{3,}\z");

    private readonly IAuthTokenManager _authTokenManager;
    private readonly SessionConfig _sessionConfig;

    public QueryApiSessionContext(SessionConfig sessionConfig, IAuthTokenManager authTokenManager)
    {
        _sessionConfig = sessionConfig;
        _authTokenManager = authTokenManager;
    }

    public string Database
    {
        get
        {
            var database = _sessionConfig.Database;
            if (database is null)
            {
                throw new ClientException(
                    "Query API: a database name is required, because the Query API has no home database to default to.");
            }

            if (!AcceptedDatabaseName.IsMatch(database))
            {
                var shownName = database.Any(char.IsControl) ? string.Empty : $" '{database}'";
                throw new ClientException(
                    $"Query API: invalid database name{shownName}. " +
                    "A database name must be at least 3 characters long and contain only letters, digits, '.' and '-'.");
            }

            return database;
        }
    }

    public string? ImpersonatedUser => _sessionConfig.ImpersonatedUser;
    public AccessMode AccessMode => _sessionConfig.DefaultAccessMode;

    public ValueTask<IAuthToken> GetAuthTokenAsync(CancellationToken cancellationToken = default)
    {
        return _sessionConfig.AuthToken is not null
            ? ValueTask.FromResult(_sessionConfig.AuthToken)
            : _authTokenManager.GetTokenAsync(cancellationToken);
    }

    public ValueTask<bool> HandleSecurityExceptionAsync(
        IAuthToken token,
        SecurityException exception,
        CancellationToken cancellationToken = default)
    {
        // Session-level auth tokens are static — no refresh possible.
        return _sessionConfig.AuthToken is not null
            ? ValueTask.FromResult(false)
            : _authTokenManager.HandleSecurityExceptionAsync(token, exception, cancellationToken);
    }
}
