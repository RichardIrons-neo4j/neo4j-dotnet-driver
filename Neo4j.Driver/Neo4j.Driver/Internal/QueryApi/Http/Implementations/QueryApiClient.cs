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
using System.Net.Http.Headers;
using System.Text.Json;
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
        var (body, headers, json) = await SendAndReadAsync<TBody>(request, cancellationToken).ConfigureAwait(false);

        return new QueryApiResult<TBody>(
            body ?? throw new ProtocolException($"Unable to parse the Query API response: {json}"),
            headers);
    }

    public async Task ExecuteAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        await SendAndReadAsync<ErrorsOnlyBody>(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(TBody? Body, HttpResponseHeaders Headers, string Json)> SendAndReadAsync<TBody>(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        where TBody : QueryApiResponse
    {
        using var response = await _httpTransport.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var body = await TryDeserializeAsync<TBody>(json, cancellationToken).ConfigureAwait(false);

        var failure = _failureReader.Read(response, json, body?.Errors);
        if (failure is not null)
        {
            throw failure;
        }

        return (body, response.Headers, json);
    }

    private async Task<TBody?> TryDeserializeAsync<TBody>(string json, CancellationToken cancellationToken)
    {
        try
        {
            return await _jsonDeserializer.DeserializeAsync<TBody>(json, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    internal record ErrorsOnlyBody : QueryApiResponse;
}
