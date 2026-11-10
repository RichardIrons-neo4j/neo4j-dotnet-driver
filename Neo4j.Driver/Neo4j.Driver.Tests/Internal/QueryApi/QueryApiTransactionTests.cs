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

public class QueryApiTransactionTests
{
    private const string TxId = "tx-1";

    private readonly AutoMocker _autoMocker = AutoMocker.ForTesting<QueryApiTransaction>();
    private readonly ClientException _serverFailure = new("Neo.ClientError.Statement.ArithmeticError", "/ by zero");

    public QueryApiTransactionTests()
    {
        _autoMocker.Use<ITransactionLifecycle>(new TransactionLifecycle());
    }

    private async Task<QueryApiTransaction> BegunTransactionAsync()
    {
        var transaction = _autoMocker.CreateInstance<QueryApiTransaction>();
        await transaction.BeginAsync(new TransactionOptions(AccessMode.Write));
        return transaction;
    }

    private async Task<QueryApiTransaction> TransactionWhoseRunFailedAsync(Exception failure)
    {
        _autoMocker.GetMock<ITransactionRunner>()
            .SetupSequence(r => r.RunAsync(It.IsAny<Query>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure)
            .ReturnsAsync(Mock.Of<IResultCursor>());

        var subject = await BegunTransactionAsync();
        var run = () => subject.RunAsync(new Query("RETURN 1"));
        await run.Should().ThrowAsync<Exception>();
        return subject;
    }

    private async Task<QueryApiTransaction> TransactionWhoseCommitFailedAsync(Exception failure)
    {
        _autoMocker.GetMock<ITransactionCommitter>()
            .SetupSequence(c => c.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure)
            .Returns(Task.CompletedTask);

        var subject = await BegunTransactionAsync();
        var commit = () => subject.CommitAsync();
        await commit.Should().ThrowAsync<Exception>();
        return subject;
    }

    private Func<int> CountRollbackRequests(Exception? firstFailsWith = null)
    {
        var requests = 0;
        _autoMocker.GetMock<ITransactionRollback>()
            .Setup(r => r.RollbackAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ++requests == 1 && firstFailsWith is not null
                ? Task.FromException(firstFailsWith)
                : Task.CompletedTask);

        return () => requests;
    }

    [Fact]
    public void TransactionConfig_ReturnsDefault()
    {
        var subject = _autoMocker.CreateInstance<QueryApiTransaction>();

        subject.TransactionConfig.Should().BeSameAs(TransactionConfig.Default);
    }

    [Fact]
    public async Task RunAsync_ReturnsCursorFromRunner()
    {
        var query = new Query("RETURN 1");
        var expectedCursor = Mock.Of<IResultCursor>();

        _autoMocker.GetMock<ITransactionRunner>()
            .Setup(r => r.RunAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCursor);

        var subject = await BegunTransactionAsync();
        var result = await subject.RunAsync(query);

        result.Should().BeSameAs(expectedCursor);
    }

    [Fact]
    public async Task RunAsync_String_DelegatesToQueryOverload()
    {
        var expectedCursor = Mock.Of<IResultCursor>();

        _autoMocker.GetMock<ITransactionRunner>()
            .Setup(r => r.RunAsync(It.Is<Query>(q => q.Text == "RETURN 1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCursor);

        var subject = await BegunTransactionAsync();
        var result = await subject.RunAsync("RETURN 1");

        result.Should().BeSameAs(expectedCursor);
    }

    [Fact]
    public async Task CommitAsync_ThrowsAfterCommit()
    {
        var subject = await BegunTransactionAsync();
        await subject.CommitAsync();

        var act = () => subject.CommitAsync();
        await act.Should().ThrowAsync<TransactionClosedException>();
    }

    [Fact]
    public async Task RollbackAsync_ThrowsAfterCommit()
    {
        var subject = await BegunTransactionAsync();
        await subject.CommitAsync();

        var act = () => subject.RollbackAsync();
        await act.Should().ThrowAsync<TransactionClosedException>();
    }

    [Fact]
    public async Task RunAsync_ThrowsAfterCommit()
    {
        var subject = await BegunTransactionAsync();
        await subject.CommitAsync();

        var act = () => subject.RunAsync(new Query("RETURN 1"));
        await act.Should().ThrowAsync<TransactionClosedException>();
    }

    [Fact]
    public async Task RunAsync_ThrowsAfterRollback()
    {
        var subject = await BegunTransactionAsync();
        await subject.RollbackAsync();

        var act = () => subject.RunAsync(new Query("RETURN 1"));
        await act.Should().ThrowAsync<TransactionClosedException>();
    }

    [Fact]
    public async Task CommitAsync_ThrowsAfterRollback()
    {
        var subject = await BegunTransactionAsync();
        await subject.RollbackAsync();

        var act = () => subject.CommitAsync();
        await act.Should().ThrowAsync<TransactionClosedException>();
    }

    [Fact]
    public void IsOpen_FalseBeforeBegin()
    {
        _autoMocker.CreateInstance<QueryApiTransaction>().IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task IsOpen_TrueAfterBegin()
    {
        (await BegunTransactionAsync()).IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task IsOpen_FalseAfterCommit()
    {
        var subject = await BegunTransactionAsync();
        await subject.CommitAsync();

        subject.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task IsOpen_FalseAfterRollback()
    {
        var subject = await BegunTransactionAsync();
        await subject.RollbackAsync();

        subject.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_WhenOpen_RollsBack()
    {
        var rollbackCalled = false;

        _autoMocker.GetMock<ITransactionRollback>()
            .Setup(h => h.RollbackAsync(It.IsAny<CancellationToken>()))
            .Callback(() => rollbackCalled = true)
            .Returns(Task.CompletedTask);

        var subject = await BegunTransactionAsync();
        await subject.DisposeAsync();

        rollbackCalled.Should().BeTrue();
        subject.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_WhenCommitted_DoesNotRollBack()
    {
        var rollbackCalled = false;

        _autoMocker.GetMock<ITransactionRollback>()
            .Setup(h => h.RollbackAsync(It.IsAny<CancellationToken>()))
            .Callback(() => rollbackCalled = true)
            .Returns(Task.CompletedTask);

        var subject = await BegunTransactionAsync();
        await subject.CommitAsync();
        await subject.DisposeAsync();

        rollbackCalled.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_WhenAlreadyRolledBack_DoesNotRollBackAgain()
    {
        var rollbackCount = 0;

        _autoMocker.GetMock<ITransactionRollback>()
            .Setup(h => h.RollbackAsync(It.IsAny<CancellationToken>()))
            .Callback(() => rollbackCount++)
            .Returns(Task.CompletedTask);

        var subject = await BegunTransactionAsync();

        await subject.RollbackAsync();
        await subject.DisposeAsync();

        rollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_WhenBeginFailed_DoesNotRollBack()
    {
        var rollbackCalled = false;

        _autoMocker.GetMock<ITransactionBeginner>()
            .Setup(b => b.BeginAsync(It.IsAny<TransactionOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ClientException("Neo.ClientError.Statement.SyntaxError", "nope"));

        _autoMocker.GetMock<ITransactionRollback>()
            .Setup(h => h.RollbackAsync(It.IsAny<CancellationToken>()))
            .Callback(() => rollbackCalled = true)
            .Returns(Task.CompletedTask);

        var subject = _autoMocker.CreateInstance<QueryApiTransaction>();
        var begin = () => subject.BeginAsync(new TransactionOptions(AccessMode.Write));
        await begin.Should().ThrowAsync<ClientException>();

        await subject.DisposeAsync();

        rollbackCalled.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_ExplainsTheTransactionWasCommitted_AfterCommit()
    {
        var subject = await BegunTransactionAsync();
        await subject.CommitAsync();

        var act = () => subject.RunAsync(new Query("RETURN 1"));

        (await act.Should().ThrowAsync<TransactionClosedException>())
            .WithMessage("Cannot run query in this transaction, because it has already been committed.");
    }

    [Fact]
    public async Task IsOpen_IsFalse_AfterARunFailedWithAServerError()
    {
        var subject = await TransactionWhoseRunFailedAsync(_serverFailure);

        var isOpen = subject.IsOpen;

        isOpen.Should().BeFalse();
    }

    [Fact]
    public async Task IsErrored_ReturnsTheOriginal_AfterARunFailedWithAServerError()
    {
        var subject = await TransactionWhoseRunFailedAsync(_serverFailure);

        var isErrored = subject.IsErrored(out var error);

        isErrored.Should().BeTrue();
        error.Should().BeSameAs(_serverFailure);
    }

    [Fact]
    public async Task RunAsync_ThrowsTransactionTerminated_AfterARunFailedWithAServerError()
    {
        var subject = await TransactionWhoseRunFailedAsync(_serverFailure);

        var act = () => subject.RunAsync(new Query("RETURN 1"));

        (await act.Should().ThrowAsync<TransactionTerminatedException>())
            .Which.InnerException.Should().BeSameAs(_serverFailure);
    }

    [Fact]
    public async Task CommitAsync_ThrowsTransactionTerminated_AfterARunFailedWithAServerError()
    {
        var subject = await TransactionWhoseRunFailedAsync(_serverFailure);

        var act = () => subject.CommitAsync();

        (await act.Should().ThrowAsync<TransactionTerminatedException>())
            .Which.InnerException.Should().BeSameAs(_serverFailure);
    }

    [Fact]
    public async Task RollbackAsync_SendsNothing_AfterARunFailedWithAServerError()
    {
        var rollbackRequests = CountRollbackRequests();
        var subject = await TransactionWhoseRunFailedAsync(_serverFailure);

        await subject.RollbackAsync();

        rollbackRequests().Should().Be(0);
    }

    [Fact]
    public async Task DisposeAsync_SendsNoRollback_AfterARunFailedWithAServerError()
    {
        var rollbackRequests = CountRollbackRequests();
        var subject = await TransactionWhoseRunFailedAsync(_serverFailure);

        await subject.DisposeAsync();

        rollbackRequests().Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_WithoutAServerCode_LeavesTheTransactionOpenForRollback()
    {
        var rollbackRequests = CountRollbackRequests();
        var subject = await TransactionWhoseRunFailedAsync(new ServiceUnavailableException("HTTP 503"));
        var isOpen = subject.IsOpen;

        await subject.DisposeAsync();

        isOpen.Should().BeTrue();
        rollbackRequests().Should().Be(1);
    }

    [Fact]
    public async Task IsOpen_IsFalse_AfterACommitFailedWithAServerError()
    {
        var subject = await TransactionWhoseCommitFailedAsync(_serverFailure);

        var isOpen = subject.IsOpen;

        isOpen.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_SendsNoRollback_AfterACommitFailedWithAServerError()
    {
        var rollbackRequests = CountRollbackRequests();
        var subject = await TransactionWhoseCommitFailedAsync(_serverFailure);

        await subject.DisposeAsync();

        rollbackRequests().Should().Be(0);
    }

    [Fact]
    public async Task CommitAsync_ThrowsTransactionTerminated_AfterACommitFailedWithAServerError()
    {
        var subject = await TransactionWhoseCommitFailedAsync(_serverFailure);

        var act = () => subject.CommitAsync();

        (await act.Should().ThrowAsync<TransactionTerminatedException>())
            .Which.InnerException.Should().BeSameAs(_serverFailure);
    }

    [Fact]
    public async Task CommitAsync_WithoutAServerCode_EndsTheTransaction()
    {
        var rollbackRequests = CountRollbackRequests();
        var subject = await TransactionWhoseCommitFailedAsync(new HttpRequestException("response ended prematurely"));
        var isOpen = subject.IsOpen;

        await subject.DisposeAsync();

        isOpen.Should().BeFalse();
        rollbackRequests().Should().Be(0);
    }

    [Fact]
    public async Task RollbackAsync_WithAServerError_EndsTheTransaction()
    {
        var rollbackRequests = CountRollbackRequests(firstFailsWith: _serverFailure);
        var subject = await BegunTransactionAsync();
        var rollback = () => subject.RollbackAsync();
        await rollback.Should().ThrowAsync<ClientException>();
        var isOpen = subject.IsOpen;

        await subject.DisposeAsync();

        isOpen.Should().BeFalse();
        rollbackRequests().Should().Be(1);
    }

    [Fact]
    public async Task RollbackAsync_WithoutAServerCode_EndsTheTransaction()
    {
        var rollbackRequests = CountRollbackRequests(firstFailsWith: new HttpRequestException("connection reset"));
        var subject = await BegunTransactionAsync();
        var rollback = () => subject.RollbackAsync();
        await rollback.Should().ThrowAsync<HttpRequestException>();
        var isOpen = subject.IsOpen;

        await subject.DisposeAsync();

        isOpen.Should().BeFalse();
        rollbackRequests().Should().Be(1);
    }
}
