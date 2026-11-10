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
using System.Text.Json;
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

public class AutoCommitRunnerTests
{
    private readonly AutoMocker _autoMocker = AutoMocker.ForTesting<AutoCommitRunner>();
    private readonly BookmarkTracker _bookmarkTracker = new(SessionConfig.Builder.Build());

    public AutoCommitRunnerTests()
    {
        _autoMocker.Use<IBookmarkTracker>(_bookmarkTracker);
    }

    private void SetupChain(QueryApiResultBody? body = null, Neo4jException? failure = null)
    {
        body ??= new QueryApiResultBody();
        var request = new HttpRequestMessage();

        _autoMocker.GetMock<IQueryApiRequestBuilder>()
            .Setup(x => x.PostAsync(
                It.IsAny<string>(),
                It.IsAny<IQueryApiRequestBody>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        _autoMocker.GetMock<IQueryApiClient>()
            .Setup(x => x.ExecuteQueryAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryApiQueryResult(body, new HttpResponseMessage().Headers, failure));
    }

    [Fact]
    public async Task RunAsync_ReturnsCursor_BuiltFromTheResponseBody()
    {
        var responseBody = new QueryApiResultBody
        {
            Data = new QueryApiDataBody
            {
                Fields = ["name", "age"],
                Values = [[JsonDocument.Parse("\"Alice\"").RootElement, JsonDocument.Parse("30").RootElement]]
            },
            Bookmarks = ["neo4j:bookmark:v1:tx55"]
        };

        SetupChain(responseBody);

        var query = new Query("MATCH (n) RETURN n.name, n.age");
        var expectedCursor = new Mock<IResultCursor>().Object;
        _autoMocker.GetMock<IQueryApiResultCursorBuilder>()
            .Setup(x => x.Build(responseBody, null, query))
            .Returns(expectedCursor);

        var subject = _autoMocker.CreateInstance<AutoCommitRunner>();
        var cursor = await subject.RunAsync(query, TestContext.Current.CancellationToken);

        cursor.Should().BeSameAs(expectedCursor);
    }

    [Fact]
    public async Task RunAsync_UpdatesBookmarkTracker_WithServerBookmarks()
    {
        // Spec: auto-commit response bookmarks must be applied to the session's tracker for causal chaining
        SetupChain(new QueryApiResultBody { Bookmarks = ["neo4j:bookmark:v1:tx55"] });
        _autoMocker.GetMock<IQueryApiResultCursorBuilder>()
            .Setup(x => x.Build(It.IsAny<QueryApiResultBody>(), It.IsAny<IResponsePipelineError?>(), It.IsAny<Query>()))
            .Returns(new Mock<IResultCursor>().Object);

        var subject = _autoMocker.CreateInstance<AutoCommitRunner>();
        await subject.RunAsync(new Query("RETURN 1"), TestContext.Current.CancellationToken);

        _bookmarkTracker.CurrentBookmarks.Values.Should().Equal("neo4j:bookmark:v1:tx55");
    }

    [Fact]
    public async Task RunAsync_IncludesCurrentBookmarks_FromTracker_InRequest()
    {
        // Spec: current session bookmarks must be forwarded in the request for causal consistency
        _bookmarkTracker.UpdateBookmarks(["existing-bookmark"]);

        object? capturedBody = null;
        var request = new HttpRequestMessage();
        _autoMocker.GetMock<IQueryApiRequestBuilder>()
            .Setup(x => x.PostAsync(
                It.IsAny<string>(),
                It.IsAny<IQueryApiRequestBody>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IQueryApiRequestBody, CancellationToken>((_, body, _) => capturedBody = body)
            .ReturnsAsync(request);

        _autoMocker.GetMock<IQueryApiClient>()
            .Setup(x => x.ExecuteQueryAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new QueryApiQueryResult(new QueryApiResultBody(), new HttpResponseMessage().Headers, null));

        _autoMocker.GetMock<IQueryApiResultCursorBuilder>()
            .Setup(x => x.Build(It.IsAny<QueryApiResultBody>(), It.IsAny<IResponsePipelineError?>(), It.IsAny<Query>()))
            .Returns(new Mock<IResultCursor>().Object);

        var subject = _autoMocker.CreateInstance<AutoCommitRunner>();
        await subject.RunAsync(new Query("RETURN 1"), TestContext.Current.CancellationToken);

        var body = capturedBody.Should().BeOfType<AutoCommitRunner.RequestBody>().Subject;
        body.Bookmarks.Should().Equal("existing-bookmark");
    }

    [Fact]
    public async Task RunAsync_Throws_WhenExecuteAsyncThrows()
    {
        var request = new HttpRequestMessage();

        _autoMocker.GetMock<IQueryApiRequestBuilder>()
            .Setup(x => x.PostAsync(
                It.IsAny<string>(),
                It.IsAny<IQueryApiRequestBody>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        _autoMocker.GetMock<IQueryApiClient>()
            .Setup(x => x.ExecuteQueryAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceUnavailableException("HTTP 503"));

        var subject = _autoMocker.CreateInstance<AutoCommitRunner>();
        var act = () => subject.RunAsync(new Query("RETURN 1"), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ServiceUnavailableException>();
    }

    [Fact]
    public async Task RunAsync_RequestBody_IncludesImpersonatedUserAndAccessMode_FromSessionContext()
    {
        _autoMocker.GetMock<ISessionContext>()
            .Setup(x => x.ImpersonatedUser)
            .Returns("banana_bob");

        _autoMocker.GetMock<ISessionContext>()
            .Setup(x => x.AccessMode)
            .Returns(AccessMode.Read);

        var request = new HttpRequestMessage();
        object? capturedBody = null;

        _autoMocker.GetMock<IQueryApiRequestBuilder>()
            .Setup(x => x.PostAsync(
                It.IsAny<string>(),
                It.IsAny<IQueryApiRequestBody>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IQueryApiRequestBody, CancellationToken>((_, body, _) => capturedBody = body)
            .ReturnsAsync(request);

        _autoMocker.GetMock<IQueryApiClient>()
            .Setup(x => x.ExecuteQueryAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new QueryApiQueryResult(new QueryApiResultBody(), new HttpResponseMessage().Headers, null));

        _autoMocker.GetMock<IQueryApiResultCursorBuilder>()
            .Setup(x => x.Build(It.IsAny<QueryApiResultBody>(), It.IsAny<IResponsePipelineError?>(), It.IsAny<Query>()))
            .Returns(new Mock<IResultCursor>().Object);

        var subject = _autoMocker.CreateInstance<AutoCommitRunner>();
        await subject.RunAsync(new Query("RETURN 1"), TestContext.Current.CancellationToken);

        var body = capturedBody.Should().BeOfType<AutoCommitRunner.RequestBody>().Subject;
        body.ImpersonatedUser.Should().Be("banana_bob");
        body.AccessMode.Should().Be("Read");
    }

    [Fact]
    public async Task RunAsync_RequestBody_AsksForCounters()
    {
        var request = new HttpRequestMessage();
        object? capturedBody = null;

        _autoMocker.GetMock<IQueryApiRequestBuilder>()
            .Setup(x => x.PostAsync(
                It.IsAny<string>(),
                It.IsAny<IQueryApiRequestBody>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IQueryApiRequestBody, CancellationToken>((_, body, _) => capturedBody = body)
            .ReturnsAsync(request);

        _autoMocker.GetMock<IQueryApiClient>()
            .Setup(x => x.ExecuteQueryAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new QueryApiQueryResult(new QueryApiResultBody(), new HttpResponseMessage().Headers, null));

        _autoMocker.GetMock<IQueryApiResultCursorBuilder>()
            .Setup(x => x.Build(It.IsAny<QueryApiResultBody>(), It.IsAny<IResponsePipelineError?>(), It.IsAny<Query>()))
            .Returns(new Mock<IResultCursor>().Object);

        var subject = _autoMocker.CreateInstance<AutoCommitRunner>();
        await subject.RunAsync(new Query("RETURN 1"), TestContext.Current.CancellationToken);

        var body = capturedBody.Should().BeOfType<AutoCommitRunner.RequestBody>().Subject;
        body.IncludeCounters.Should().BeTrue();
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

        var subject = _autoMocker.CreateInstance<AutoCommitRunner>();
        var cursor = await subject.RunAsync(new Query("RETURN 1/0 AS n"), TestContext.Current.CancellationToken);

        cursor.Should().BeSameAs(expectedCursor);
    }
}
