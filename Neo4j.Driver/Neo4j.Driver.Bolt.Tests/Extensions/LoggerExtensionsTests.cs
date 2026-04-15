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

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Neo4j.Driver.Bolt.Extensions;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests.Extensions;

[TestFixture]
internal class LoggerExtensionsTests
{
    [Test]
    public void LogIfInvokesArgsAndLogsWhenLevelEnabled()
    {
        var logger = new Mock<ILogger>();
        logger.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(true);
        var argsInvoked = false;
        object[] GetArgs() { argsInvoked = true; return [42]; }

        logger.Object.LogIf(LogLevel.Debug, "Message {A}", GetArgs);

        argsInvoked.Should().BeTrue();
        logger.Verify(
            x => x.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public void LogIfDoesNotInvokeArgsWhenLevelDisabled()
    {
        var logger = new Mock<ILogger>();
        logger.Setup(x => x.IsEnabled(LogLevel.Trace)).Returns(false);
        var argsInvoked = false;

        logger.Object.LogIf(LogLevel.Trace, "Message", () => { argsInvoked = true; return []; });

        argsInvoked.Should().BeFalse();
    }
}
