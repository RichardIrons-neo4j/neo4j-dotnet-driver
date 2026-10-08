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

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Neo4j.Driver.Internal.QueryApi;

internal class QueryApiClient : IQueryApiClient
{
    private readonly IQueryApiFailureReader _failureReader;
    private readonly IQueryApiHttpTransport _httpTransport;
    private readonly IJsonDeserializer _jsonDeserializer;

    public QueryApiClient(
        IQueryApiHttpTransport httpTransport,
        IJsonDeserializer jsonDeserializer,
        IQueryApiFailureReader failureReader)
    {
        _httpTransport = httpTransport;
        _jsonDeserializer = jsonDeserializer;
        _failureReader = failureReader;
    }

    public async Task<QueryApiResult<TBody>> ExecuteAsync<TBody>(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
        where TBody : QueryApiResponse
    {
        using var response = await _httpTransport.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var headers = response.Headers;

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var body = await _jsonDeserializer
            .DeserializeAsync<TBody>(json, cancellationToken)
            .ConfigureAwait(false);

        var failure = _failureReader.Read(response, json, body?.Errors);
        if (failure is not null)
        {
            throw failure;
        }

        return new QueryApiResult<TBody>(body!, headers);
    }
}
