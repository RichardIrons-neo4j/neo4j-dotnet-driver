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

using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Neo4j.Driver.Internal;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.Core;

public class SecurityExceptionNotifierTests
{
    private readonly AutoMocker _autoMocker = AutoMocker.ForTesting<SecurityExceptionNotifier>();
    private readonly IAuthToken _token = AuthTokens.Basic("alice", "pass");
    private readonly SecurityException _failure = new("Neo.ClientError.Security.TokenExpired", "expired");

    [Fact]
    public async Task NotifyAsync_MarksTheExceptionRetriable_WhenTheContextHandledIt()
    {
        _autoMocker.GetMock<ISessionContext>()
            .Setup(c => c.HandleSecurityExceptionAsync(_token, _failure, TestContext.Current.CancellationToken))
            .ReturnsAsync(true);
        var subject = _autoMocker.CreateInstance<SecurityExceptionNotifier>();

        await subject.NotifyAsync(_token, _failure, TestContext.Current.CancellationToken);

        _failure.IsRetriable.Should().BeTrue();
    }

    [Fact]
    public async Task NotifyAsync_LeavesTheExceptionNotRetriable_WhenTheContextDidNotHandleIt()
    {
        _autoMocker.GetMock<ISessionContext>()
            .Setup(c => c.HandleSecurityExceptionAsync(_token, _failure, TestContext.Current.CancellationToken))
            .ReturnsAsync(false);
        var subject = _autoMocker.CreateInstance<SecurityExceptionNotifier>();

        await subject.NotifyAsync(_token, _failure, TestContext.Current.CancellationToken);

        _failure.IsRetriable.Should().BeFalse();
    }

    [Fact]
    public async Task NotifyAsync_AsksTheContextOncePerException()
    {
        _autoMocker.GetMock<ISessionContext>()
            .SetupSequence(c => c.HandleSecurityExceptionAsync(_token, _failure, TestContext.Current.CancellationToken))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        var subject = _autoMocker.CreateInstance<SecurityExceptionNotifier>();

        await subject.NotifyAsync(_token, _failure, TestContext.Current.CancellationToken);
        await subject.NotifyAsync(_token, _failure, TestContext.Current.CancellationToken);

        _failure.IsRetriable.Should().BeFalse();
    }
}
