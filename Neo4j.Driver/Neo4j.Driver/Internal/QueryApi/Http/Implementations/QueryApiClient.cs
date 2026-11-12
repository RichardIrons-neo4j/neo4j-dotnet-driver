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

using System.Net;
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
    private readonly ISecurityExceptionNotifier _securityExceptionNotifier;
    private readonly IServerDiscovery _serverDiscovery;

    public QueryApiClient(
        IServerDiscovery serverDiscovery,
        IQueryApiHttpTransport httpTransport,
        IJsonDeserializer jsonDeserializer,
        IQueryApiFailureReader failureReader,
        ISecurityExceptionNotifier securityExceptionNotifier)
    {
        _serverDiscovery = serverDiscovery;
        _httpTransport = httpTransport;
        _jsonDeserializer = jsonDeserializer;
        _failureReader = failureReader;
        _securityExceptionNotifier = securityExceptionNotifier;
    }

    public async Task<QueryApiResult<TBody>> ExecuteAsync<TBody>(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
        where TBody : QueryApiResponse
    {
        var received = await SendAndReadAsync<TBody>(request, cancellationToken).ConfigureAwait(false);

        if (received.Failure is not null)
        {
            throw received.Failure;
        }

        return new QueryApiResult<TBody>(received.Body ?? throw UnparseableResponse(received.Json), received.Headers);
    }

    public async Task ExecuteAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var received = await SendAndReadAsync<ErrorsOnlyBody>(request, cancellationToken).ConfigureAwait(false);

        if (received.Failure is not null)
        {
            throw received.Failure;
        }
    }

    public async Task<QueryApiQueryResult> ExecuteQueryAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        var received = await SendAndReadAsync<QueryApiResultBody>(request, cancellationToken).ConfigureAwait(false);

        if (received.Failure is not null && IsRequestFailure(received))
        {
            throw received.Failure;
        }

        return new QueryApiQueryResult(
            received.Body ?? throw UnparseableResponse(received.Json),
            received.Headers,
            received.Failure);
    }

    private bool IsRequestFailure(ReceivedResponse<QueryApiResultBody> received)
    {
        return received.Body is null || received.Status == HttpStatusCode.Unauthorized;
    }

    private async Task<ReceivedResponse<TBody>> SendAndReadAsync<TBody>(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        where TBody : QueryApiResponse
    {
        await _serverDiscovery.EnsureDiscoveredAsync(cancellationToken).ConfigureAwait(false);
        using var response = await _httpTransport.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var body = await TryDeserializeAsync<TBody>(json, cancellationToken).ConfigureAwait(false);
        var failure = _failureReader.Read(response, json, body?.Errors);
        if (failure is SecurityException security && request.Options.TryGetValue(QueryApiAuthEnricher.AuthTokenKey, out var token))
        {
            await _securityExceptionNotifier.NotifyAsync(token, security, cancellationToken).ConfigureAwait(false);
        }

        return new ReceivedResponse<TBody>(body, response.Headers, json, failure, response.StatusCode);
    }

    private ProtocolException UnparseableResponse(string json)
    {
        return new ProtocolException($"Unable to parse the Query API response: {json}");
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

    private record ReceivedResponse<TBody>(
        TBody? Body,
        HttpResponseHeaders Headers,
        string Json,
        Neo4jException? Failure,
        HttpStatusCode Status)
        where TBody : QueryApiResponse;
}
