// Copyright (c) "Neo4j"
// Neo4j Sweden AB [https://neo4j.com]
// 
// Licensed under the Apache License, Version 2.0 (the "License").
// you may not use this file except in compliance with the License.
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
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Neo4j.Driver.Internal;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.Core;

public class ScopedSessionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly Dictionary<string, object> Metadata = new() { ["app"] = "tests" };

    private readonly AutoMocker _autoMocker = AutoMocker.ForTesting<ScopedSession>();

    public ScopedSessionTests()
    {
        _autoMocker.Use(SessionConfig.Builder.Build());
        _autoMocker.Use<IBookmarkTracker>(new BookmarkTracker(SessionConfig.Builder.Build()));
        _autoMocker.Use<Func<TransactionConfigBuilder>>(
            () => new TransactionConfigBuilder(Mock.Of<INeo4jLogger>(), new TransactionConfig()));
    }

    private static void ConfigureTransaction(TransactionConfigBuilder builder)
    {
        builder.WithTimeout(Timeout).WithMetadata(Metadata);
    }

    private static bool IsConfigured(TransactionConfig config)
    {
        return config.Timeout == Timeout && ReferenceEquals(config.Metadata, Metadata);
    }

    private void UsePassThroughRetryLogic()
    {
        _autoMocker.Use<IAsyncRetryLogic>(new SimpleRetryLogic(fn => fn()));
    }

    private void UseThrowingRetryLogic()
    {
        _autoMocker.Use<IAsyncRetryLogic>(
            new SimpleRetryLogic(_ => throw new ScopedSessionTestException("Retry failed")));
    }

    private Mock<IResultCursor> FirstRunReturnsCursor(bool isOpen)
    {
        var first = new Mock<IResultCursor>();
        first.SetupGet(c => c.IsOpen).Returns(isOpen);
        _autoMocker.GetMock<IAutoCommitRunner>()
            .SetupSequence(r => r.RunAsync(It.IsAny<Query>(), It.IsAny<TransactionConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(first.Object)
            .ReturnsAsync(Mock.Of<IResultCursor>());

        return first;
    }

    private Mock<IInternalAsyncTransaction> FactoryBeginsTransaction(bool isOpen)
    {
        var tx = new Mock<IInternalAsyncTransaction>();
        tx.SetupGet(t => t.IsOpen).Returns(isOpen);
        _autoMocker.GetMock<ITransactionFactory>()
            .Setup(f => f.BeginTransactionAsync(It.IsAny<TransactionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        return tx;
    }

    [Fact]
    public async Task RunAsync_ReturnsCursorFromRunner()
    {
        var query = new Query("RETURN 1");
        var expectedCursor = Mock.Of<IResultCursor>();

        _autoMocker.GetMock<IAutoCommitRunner>()
            .Setup(r => r.RunAsync(query, TransactionConfig.Default, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCursor);

        var sut = _autoMocker.CreateInstance<ScopedSession>();
        var result = await sut.RunAsync(query, null!, false);

        result.Should().BeSameAs(expectedCursor);
    }

    [Fact]
    public async Task RunAsync_RunsWithTheConfigBuiltFromItsAction()
    {
        var query = new Query("RETURN 1");
        var expectedCursor = Mock.Of<IResultCursor>();
        _autoMocker.GetMock<IAutoCommitRunner>()
            .Setup(r => r.RunAsync(query, It.Is<TransactionConfig>(c => IsConfigured(c)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCursor);

        var sut = _autoMocker.CreateInstance<ScopedSession>();

        var result = await sut.RunAsync(query, ConfigureTransaction);

        result.Should().BeSameAs(expectedCursor);
    }

    [Fact]
    public async Task BeginTransactionAsync_BeginsWithTheConfigBuiltFromItsAction()
    {
        var tx = Mock.Of<IInternalAsyncTransaction>();
        _autoMocker.GetMock<ITransactionFactory>()
            .Setup(f => f.BeginTransactionAsync(
                It.Is<TransactionOptions>(o => o.AccessMode == AccessMode.Read && IsConfigured(o.TransactionConfig)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx);

        var sut = _autoMocker.CreateInstance<ScopedSession>();

        var result = await sut.BeginTransactionAsync(AccessMode.Read, ConfigureTransaction);

        result.Should().BeSameAs(tx);
    }

    [Fact]
    public async Task ExecuteReadAsync_BeginsWithTheConfigBuiltFromItsAction()
    {
        UsePassThroughRetryLogic();
        var tx = new Mock<IInternalAsyncTransaction>();
        _autoMocker.GetMock<ITransactionFactory>()
            .Setup(f => f.BeginTransactionAsync(
                It.Is<TransactionOptions>(o => o.AccessMode == AccessMode.Read && IsConfigured(o.TransactionConfig)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        var sut = _autoMocker.CreateInstance<ScopedSession>();

        var result = await sut.ExecuteReadAsync(
            runner => Task.FromResult(ReferenceEquals(runner, tx.Object) ? 42 : -1),
            ConfigureTransaction);

        result.Should().Be(42);
    }

    [Fact]
    public async Task PipelinedExecuteWriteAsync_BeginsWithTheGivenConfig()
    {
        UsePassThroughRetryLogic();
        var config = new TransactionConfig(Metadata, Timeout);
        var expected = new EagerResult<int>(42, Mock.Of<IResultSummary>(), []);
        var tx = new Mock<IInternalAsyncTransaction>();
        _autoMocker.GetMock<ITransactionFactory>()
            .Setup(f => f.BeginTransactionAsync(
                new TransactionOptions(AccessMode.Write, config),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        var sut = _autoMocker.CreateInstance<ScopedSession>();

        var result = await sut.PipelinedExecuteWriteAsync(
            runner => Task.FromResult(ReferenceEquals(runner, tx.Object) ? expected : null!),
            config);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task DisposeAsync_ConsumesCursorFromLastRunAsync()
    {
        var query = new Query("RETURN 1");
        var cursor = new Mock<IResultCursor>();

        _autoMocker.GetMock<IAutoCommitRunner>()
            .Setup(r => r.RunAsync(query, TransactionConfig.Default, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);

        var sut = _autoMocker.CreateInstance<ScopedSession>();
        await sut.RunAsync(query, null!, false);

        await sut.DisposeAsync();

        cursor.Verify(c => c.ConsumeAsync(), Times.Once);
    }

    [Fact]
    public async Task BeginTransactionAsync_ReturnsTransactionFromFactory()
    {
        var tx = Mock.Of<IInternalAsyncTransaction>();
        _autoMocker.GetMock<ITransactionFactory>()
            .Setup(f => f.BeginTransactionAsync(
                new TransactionOptions(AccessMode.Read, TransactionConfig.Default),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx);

        var sut = _autoMocker.CreateInstance<ScopedSession>();

        var result = await sut.BeginTransactionAsync(AccessMode.Read);

        result.Should().BeSameAs(tx);
    }

    [Fact]
    public async Task BeginTransactionAsync_WithoutAMode_BeginsInTheSessionsDefaultAccessMode()
    {
        _autoMocker.Use(SessionConfig.Builder.WithDefaultAccessMode(AccessMode.Read).Build());
        var tx = Mock.Of<IInternalAsyncTransaction>();
        _autoMocker.GetMock<ITransactionFactory>()
            .Setup(f => f.BeginTransactionAsync(
                new TransactionOptions(AccessMode.Read, TransactionConfig.Default),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx);

        var sut = _autoMocker.CreateInstance<ScopedSession>();

        var result = await sut.BeginTransactionAsync();

        result.Should().BeSameAs(tx);
    }

    [Fact]
    public async Task ExecuteWriteAsync_WhenCommitFails_ThrowsOriginalExceptionEvenIfRollbackAlsoFails()
    {
        UsePassThroughRetryLogic();
        var commitError = new ServiceUnavailableException("HTTP 500");
        var tx = new Mock<IInternalAsyncTransaction>();
        tx.SetupGet(t => t.IsOpen).Returns(true);
        tx.Setup(t => t.CommitAsync()).ThrowsAsync(commitError);
        tx.Setup(t => t.RollbackAsync()).ThrowsAsync(new ServiceUnavailableException("rollback also failed"));
        _autoMocker.GetMock<ITransactionFactory>()
            .Setup(f => f.BeginTransactionAsync(
                It.IsAny<TransactionOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        var sut = _autoMocker.CreateInstance<ScopedSession>();

        var act = () => sut.ExecuteWriteAsync(_ => Task.FromResult(0));

        await act.Should().ThrowAsync<ServiceUnavailableException>().WithMessage("HTTP 500");
    }

    [Fact]
    public async Task ExecuteReadAsync_PassesTransactionToWorkAndReturnsResult()
    {
        UsePassThroughRetryLogic();
        var tx = new Mock<IInternalAsyncTransaction>();
        tx.Setup(t => t.CommitAsync()).Returns(Task.CompletedTask);
        _autoMocker.GetMock<ITransactionFactory>()
            .Setup(f => f.BeginTransactionAsync(
                new TransactionOptions(AccessMode.Read, TransactionConfig.Default),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        var sut = _autoMocker.CreateInstance<ScopedSession>();

        var result =
            await sut.ExecuteReadAsync(runner => Task.FromResult(ReferenceEquals(runner, tx.Object) ? 42 : -1));

        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteWriteAsync_PropagatesExceptionFromRetryLogic()
    {
        UseThrowingRetryLogic();

        var sut = _autoMocker.CreateInstance<ScopedSession>();
        var act = () => sut.ExecuteWriteAsync(_ => Task.FromResult(42));

        await act.Should().ThrowAsync<ScopedSessionTestException>().WithMessage("Retry failed");
    }

    [Fact]
    public async Task RunAsync_WhileATransactionIsOpen_ThrowsTransactionNesting()
    {
        FactoryBeginsTransaction(isOpen: true);
        var sut = _autoMocker.CreateInstance<ScopedSession>();
        await sut.BeginTransactionAsync();

        var act = () => sut.RunAsync("RETURN 1");

        await act.Should().ThrowAsync<TransactionNestingException>();
    }

    [Fact]
    public async Task RunAsync_InsideATransactionFunction_ThrowsTransactionNesting()
    {
        UsePassThroughRetryLogic();
        FactoryBeginsTransaction(isOpen: true);
        var sut = _autoMocker.CreateInstance<ScopedSession>();

        var act = () => sut.ExecuteWriteAsync<IResultCursor>(_ => sut.RunAsync("RETURN 1"));

        await act.Should().ThrowAsync<TransactionNestingException>();
    }

    [Fact]
    public async Task BeginTransactionAsync_InsideATransactionFunction_ThrowsTransactionNesting()
    {
        UsePassThroughRetryLogic();
        FactoryBeginsTransaction(isOpen: true);
        var sut = _autoMocker.CreateInstance<ScopedSession>();

        var act = () => sut.ExecuteWriteAsync<IAsyncTransaction>(_ => sut.BeginTransactionAsync());

        await act.Should().ThrowAsync<TransactionNestingException>();
    }

    [Fact]
    public async Task ExecuteWriteAsync_WhileATransactionIsOpen_ThrowsTransactionNesting()
    {
        UsePassThroughRetryLogic();
        FactoryBeginsTransaction(isOpen: true);
        var sut = _autoMocker.CreateInstance<ScopedSession>();
        await sut.BeginTransactionAsync();

        var act = () => sut.ExecuteWriteAsync(_ => Task.FromResult(0));

        await act.Should().ThrowAsync<TransactionNestingException>();
    }

    [Fact]
    public async Task RunAsync_ConsumesTheCursorOfThePreviousRun()
    {
        var previous = FirstRunReturnsCursor(isOpen: true);
        var sut = _autoMocker.CreateInstance<ScopedSession>();
        await sut.RunAsync("RETURN 1");

        await sut.RunAsync("RETURN 2");

        previous.Verify(c => c.ConsumeAsync(), Times.Once);
    }

    [Fact]
    public async Task BeginTransactionAsync_ConsumesTheCursorOfThePreviousRun()
    {
        var previous = FirstRunReturnsCursor(isOpen: true);
        FactoryBeginsTransaction(isOpen: true);
        var sut = _autoMocker.CreateInstance<ScopedSession>();
        await sut.RunAsync("RETURN 1");

        await sut.BeginTransactionAsync();

        previous.Verify(c => c.ConsumeAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteWriteAsync_ConsumesTheCursorOfThePreviousRun()
    {
        UsePassThroughRetryLogic();
        var previous = FirstRunReturnsCursor(isOpen: true);
        FactoryBeginsTransaction(isOpen: true);
        var sut = _autoMocker.CreateInstance<ScopedSession>();
        await sut.RunAsync("RETURN 1");

        await sut.ExecuteWriteAsync(_ => Task.FromResult(0));

        previous.Verify(c => c.ConsumeAsync(), Times.Once);
    }

    [Fact]
    public async Task RunAsync_KeepingUnconsumedResults_ThrowsWhileThePreviousCursorIsOpen()
    {
        FirstRunReturnsCursor(isOpen: true);
        var sut = _autoMocker.CreateInstance<ScopedSession>();
        await sut.RunAsync(new Query("RETURN 1"), null!, false);

        var act = () => sut.RunAsync(new Query("RETURN 2"), null!, false);

        await act.Should().ThrowAsync<ClientException>().WithMessage("*consume the current query result*");
    }

    [Fact]
    public async Task RunAsync_KeepingUnconsumedResults_RunsOnceThePreviousCursorIsClosed()
    {
        FirstRunReturnsCursor(isOpen: false);
        var sut = _autoMocker.CreateInstance<ScopedSession>();
        await sut.RunAsync(new Query("RETURN 1"), null!, false);

        var act = () => sut.RunAsync(new Query("RETURN 2"), null!, false);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BeginTransactionAsync_KeepingUnconsumedResults_ThrowsWhileThePreviousCursorIsOpen()
    {
        FirstRunReturnsCursor(isOpen: true);
        FactoryBeginsTransaction(isOpen: true);
        var sut = _autoMocker.CreateInstance<ScopedSession>();
        await sut.RunAsync(new Query("RETURN 1"), null!, false);

        var act = () => sut.BeginTransactionAsync(null!, false);

        await act.Should().ThrowAsync<ClientException>().WithMessage("*consume the current query result*");
    }

    [Fact]
    public async Task RunAsync_AfterDispose_ThrowsClientException()
    {
        var sut = _autoMocker.CreateInstance<ScopedSession>();
        await sut.DisposeAsync();

        var act = () => sut.RunAsync("RETURN 1");

        await act.Should().ThrowAsync<ClientException>().WithMessage("*already been disposed*");
    }

    [Fact]
    public async Task BeginTransactionAsync_AfterDispose_ThrowsClientException()
    {
        FactoryBeginsTransaction(isOpen: true);
        var sut = _autoMocker.CreateInstance<ScopedSession>();
        await sut.DisposeAsync();

        var act = () => sut.BeginTransactionAsync();

        await act.Should().ThrowAsync<ClientException>().WithMessage("*already been disposed*");
    }

    [Fact]
    public async Task ExecuteWriteAsync_AfterDispose_ThrowsClientException()
    {
        UsePassThroughRetryLogic();
        FactoryBeginsTransaction(isOpen: true);
        var sut = _autoMocker.CreateInstance<ScopedSession>();
        await sut.DisposeAsync();

        var act = () => sut.ExecuteWriteAsync(_ => Task.FromResult(0));

        await act.Should().ThrowAsync<ClientException>().WithMessage("*already been disposed*");
    }

    [Fact]
    public async Task DisposeAsync_WithAnOpenTransaction_DisposesTheTransaction()
    {
        var tx = FactoryBeginsTransaction(isOpen: true);
        var sut = _autoMocker.CreateInstance<ScopedSession>();
        await sut.BeginTransactionAsync();

        await sut.DisposeAsync();

        tx.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenDisposingTheOpenTransactionFails_ThrowsClientException()
    {
        var failure = new ServiceUnavailableException("HTTP 503");
        var tx = FactoryBeginsTransaction(isOpen: true);
        tx.Setup(t => t.DisposeAsync()).Returns(new ValueTask(Task.FromException(failure)));
        var sut = _autoMocker.CreateInstance<ScopedSession>();
        await sut.BeginTransactionAsync();

        var act = () => sut.DisposeAsync().AsTask();

        (await act.Should().ThrowAsync<ClientException>())
            .Which.InnerException.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task DisposeAsync_WhenConsumingTheLastCursorFails_StillRaisesDisposed()
    {
        var previous = FirstRunReturnsCursor(isOpen: true);
        previous.Setup(c => c.ConsumeAsync())
            .ThrowsAsync(new ClientException("Neo.ClientError.Statement.ArithmeticError", "/ by zero"));
        var sut = _autoMocker.CreateInstance<ScopedSession>();
        var disposedRaised = false;
        sut.Disposed += (_, _) =>
        {
            disposedRaised = true;
            return Task.CompletedTask;
        };
        await sut.RunAsync("RETURN 1");

        var dispose = () => sut.DisposeAsync().AsTask();

        await dispose.Should().ThrowAsync<ClientException>();
        disposedRaised.Should().BeTrue();
    }
}
