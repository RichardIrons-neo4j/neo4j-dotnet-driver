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
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver.Internal.Auth;

namespace Neo4j.Driver.Internal.QueryApi;

internal class QueryApiHttpTransport : IQueryApiHttpTransport
{
    private readonly HttpClient _client;
    private readonly ILogger _logger;
    private bool _disposed;

    public QueryApiHttpTransport(ILogger logger, DriverContext driverContext)
    {
        _logger = logger;
        Handler = CreateHandler(driverContext);
        _client = new HttpClient(Handler) { Timeout = Timeout.InfiniteTimeSpan };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(Config.DefaultUserAgent);
    }

    internal SocketsHttpHandler Handler { get; }

    internal HttpClient Client
    {
        get { return _client; }
    }

    private static SocketsHttpHandler CreateHandler(DriverContext driverContext)
    {
        var config = driverContext.Config;
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = config.ConnectionTimeout,
            MaxConnectionsPerServer =
                config.MaxConnectionPoolSize == Config.Infinite ? int.MaxValue : config.MaxConnectionPoolSize,
            PooledConnectionIdleTimeout = config.ConnectionIdleTimeout,
            PooledConnectionLifetime = config.MaxConnectionLifetime
        };

        if (driverContext.EncryptionManager is { UseTls: true } encryption)
        {
            handler.SslOptions = CreateSslOptions(driverContext, encryption);
        }

        return handler;
    }

    private static SslClientAuthenticationOptions CreateSslOptions(
        DriverContext driverContext,
        EncryptionManager encryption)
    {
        var config = driverContext.Config;
        var negotiator = new DefaultTlsNegotiator(config.Neo4JLogger, encryption);
        var options = new SslClientAuthenticationOptions
        {
            EnabledSslProtocols = config.TlsVersion,
            RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
                negotiator.ValidateServerCertificate(driverContext.InitialUri, certificate, chain, errors)
        };

        if (config.ClientCertificateProvider is {} provider)
        {
            options.LocalCertificateSelectionCallback = (_, _, _, _, _) =>
                provider.GetCertificateAsync().GetAwaiter().GetResult();
        }

        return options;
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(QueryApiHttpTransport),
                "Failed to acquire a new connection as the driver has already been disposed.");
        }

        _logger.LogDebug("Sending {method} request to {uri}", request.Method, request.RequestUri);
        var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "{method} {uri} returned {statusCode}", request.Method, request.RequestUri, (int)response.StatusCode);

        return response;
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
