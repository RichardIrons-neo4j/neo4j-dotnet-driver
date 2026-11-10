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
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Neo4j.Driver.Internal;
using Neo4j.Driver.Internal.MessageHandling;
using Neo4j.Driver.Internal.QueryApi;
using Neo4j.Driver.Tests.Internal.Core;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

/// <summary>
/// Running a query within an explicit transaction requires the transaction ID in the URL and, for Aura instances,
/// the <c>neo4j-cluster-affinity</c> header forwarded from BEGIN. Spec:
/// https://neo4j.com/docs/query-api/current/#query-api-run-query-in-transaction
/// </summary>
public class TransactionRunnerTests
{
    private const string TxId = "tx-1";

    private readonly AutoMocker _autoMocker = AutoMocker.ForTesting<TransactionRunner>();

    public TransactionRunnerTests()
    {
        _autoMocker.GetMock<IQueryApiTransactionContextTracker>()
            .SetupGet(x => x.Context)
            .Returns(new QueryApiTransactionContext(TxId, null, AuthTokens.None));
    }

    private void SetupChain(QueryApiResultBody? body = null, Neo4jException? failure = null)
    {
        body ??= new QueryApiResultBody();
        var request = new HttpRequestMessage();

        _autoMocker.GetMock<IQueryApiRequestBuilder>()
            .Setup(x => x.PostAsync($"query/v2/tx/{TxId}", It.IsAny<IQueryApiRequestBody>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        _autoMocker.GetMock<IQueryApiClient>()
            .Setup(x => x.ExecuteQueryAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryApiQueryResult(body, new HttpResponseMessage().Headers, failure));
    }

    [Fact]
    public async Task RunAsync_ReturnsCursor_BuiltFromTheResponseBody()
    {
        // Spec: successful response contains data.fields, data.values; these are built into an IResultCursor
        var responseBody = new QueryApiResultBody
        {
            Data = new QueryApiDataBody { Fields = ["x"] }
        };
        SetupChain(responseBody);

        var query = new Query("RETURN 42 AS x");
        var expectedCursor = new Mock<IResultCursor>().Object;
        _autoMocker.GetMock<IQueryApiResultCursorBuilder>()
            .Setup(x => x.Build(responseBody, null, query))
            .Returns(expectedCursor);

        var subject = _autoMocker.CreateInstance<TransactionRunner>();
        var cursor = await subject.RunAsync(query, TestContext.Current.CancellationToken);

        cursor.Should().BeSameAs(expectedCursor);
    }

    [Fact]
    public async Task RunAsync_Throws_WhenExecuteAsyncThrows()
    {
        var request = new HttpRequestMessage();

        _autoMocker.GetMock<IQueryApiRequestBuilder>()
            .Setup(x => x.PostAsync($"query/v2/tx/{TxId}", It.IsAny<IQueryApiRequestBody>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        _autoMocker.GetMock<IQueryApiClient>()
            .Setup(x => x.ExecuteQueryAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceUnavailableException("HTTP 503"));

        var subject = _autoMocker.CreateInstance<TransactionRunner>();
        var act = () => subject.RunAsync(new Query("RETURN 1"), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ServiceUnavailableException>();
    }

    [Fact]
    public async Task RunAsync_PassesADeferredFailure_ToTheCursor()
    {
        var failure = new ClientException("Neo.ClientError.Statement.ArithmeticError", "/ by zero");
        SetupChain(new QueryApiResultBody { Data = new QueryApiDataBody { Fields = ["n"] } }, failure);

        var expectedCursor = new Mock<IResultCursor>().Object;
        _autoMocker.GetMock<IQueryApiResultCursorBuilder>()
            .Setup(x => x.Build(
                It.IsAny<QueryApiResultBody>(),
                It.Is<IResponsePipelineError>(e => e.Exception == failure),
                It.IsAny<Query>()))
            .Returns(expectedCursor);

        var subject = _autoMocker.CreateInstance<TransactionRunner>();
        var cursor = await subject.RunAsync(new Query("RETURN 1/0 AS n"), TestContext.Current.CancellationToken);

        cursor.Should().BeSameAs(expectedCursor);
    }

    [Fact]
    public async Task RunAsync_SharesOnePendingFailure_BetweenTheCursorAndTheLifecycle()
    {
        var failure = new ClientException("Neo.ClientError.Statement.ArithmeticError", "/ by zero");
        SetupChain(new QueryApiResultBody { Data = new QueryApiDataBody { Fields = ["n"] } }, failure);

        IResponsePipelineError? cursorError = null;
        _autoMocker.GetMock<IQueryApiResultCursorBuilder>()
            .Setup(x => x.Build(It.IsAny<QueryApiResultBody>(), It.IsAny<IResponsePipelineError?>(), It.IsAny<Query>()))
            .Callback<QueryApiResultBody, IResponsePipelineError?, Query>((_, error, _) => cursorError = error)
            .Returns(new Mock<IResultCursor>().Object);

        IResponsePipelineError? lifecycleError = null;
        _autoMocker.GetMock<ITransactionLifecycle>()
            .Setup(x => x.FailDeferred(It.IsAny<IResponsePipelineError>()))
            .Callback<IResponsePipelineError>(e => lifecycleError = e);

        var subject = _autoMocker.CreateInstance<TransactionRunner>();
        await subject.RunAsync(new Query("RETURN 1/0 AS n"), TestContext.Current.CancellationToken);

        cursorError!.Exception.Should().BeSameAs(failure);
        lifecycleError.Should().BeSameAs(cursorError);
    }

    [Fact]
    public async Task RunAsync_RequestBody_AsksForCounters()
    {
        var request = new HttpRequestMessage();
        object? capturedBody = null;

        _autoMocker.GetMock<IQueryApiRequestBuilder>()
            .Setup(x => x.PostAsync($"query/v2/tx/{TxId}", It.IsAny<IQueryApiRequestBody>(), It.IsAny<CancellationToken>()))
            .Callback<string, IQueryApiRequestBody, CancellationToken>((_, body, _) => capturedBody = body)
            .ReturnsAsync(request);

        _autoMocker.GetMock<IQueryApiClient>()
            .Setup(x => x.ExecuteQueryAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new QueryApiQueryResult(new QueryApiResultBody(), new HttpResponseMessage().Headers, null));

        _autoMocker.GetMock<IQueryApiResultCursorBuilder>()
            .Setup(x => x.Build(It.IsAny<QueryApiResultBody>(), It.IsAny<IResponsePipelineError?>(), It.IsAny<Query>()))
            .Returns(new Mock<IResultCursor>().Object);

        var subject = _autoMocker.CreateInstance<TransactionRunner>();
        await subject.RunAsync(new Query("RETURN 1"), TestContext.Current.CancellationToken);

        var body = capturedBody.Should().BeOfType<TransactionRunner.RequestBody>().Subject;
        body.IncludeCounters.Should().BeTrue();
    }
}
