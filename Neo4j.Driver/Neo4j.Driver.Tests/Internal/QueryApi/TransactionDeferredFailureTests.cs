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
using Neo4j.Driver.Internal.QueryApi;
using Neo4j.Driver.Tests.Internal.Core;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

/// <summary>
/// A response can carry the result header and then an error. The run returns normally and the failure is raised
/// later, but the transaction is already dead. These tests wire the real runner, transaction and lifecycle together.
/// </summary>
public class TransactionDeferredFailureTests
{
    private const string TxId = "tx-1";

    private readonly AutoMocker _autoMocker = AutoMockerExtensions.ForTesting<TransactionRunner>();
    private readonly ClientException _failure = new("Neo.ClientError.Statement.ArithmeticError", "/ by zero");

    public TransactionDeferredFailureTests()
    {
        _autoMocker.Use<ITransactionLifecycle>(new TransactionLifecycle());

        _autoMocker.GetMock<IQueryApiTransactionContextTracker>()
            .SetupGet(x => x.Context)
            .Returns(new QueryApiTransactionContext(TxId, null, AuthTokens.None));

        _autoMocker.GetMock<IQueryApiRequestBuilder>()
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<IQueryApiRequestBody>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpRequestMessage());

        var body = new QueryApiResultBody { Data = new QueryApiDataBody { Fields = ["n"] } };
        _autoMocker.GetMock<IQueryApiClient>()
            .Setup(x => x.ExecuteQueryAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryApiQueryResult(body, new HttpResponseMessage().Headers, _failure));

        _autoMocker.GetMock<IQueryApiResultCursorBuilder>()
            .Setup(x => x.Build(It.IsAny<QueryApiResultSet>(), It.IsAny<Query>()))
            .Returns(Mock.Of<IResultCursor>());
    }

    private async Task<QueryApiTransaction> TransactionWhoseRunReturnedADeferredFailureAsync()
    {
        _autoMocker.Use<ITransactionRunner>(_autoMocker.CreateInstance<TransactionRunner>());

        var subject = _autoMocker.CreateInstance<QueryApiTransaction>();
        await subject.BeginAsync(new TransactionOptions(AccessMode.Write));
        await subject.RunAsync(new Query("RETURN 1/0 AS n"));
        return subject;
    }

    [Fact]
    public async Task IsOpen_IsFalse_WhenARunReturnedADeferredFailure()
    {
        var subject = await TransactionWhoseRunReturnedADeferredFailureAsync();

        var isOpen = subject.IsOpen;

        isOpen.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_ThrowsTheOriginalFailureWithoutSendingARequest_AfterADeferredFailure()
    {
        var subject = await TransactionWhoseRunReturnedADeferredFailureAsync();

        var act = () => subject.RunAsync(new Query("RETURN 1 AS n"));

        (await act.Should().ThrowAsync<ClientException>()).Which.Should().BeSameAs(_failure);
        _autoMocker.GetMock<IQueryApiClient>()
            .Verify(x => x.ExecuteQueryAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitAsync_ThrowsTheOriginalFailureWithoutSendingARequest_AfterADeferredFailure()
    {
        var subject = await TransactionWhoseRunReturnedADeferredFailureAsync();

        var act = () => subject.CommitAsync();

        (await act.Should().ThrowAsync<ClientException>()).Which.Should().BeSameAs(_failure);
        _autoMocker.GetMock<ITransactionCommitter>()
            .Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisposeAsync_SendsNoRollback_AfterADeferredFailure()
    {
        var subject = await TransactionWhoseRunReturnedADeferredFailureAsync();

        await subject.DisposeAsync();

        _autoMocker.GetMock<ITransactionRollback>()
            .Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
