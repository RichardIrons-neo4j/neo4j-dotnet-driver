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
using Neo4j.Driver.Internal.QueryApi;
using Neo4j.Driver.Tests.Internal.Core;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

/// <summary>
/// Rolling back a transaction discards all statements run within it. A DELETE request is used — the only Query
/// API operation that is not a POST. Spec: https://neo4j.com/docs/query-api/current/#query-api-rollback-transaction
/// </summary>
public class TransactionRollbackerTests
{
    private const string TxId = "tx-1";

    private readonly AutoMocker _autoMocker = AutoMockerExtensions.ForTesting<TransactionRollbacker>();

    public TransactionRollbackerTests()
    {
        _autoMocker.GetMock<IQueryApiTransactionContextTracker>()
            .SetupGet(x => x.Context)
            .Returns(new QueryApiTransactionContext(TxId, null));
    }

    [Fact]
    public async Task Throws_WhenTheServerRejectsTheRollback()
    {
        var request = new HttpRequestMessage();
        _autoMocker.GetMock<IQueryApiRequestBuilder>()
            .Setup(x => x.DeleteAsync($"query/v2/tx/{TxId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var failure = new ClientException("Neo.ClientError.Transaction.TransactionNotFound", "no such tx");
        _autoMocker.GetMock<IQueryApiClient>()
            .Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var subject = _autoMocker.CreateInstance<TransactionRollbacker>();
        var act = () => subject.RollbackAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ClientException>()).Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task DoesNotSendRequest_WhenTransactionAlreadyFailed()
    {
        var requestBuilder = _autoMocker.GetMock<IQueryApiRequestBuilder>();
        _autoMocker.GetMock<IQueryApiTransactionContextTracker>()
            .SetupGet(x => x.IsFailed)
            .Returns(true);

        var subject = _autoMocker.CreateInstance<TransactionRollbacker>();
        var act = () => subject.RollbackAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();

        requestBuilder.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
