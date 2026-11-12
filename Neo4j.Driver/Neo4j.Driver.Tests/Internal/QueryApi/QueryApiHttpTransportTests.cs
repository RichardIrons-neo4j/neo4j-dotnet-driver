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
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Moq;
using Neo4j.Driver.Internal;
using Neo4j.Driver.Internal.QueryApi;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

public class QueryApiHttpTransportTests
{
    private static readonly Uri PlainUri = new("http://localhost:7474");
    private readonly Mock<ILogger> _logger = new();

    private QueryApiHttpTransport CreateTransport(Uri? uri = null, Action<ConfigBuilder>? config = null)
    {
        return new QueryApiHttpTransport(_logger.Object, TestDriverContext.With(uri ?? PlainUri, config: config));
    }

    private static CancellationTokenSource TimeLimit()
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        source.CancelAfter(TimeSpan.FromSeconds(15));
        return source;
    }

    private static Uri LocalUri(TcpListener listener)
    {
        return new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
    }

    private static Uri UnusedLocalUri()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return LocalUri(listener);
    }

    [Fact]
    public async Task SendAsync_WhenTheResponseIsCutOff_ThrowsServiceUnavailable()
    {
        await using var server = LoopbackHttpServer.HttpCuttingOffResponses();
        await using var transport = CreateTransport(server.Uri);
        using var cancellation = TimeLimit();

        var act = () => transport.SendAsync(new HttpRequestMessage(HttpMethod.Get, server.Uri), cancellation.Token);

        (await act.Should().ThrowAsync<ServiceUnavailableException>())
            .Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task SendAsync_WhenTheConnectionIsRefused_ThrowsServiceUnavailable()
    {
        var uri = UnusedLocalUri();
        await using var transport = CreateTransport(uri);
        using var cancellation = TimeLimit();

        var act = () => transport.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellation.Token);

        await act.Should().ThrowAsync<ServiceUnavailableException>();
    }

    [Fact]
    public async Task SendAsync_WhenTheCallerCancels_ThrowsOperationCanceled()
    {
        using var silentServer = new TcpListener(IPAddress.Loopback, 0);
        silentServer.Start();
        var uri = LocalUri(silentServer);
        await using var transport = CreateTransport(uri);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var act = () => transport.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var transport = CreateTransport();
        await transport.DisposeAsync();

        var act = () => transport.SendAsync(new HttpRequestMessage(HttpMethod.Post, "http://localhost/"));

        var error = await act.Should().ThrowAsync<ObjectDisposedException>();
        error.Which.Message.Should()
            .StartWith("Failed to acquire a new connection as the driver has already been disposed.");
    }

    [Fact]
    public async Task Handler_AppliesTheConnectionSettings_FromTheDriverConfig()
    {
        await using var transport = CreateTransport(
            config: builder => builder
                .WithConnectionTimeout(TimeSpan.FromSeconds(7))
                .WithMaxConnectionPoolSize(11)
                .WithConnectionIdleTimeout(TimeSpan.FromMinutes(3))
                .WithMaxConnectionLifetime(TimeSpan.FromMinutes(20)));

        var handler = transport.Handler;

        using var scope = new AssertionScope();
        handler.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(7));
        handler.MaxConnectionsPerServer.Should().Be(11);
        handler.PooledConnectionIdleTimeout.Should().Be(TimeSpan.FromMinutes(3));
        handler.PooledConnectionLifetime.Should().Be(TimeSpan.FromMinutes(20));
    }

    [Fact]
    public async Task Handler_TreatsAnInfinitePoolSizeAsUnlimited()
    {
        await using var transport = CreateTransport(config: builder => builder.WithMaxConnectionPoolSize(Config.Infinite));

        var maxConnections = transport.Handler.MaxConnectionsPerServer;

        maxConnections.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task Client_HasNoRequestTimeout_SoTheServerTimeoutAndTheCallersTokenGovern()
    {
        await using var transport = CreateTransport();

        var timeout = transport.Client.Timeout;

        timeout.Should().Be(Timeout.InfiniteTimeSpan);
    }

    [Fact]
    public async Task SendAsync_SendsTheDriversUserAgent_NotTheConfiguredOne()
    {
        await using var server = LoopbackHttpServer.Http();
        await using var transport = CreateTransport(server.Uri, builder => builder.WithUserAgent("my-custom-agent"));
        using var cancellation = TimeLimit();

        using var response = await transport.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, server.Uri),
            cancellation.Token);
        var received = await server.ReceivedRequestAsync();

        received.Headers.GetValueOrDefault("User-Agent").Should().Be(Config.DefaultUserAgent);
    }

    [Fact]
    public async Task SendAsync_OverHttps_RejectsAnUntrustedServer_WhenNoTrustManagerIsConfigured()
    {
        await using var server = LoopbackHttpServer.Https();
        await using var transport = CreateTransport(server.Uri);
        using var cancellation = TimeLimit();

        var act = () => transport.SendAsync(new HttpRequestMessage(HttpMethod.Get, server.Uri), cancellation.Token);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SendAsync_OverHttps_ConsultsTheConfiguredTrustManager()
    {
        var trustManager = new RecordingTrustManager();
        await using var server = LoopbackHttpServer.Https();
        await using var transport = CreateTransport(server.Uri, builder => builder.WithTrustManager(trustManager));
        using var cancellation = TimeLimit();

        using var response = await transport.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, server.Uri),
            cancellation.Token);

        using var scope = new AssertionScope();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        trustManager.Subjects.Should().Equal(server.Certificate!.Subject);
    }

    [Fact]
    public async Task SendAsync_OverHttps_PresentsTheCertificateFromTheClientCertificateProvider()
    {
        using var clientCertificate = LoopbackHttpServer.SelfSignedCertificate("CN=client");
        var provider = new Mock<IClientCertificateProvider>();
        provider.Setup(x => x.GetCertificateAsync())
            .Returns(new ValueTask<X509Certificate>(clientCertificate));

        await using var server = LoopbackHttpServer.Https(requireClientCertificate: true);
        await using var transport = CreateTransport(
            server.Uri,
            builder => builder
                .WithTrustManager(new RecordingTrustManager())
                .WithClientCertificateProvider(provider.Object));
        using var cancellation = TimeLimit();

        using var response = await transport.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, server.Uri),
            cancellation.Token);
        var received = await server.ReceivedRequestAsync();

        received.ClientCertificate.Should().NotBeNull();
        received.ClientCertificate!.Subject.Should().Be("CN=client");
    }

    private class RecordingTrustManager : TrustManager
    {
        public List<string> Subjects { get; } = [];

        public override bool ValidateServerCertificate(
            Uri uri,
            X509Certificate2 certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            Subjects.Add(certificate.Subject);
            return true;
        }
    }
}
