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
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Neo4j.Driver.Internal;
using Neo4j.Driver.Internal.QueryApi;
using Neo4j.Driver.Tests.Internal.Core;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

public class QueryApiClientTests
{
    private readonly AutoMocker _autoMocker = AutoMocker.ForTesting<QueryApiClient>();

    private sealed record TestBody : QueryApiResponse
    {
        public string? Value { get; init; }
    }

    private ServiceUnavailableException SetupDiscoveryFailure()
    {
        var failure = new ServiceUnavailableException("discovery failed");
        _autoMocker.GetMock<IServerDiscovery>()
            .Setup(x => x.EnsureDiscoveredAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var sent = new InvalidOperationException("the request was sent");
        _autoMocker.GetMock<IQueryApiHttpTransport>()
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(sent);

        return failure;
    }

    [Fact]
    public async Task ThrowsTheDiscoveryFailure_BeforeSendingTheRequest()
    {
        var failure = SetupDiscoveryFailure();

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var act = () => subject.ExecuteAsync<TestBody>(new HttpRequestMessage(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableException>()).Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task ExecuteAsyncWithoutABody_ThrowsTheDiscoveryFailure_BeforeSendingTheRequest()
    {
        var failure = SetupDiscoveryFailure();

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var act = () => subject.ExecuteAsync(new HttpRequestMessage(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableException>()).Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task ExecuteQueryAsync_ThrowsTheDiscoveryFailure_BeforeSendingTheRequest()
    {
        var failure = SetupDiscoveryFailure();

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var act = () => subject.ExecuteQueryAsync(new HttpRequestMessage(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableException>()).Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task ReturnsDeserializedBody_InResult()
    {
        var expected = new TestBody { Value = "hello" };

        _autoMocker.GetMock<IQueryApiHttpTransport>()
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new ByteArrayContent([]) });

        _autoMocker.GetMock<IJsonDeserializer>()
            .Setup(x => x.DeserializeAsync<TestBody>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var result = await subject.ExecuteAsync<TestBody>(
            new HttpRequestMessage(),
            TestContext.Current.CancellationToken);

        result.Body.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task CapturesResponseHeaders_InResult()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new ByteArrayContent([]) };
        response.Headers.TryAddWithoutValidation("neo4j-cluster-affinity", "shard-42");

        _autoMocker.GetMock<IQueryApiHttpTransport>()
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        _autoMocker.GetMock<IJsonDeserializer>()
            .Setup(x => x.DeserializeAsync<TestBody>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestBody());

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var result = await subject.ExecuteAsync<TestBody>(
            new HttpRequestMessage(),
            TestContext.Current.CancellationToken);

        result.ResponseHeaders.TryGetValues("neo4j-cluster-affinity", out var vals).Should().BeTrue();
        vals.Should().ContainSingle().Which.Should().Be("shard-42");
    }

    [Fact]
    public async Task PropagatesException_WhenTransportThrows()
    {
        _autoMocker.GetMock<IQueryApiHttpTransport>()
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceUnavailableException("HTTP 503"));

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var act = () => subject.ExecuteAsync<TestBody>(new HttpRequestMessage(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ServiceUnavailableException>();
    }

    [Fact]
    public async Task Throws_WhenTheFailureReaderReturnsAFailure()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new ByteArrayContent([]) };
        QueryApiErrorBody[] errors = [new QueryApiErrorBody("Neo.ClientError.General.Unknown", "error")];

        _autoMocker.GetMock<IQueryApiHttpTransport>()
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        _autoMocker.GetMock<IJsonDeserializer>()
            .Setup(x => x.DeserializeAsync<TestBody>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestBody { Errors = errors });

        var failure = new ClientException("Neo.ClientError.General.Unknown", "error");
        _autoMocker.GetMock<IQueryApiFailureReader>()
            .Setup(x => x.Read(response, "", errors))
            .Returns(failure);

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var act = () => subject.ExecuteAsync<TestBody>(new HttpRequestMessage(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ClientException>()).Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task ThrowsTheReadersFailure_WhenTheBodyIsNotJson()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>") };

        _autoMocker.GetMock<IQueryApiHttpTransport>()
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        _autoMocker.GetMock<IJsonDeserializer>()
            .Setup(x => x.DeserializeAsync<TestBody>("<html>", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonException("not json"));

        var failure = new ServiceUnavailableException("HTTP 502");
        _autoMocker.GetMock<IQueryApiFailureReader>()
            .Setup(x => x.Read(response, "<html>", null))
            .Returns(failure);

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var act = () => subject.ExecuteAsync<TestBody>(new HttpRequestMessage(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableException>()).Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task ExecuteAsyncWithoutABody_DoesNotThrow_WhenTheResponseBodyIsEmpty()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };

        _autoMocker.GetMock<IQueryApiHttpTransport>()
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        _autoMocker.GetMock<IJsonDeserializer>()
            .Setup(x => x.DeserializeAsync<QueryApiClient.ErrorsOnlyBody>("", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonException("empty"));

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var act = () => subject.ExecuteAsync(new HttpRequestMessage(), TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsyncWithoutABody_ThrowsTheReadersFailure()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
        QueryApiErrorBody[] errors = [new QueryApiErrorBody("Neo.ClientError.Request.Invalid", "no such tx")];

        _autoMocker.GetMock<IQueryApiHttpTransport>()
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        _autoMocker.GetMock<IJsonDeserializer>()
            .Setup(x => x.DeserializeAsync<QueryApiClient.ErrorsOnlyBody>("{}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryApiClient.ErrorsOnlyBody { Errors = errors });

        var failure = new ClientException("Neo.ClientError.Request.Invalid", "no such tx");
        _autoMocker.GetMock<IQueryApiFailureReader>()
            .Setup(x => x.Read(response, "{}", errors))
            .Returns(failure);

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var act = () => subject.ExecuteAsync(new HttpRequestMessage(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ClientException>()).Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task ExecuteQueryAsync_ReturnsTheFailureOnTheResult_WhenTheBodyCarriesAHeader()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("with header") };
        var body = new QueryApiResultBody
        {
            Data = new QueryApiDataBody { Fields = ["n"] },
            Errors = [new QueryApiErrorBody("Neo.ClientError.Statement.ArithmeticError", "/ by zero")]
        };
        var failure = new ClientException("Neo.ClientError.Statement.ArithmeticError", "/ by zero");
        SetupQueryResponse(response, "with header", body, failure);

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var result = await subject.ExecuteQueryAsync(new HttpRequestMessage(), TestContext.Current.CancellationToken);

        result.Body.Should().BeSameAs(body);
        result.Failure.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task ExecuteQueryAsync_ThrowsTheFailure_WhenTheBodyCarriesNoHeader()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("no header") };
        var body = new QueryApiResultBody
        {
            Errors = [new QueryApiErrorBody("Neo.ClientError.Statement.ArithmeticError", "/ by zero")]
        };
        var failure = new ClientException("Neo.ClientError.Statement.ArithmeticError", "/ by zero");
        SetupQueryResponse(response, "no header", body, failure);

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var act = () => subject.ExecuteQueryAsync(new HttpRequestMessage(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ClientException>()).Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task ExecuteQueryAsync_ThrowsTheFailure_WhenTheDataCarriesNoFields()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("empty data") };
        var body = new QueryApiResultBody
        {
            Data = new QueryApiDataBody(),
            Errors = [new QueryApiErrorBody("Neo.ClientError.Statement.ArithmeticError", "/ by zero")]
        };
        var failure = new ClientException("Neo.ClientError.Statement.ArithmeticError", "/ by zero");
        SetupQueryResponse(response, "empty data", body, failure);

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var act = () => subject.ExecuteQueryAsync(new HttpRequestMessage(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ClientException>()).Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task ExecuteQueryAsync_ReturnsNoFailure_WhenTheResponseSucceeded()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("ok") };
        var body = new QueryApiResultBody { Data = new QueryApiDataBody { Fields = ["n"] } };
        SetupQueryResponse(response, "ok", body, null);

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var result = await subject.ExecuteQueryAsync(new HttpRequestMessage(), TestContext.Current.CancellationToken);

        result.Body.Should().BeSameAs(body);
        result.Failure.Should().BeNull();
    }

    private void SetupQueryResponse(
        HttpResponseMessage response,
        string json,
        QueryApiResultBody body,
        Neo4jException? failure)
    {
        _autoMocker.GetMock<IQueryApiHttpTransport>()
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        _autoMocker.GetMock<IJsonDeserializer>()
            .Setup(x => x.DeserializeAsync<QueryApiResultBody>(json, It.IsAny<CancellationToken>()))
            .ReturnsAsync(body);

        _autoMocker.GetMock<IQueryApiFailureReader>()
            .Setup(x => x.Read(response, json, body.Errors))
            .Returns(failure);
    }

    private static HttpRequestMessage RequestUsing(IAuthToken token)
    {
        var request = new HttpRequestMessage();
        request.Options.Set(QueryApiAuthEnricher.AuthTokenKey, token);
        return request;
    }

    private (Func<IAuthToken?> Token, Func<SecurityException?> Failure) CaptureNotification(bool handled = false)
    {
        IAuthToken? notifiedToken = null;
        SecurityException? notifiedFailure = null;
        _autoMocker.GetMock<ISecurityExceptionNotifier>()
            .Setup(n => n.NotifyAsync(It.IsAny<IAuthToken>(), It.IsAny<SecurityException>(), It.IsAny<CancellationToken>()))
            .Callback<IAuthToken, SecurityException, CancellationToken>(
                (token, failure, _) =>
                {
                    notifiedToken = token;
                    notifiedFailure = failure;
                    failure.Retriable = handled;
                })
            .Returns(ValueTask.CompletedTask);

        return (() => notifiedToken, () => notifiedFailure);
    }

    private void SetupThrownFailure(Neo4jException failure)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new ByteArrayContent([]) };
        QueryApiErrorBody[] errors = [new QueryApiErrorBody(failure.Code, failure.Message)];

        _autoMocker.GetMock<IQueryApiHttpTransport>()
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        _autoMocker.GetMock<IJsonDeserializer>()
            .Setup(x => x.DeserializeAsync<TestBody>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestBody { Errors = errors });

        _autoMocker.GetMock<IQueryApiFailureReader>()
            .Setup(x => x.Read(response, "", errors))
            .Returns(failure);
    }

    [Fact]
    public async Task NotifiesASecurityFailure_WithTheTokenTheRequestUsed()
    {
        var token = AuthTokens.Basic("alice", "pass");
        var failure = new SecurityException("Neo.ClientError.Security.TokenExpired", "expired");
        SetupThrownFailure(failure);
        var notified = CaptureNotification();

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var act = () => subject.ExecuteAsync<TestBody>(RequestUsing(token), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<SecurityException>();

        notified.Token().Should().BeSameAs(token);
        notified.Failure().Should().BeSameAs(failure);
    }

    [Fact]
    public async Task ThrowsASecurityFailureThatAlreadyReflectsTheNotification()
    {
        var failure = new SecurityException("Neo.ClientError.Security.TokenExpired", "expired");
        SetupThrownFailure(failure);
        CaptureNotification(handled: true);

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var act = () => subject.ExecuteAsync<TestBody>(
            RequestUsing(AuthTokens.Basic("alice", "pass")),
            TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<SecurityException>()).Which.IsRetriable.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteQueryAsync_NotifiesASecurityFailure_ThatRidesOnTheResult()
    {
        var token = AuthTokens.Basic("alice", "pass");
        var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("with header") };
        var body = new QueryApiResultBody
        {
            Data = new QueryApiDataBody { Fields = ["n"] },
            Errors = [new QueryApiErrorBody("Neo.ClientError.Security.TokenExpired", "expired")]
        };
        var failure = new SecurityException("Neo.ClientError.Security.TokenExpired", "expired");
        SetupQueryResponse(response, "with header", body, failure);
        var notified = CaptureNotification();

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var result = await subject.ExecuteQueryAsync(RequestUsing(token), TestContext.Current.CancellationToken);

        result.Failure.Should().BeSameAs(failure);
        notified.Token().Should().BeSameAs(token);
        notified.Failure().Should().BeSameAs(failure);
    }

    [Fact]
    public async Task DoesNotNotify_AFailureThatIsNotASecurityException()
    {
        SetupThrownFailure(new ClientException("Neo.ClientError.Statement.SyntaxError", "bad"));

        var subject = _autoMocker.CreateInstance<QueryApiClient>();
        var act = () => subject.ExecuteAsync<TestBody>(
            RequestUsing(AuthTokens.Basic("alice", "pass")),
            TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ClientException>();

        _autoMocker.GetMock<ISecurityExceptionNotifier>()
            .Verify(
                n => n.NotifyAsync(It.IsAny<IAuthToken>(), It.IsAny<SecurityException>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }
}
