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

public class ServerDiscoveryTests
{
    private readonly AutoMocker _autoMocker = AutoMocker.ForTesting<ServerDiscovery>();

    private Mock<IConnectivityVerifier> Verifier => _autoMocker.GetMock<IConnectivityVerifier>();

    [Fact]
    public async Task Discovers_OnlyOnce_AcrossSuccessiveCalls()
    {
        Verifier.Setup(x => x.VerifyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IServerInfo>());
        var subject = _autoMocker.CreateInstance<ServerDiscovery>();

        await subject.EnsureDiscoveredAsync(TestContext.Current.CancellationToken);
        await subject.EnsureDiscoveredAsync(TestContext.Current.CancellationToken);

        Verifier.Verify(x => x.VerifyAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SharesOneDiscovery_BetweenCallsMadeWhileItIsInFlight()
    {
        var discovery = new TaskCompletionSource<IServerInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        Verifier.Setup(x => x.VerifyAsync(It.IsAny<CancellationToken>())).Returns(discovery.Task);
        var subject = _autoMocker.CreateInstance<ServerDiscovery>();

        var first = subject.EnsureDiscoveredAsync(TestContext.Current.CancellationToken);
        var second = subject.EnsureDiscoveredAsync(TestContext.Current.CancellationToken);
        discovery.SetResult(Mock.Of<IServerInfo>());
        await Task.WhenAll(first, second);

        Verifier.Verify(x => x.VerifyAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancellingOneCaller_CancelsOnlyThatCaller()
    {
        var discovery = new TaskCompletionSource<IServerInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        Verifier.Setup(x => x.VerifyAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => discovery.Task.WaitAsync(token));
        var subject = _autoMocker.CreateInstance<ServerDiscovery>();
        using var cancellation = new CancellationTokenSource();
        var cancelled = subject.EnsureDiscoveredAsync(cancellation.Token);
        var other = subject.EnsureDiscoveredAsync(TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();
        discovery.SetResult(Mock.Of<IServerInfo>());

        var cancelledCaller = () => cancelled;
        var otherCaller = () => other;
        await cancelledCaller.Should().ThrowAsync<OperationCanceledException>();
        await otherCaller.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ThrowsTheDiscoveryFailure()
    {
        var failure = new ServiceUnavailableException("discovery failed");
        Verifier.Setup(x => x.VerifyAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var subject = _autoMocker.CreateInstance<ServerDiscovery>();

        var act = () => subject.EnsureDiscoveredAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableException>()).Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task DiscoversAgain_AfterAFailedDiscovery()
    {
        Verifier.SetupSequence(x => x.VerifyAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceUnavailableException("discovery failed"))
            .ReturnsAsync(Mock.Of<IServerInfo>());
        var subject = _autoMocker.CreateInstance<ServerDiscovery>();
        var failed = () => subject.EnsureDiscoveredAsync(TestContext.Current.CancellationToken);
        await failed.Should().ThrowAsync<ServiceUnavailableException>();

        var retry = () => subject.EnsureDiscoveredAsync(TestContext.Current.CancellationToken);

        await retry.Should().NotThrowAsync();
    }
}
