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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver.Internal.Auth;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

internal record LoopbackRequest(IReadOnlyDictionary<string, string> Headers, X509Certificate? ClientCertificate);

internal class LoopbackHttpServer : IAsyncDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly TaskCompletionSource<LoopbackRequest> _firstRequest =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly bool _cutsOffResponses;
    private readonly bool _requireClientCertificate;
    private readonly Task _serving;

    private LoopbackHttpServer(X509Certificate2? certificate, bool requireClientCertificate, bool cutsOffResponses)
    {
        Certificate = certificate;
        _requireClientCertificate = requireClientCertificate;
        _cutsOffResponses = cutsOffResponses;
        _listener.Start();

        var scheme = certificate is null ? "http" : "https";
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Uri = new Uri($"{scheme}://127.0.0.1:{port}/");
        _serving = ServeAsync();
    }

    public Uri Uri { get; }

    public X509Certificate2? Certificate { get; }

    public static LoopbackHttpServer Http()
    {
        return new LoopbackHttpServer(null, false, false);
    }

    public static LoopbackHttpServer HttpCuttingOffResponses()
    {
        return new LoopbackHttpServer(null, false, true);
    }

    public static LoopbackHttpServer Https(bool requireClientCertificate = false)
    {
        return new LoopbackHttpServer(SelfSignedCertificate("CN=localhost"), requireClientCertificate, false);
    }

    public static X509Certificate2 SelfSignedCertificate(string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        var pfx = certificate.Export(X509ContentType.Pfx);
        return new InternalX509CertificateLoader().LoadCertificate(pfx, null, X509KeyStorageFlags.DefaultKeySet);
    }

    public Task<LoopbackRequest> ReceivedRequestAsync()
    {
        return _firstRequest.Task;
    }

    public async ValueTask DisposeAsync()
    {
        _stopping.Cancel();
        _listener.Stop();
        await _serving.ConfigureAwait(false);
        Certificate?.Dispose();
        _stopping.Dispose();
    }

    private async Task ServeAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            try
            {
                var socket = await _listener.AcceptSocketAsync(_stopping.Token).ConfigureAwait(false);
                await HandleAsync(socket).ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException or AuthenticationException or SocketException
                                          or ObjectDisposedException or OperationCanceledException)
            {
                if (_stopping.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private async Task HandleAsync(Socket socket)
    {
        await using var network = new NetworkStream(socket, ownsSocket: true);
        if (Certificate is null)
        {
            await RespondAsync(network, null).ConfigureAwait(false);
            return;
        }

        await using var tls = new SslStream(network);
        await tls.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions
                {
                    ServerCertificate = Certificate,
                    ClientCertificateRequired = _requireClientCertificate,
                    RemoteCertificateValidationCallback = (_, _, _, _) => true
                },
                _stopping.Token)
            .ConfigureAwait(false);

        await RespondAsync(tls, tls.RemoteCertificate).ConfigureAwait(false);
    }

    private async Task RespondAsync(Stream stream, X509Certificate? clientCertificate)
    {
        var headers = await ReadHeadersAsync(stream).ConfigureAwait(false);
        _firstRequest.TrySetResult(new LoopbackRequest(headers, clientCertificate));

        var response = Encoding.ASCII.GetBytes(
            _cutsOffResponses
                ? "HTTP/1.1 200 OK\r\nContent-Length: 100\r\nConnection: close\r\n\r\n{\"data\""
                : "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response, _stopping.Token).ConfigureAwait(false);
        await stream.FlushAsync(_stopping.Token).ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, string>> ReadHeadersAsync(Stream stream)
    {
        var received = new List<byte>();
        var buffer = new byte[1024];
        var head = string.Empty;

        while (!head.Contains("\r\n\r\n"))
        {
            var read = await stream.ReadAsync(buffer, _stopping.Token).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            received.AddRange(buffer.Take(read));
            head = Encoding.ASCII.GetString(received.ToArray());
        }

        return head.Split("\r\n")
            .Skip(1)
            .Where(line => line.Contains(':'))
            .Select(line => line.Split(':', 2))
            .ToDictionary(pair => pair[0].Trim(), pair => pair[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
