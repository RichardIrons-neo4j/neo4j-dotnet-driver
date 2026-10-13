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
using FluentAssertions;
using Neo4j.Driver.Internal;
using Neo4j.Driver.Internal.MessageHandling;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.Core;

public class TransactionLifecycleTests
{
    public enum Operation
    {
        Run,
        Commit,
        Rollback
    }

    private readonly ClientException _original = new("Neo.ClientError.Statement.ArithmeticError", "/ by zero");

    private static TransactionLifecycle ActiveLifecycle()
    {
        var subject = new TransactionLifecycle();
        subject.Activate();
        return subject;
    }

    private static void Ensure(ITransactionLifecycle subject, Operation operation)
    {
        switch (operation)
        {
            case Operation.Run:
                subject.EnsureCanRun();
                break;
            case Operation.Commit:
                subject.EnsureCanCommit();
                break;
            case Operation.Rollback:
                subject.EnsureCanRollback();
                break;
        }
    }

    [Fact]
    public void GetState_IsCreated_BeforeActivation()
    {
        var subject = new TransactionLifecycle();

        var state = subject.GetState();

        state.Should().Be(TransactionState.Created);
    }

    [Fact]
    public void GetState_IsActive_AfterActivation()
    {
        var subject = ActiveLifecycle();

        var state = subject.GetState();

        state.Should().Be(TransactionState.Active);
    }

    [Fact]
    public void GetState_IsCommitted_AfterCommitted()
    {
        var subject = ActiveLifecycle();
        subject.Committed();

        var state = subject.GetState();

        state.Should().Be(TransactionState.Committed);
    }

    [Fact]
    public void GetState_IsRolledBack_AfterRolledBack()
    {
        var subject = ActiveLifecycle();
        subject.RolledBack();

        var state = subject.GetState();

        state.Should().Be(TransactionState.RolledBack);
    }

    [Theory]
    [InlineData(Operation.Run)]
    [InlineData(Operation.Commit)]
    [InlineData(Operation.Rollback)]
    public void Ensure_DoesNotThrow_WhenActive(Operation operation)
    {
        var subject = ActiveLifecycle();

        var act = () => Ensure(subject, operation);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(Operation.Run, true, "Cannot run query in this transaction, because it has already been committed.")]
    [InlineData(Operation.Run, false, "Cannot run query in this transaction, because it has already been rolled back.")]
    [InlineData(Operation.Commit, true, "Cannot commit this transaction, because it has already been committed.")]
    [InlineData(Operation.Commit, false, "Cannot commit this transaction, because it has already been rolled back.")]
    [InlineData(Operation.Rollback, true, "Cannot rollback this transaction, because it has already been committed.")]
    [InlineData(Operation.Rollback, false, "Cannot rollback this transaction, because it has already been rolled back.")]
    public void Ensure_ThrowsTransactionClosed_WhenAlreadyFinished(Operation operation, bool committed, string message)
    {
        var subject = ActiveLifecycle();
        if (committed)
        {
            subject.Committed();
        }
        else
        {
            subject.RolledBack();
        }

        var act = () => Ensure(subject, operation);

        act.Should().Throw<TransactionClosedException>().WithMessage(message);
    }

    [Theory]
    [InlineData(Operation.Run)]
    [InlineData(Operation.Commit)]
    public void Ensure_ThrowsTransactionTerminated_CarryingTheOriginal_AfterFail(Operation operation)
    {
        var subject = ActiveLifecycle();
        subject.Fail(_original);

        var act = () => Ensure(subject, operation);

        act.Should().Throw<TransactionTerminatedException>().Which.InnerException.Should().BeSameAs(_original);
    }

    [Fact]
    public void EnsureCanRollback_DoesNotThrow_AfterFail()
    {
        var subject = ActiveLifecycle();
        subject.Fail(_original);

        var act = () => subject.EnsureCanRollback();

        act.Should().NotThrow();
    }

    [Fact]
    public void GetState_IsFailed_AfterFail()
    {
        var subject = ActiveLifecycle();
        subject.Fail(_original);

        var state = subject.GetState();

        state.Should().Be(TransactionState.Failed);
    }

    [Fact]
    public void TryGetError_ReturnsFalse_WhenNothingHasFailed()
    {
        var subject = ActiveLifecycle();

        var failed = subject.TryGetError(out var error);

        failed.Should().BeFalse();
        error.Should().BeNull();
    }

    [Fact]
    public void TryGetError_ReturnsTheOriginal_AfterFail()
    {
        var subject = ActiveLifecycle();
        subject.Fail(_original);

        var failed = subject.TryGetError(out var error);

        failed.Should().BeTrue();
        error.Should().BeSameAs(_original);
    }

    [Fact]
    public void Fail_KeepsTheFirstError()
    {
        var subject = ActiveLifecycle();
        var second = new ClientException("Neo.ClientError.Statement.SyntaxError", "bad query");
        subject.Fail(_original);
        subject.Fail(second);

        subject.TryGetError(out var error);

        error.Should().BeSameAs(_original);
    }

    [Fact]
    public void GetState_IsFailed_AsSoonAsADeferredFailureIsRecorded()
    {
        var subject = ActiveLifecycle();
        subject.FailDeferred(new ResponsePipelineError(_original));

        var state = subject.GetState();

        state.Should().Be(TransactionState.Failed);
    }

    [Fact]
    public void TryGetError_ReturnsThePendingException_AfterFailDeferred()
    {
        var subject = ActiveLifecycle();
        subject.FailDeferred(new ResponsePipelineError(_original));

        var failed = subject.TryGetError(out var error);

        failed.Should().BeTrue();
        error.Should().BeSameAs(_original);
    }

    [Theory]
    [InlineData(Operation.Run)]
    [InlineData(Operation.Commit)]
    public void Ensure_ThrowsThePendingFailureItself_ThenTransactionTerminated(Operation operation)
    {
        var subject = ActiveLifecycle();
        subject.FailDeferred(new ResponsePipelineError(_original));

        var first = () => Ensure(subject, operation);
        var second = () => Ensure(subject, operation);

        first.Should().Throw<ClientException>().Which.Should().BeSameAs(_original);
        second.Should().Throw<TransactionTerminatedException>().Which.InnerException.Should().BeSameAs(_original);
    }

    [Theory]
    [InlineData(Operation.Run)]
    [InlineData(Operation.Commit)]
    public void Ensure_ThrowsTransactionTerminated_WhenTheFailureWasAlreadySurfacedElsewhere(Operation operation)
    {
        var pending = new ResponsePipelineError(_original);
        var subject = ActiveLifecycle();
        subject.FailDeferred(pending);
        var surface = () => pending.EnsureThrown();
        surface.Should().Throw<ClientException>();

        var act = () => Ensure(subject, operation);

        act.Should().Throw<TransactionTerminatedException>().Which.InnerException.Should().BeSameAs(_original);
    }
}
