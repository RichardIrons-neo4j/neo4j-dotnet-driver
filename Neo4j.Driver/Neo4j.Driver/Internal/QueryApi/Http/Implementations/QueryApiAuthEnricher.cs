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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver.Internal.Auth;

namespace Neo4j.Driver.Internal.QueryApi;

internal class QueryApiAuthEnricher : IHttpRequestEnricher
{
    internal static readonly HttpRequestOptionsKey<IAuthToken> AuthTokenKey = new("neo4j.auth-token");

    private readonly IQueryApiTransactionContextTracker _contextTracker;
    private readonly IBase64Encoder _encoder;
    private readonly ISessionContext _sessionContext;

    public QueryApiAuthEnricher(
        ISessionContext sessionContext,
        IQueryApiTransactionContextTracker contextTracker,
        IBase64Encoder encoder)
    {
        _sessionContext = sessionContext;
        _contextTracker = contextTracker;
        _encoder = encoder;
    }

    private AuthenticationHeaderValue BuildBasicHeader(AuthToken authToken)
    {
        var principal = authToken.Principal ?? string.Empty;
        var credentials = authToken.Content.GetValueOrDefault(AuthToken.CredentialsKey) as string  ?? string.Empty;

        if (principal.Contains(':'))
        {
            throw new NotSupportedException(
                "Basic auth principal (username) must not contain a colon (':') — " +
                "the Query API cannot distinguish it from the credentials separator.");
        }

        return authToken.Realm is null 
            ? new AuthenticationHeaderValue("Basic", _encoder.Encode($"{principal}:{credentials}")) 
            : throw new NotSupportedException("Basic auth realm is not supported by the Query API.");
    }

    private AuthenticationHeaderValue BuildBearerHeader(AuthToken authToken)
    {
        var token = authToken.Content.GetValueOrDefault(AuthToken.CredentialsKey) as string  ?? string.Empty;
        return new AuthenticationHeaderValue("Bearer", token);
    }

    public async ValueTask Enrich(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var returnedToken = _contextTracker.Context?.AuthToken
            ?? await _sessionContext.GetAuthTokenAsync(cancellationToken).ConfigureAwait(false);

        request.Options.Set(AuthTokenKey, returnedToken);
        if (returnedToken is not AuthToken authToken)
        {
            throw new InvalidOperationException("Unsupported auth token: " + returnedToken.GetType().Name);
        }

        request.Headers.Authorization = authToken.Scheme switch
        {
            "basic" => BuildBasicHeader(authToken),
            "bearer" => BuildBearerHeader(authToken),
            "none" => null,
            _  => throw new NotSupportedException($"Auth scheme '{authToken.Scheme}' is not supported by the Query API.")
        };
    }
}
