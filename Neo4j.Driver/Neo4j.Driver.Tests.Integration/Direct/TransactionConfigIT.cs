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
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Neo4j.Driver.IntegrationTests.Internals;
using Xunit;

namespace Neo4j.Driver.IntegrationTests.Direct;

public sealed class TransactionConfigIT : DirectDriverTestBase
{
    private const string ReadMetadata = "CALL tx.getMetaData() YIELD metadata RETURN metadata";
    private const string SlowQuery = "UNWIND range(1, 100000) AS a UNWIND range(1, 100000) AS b RETURN count(*) AS c";

    private static readonly Dictionary<string, object> Metadata = new() { ["app"] = "tests", ["attempt"] = 3L };

    public TransactionConfigIT(ITestOutputHelper output, StandAloneIntegrationTestFixture fixture) : base(
        output,
        fixture)
    {
    }

    private IDriver Driver => Server.Driver;

    [QueryApiMinServerVersion("2026.04")]
    [RequireServerFact]
    public async Task AutoCommit_ShouldSendTheTransactionMetadata()
    {
        await using var session = Driver.AsyncSession();

        var cursor = await session.RunAsync(ReadMetadata, config => config.WithMetadata(Metadata));
        var record = await cursor.SingleAsync();

        record["metadata"].As<Dictionary<string, object>>().Should().BeEquivalentTo(Metadata);
    }

    [QueryApiMinServerVersion("2026.04")]
    [RequireServerFact]
    public async Task ExplicitTransaction_ShouldSendTheTransactionMetadata()
    {
        await using var session = Driver.AsyncSession();
        await using var transaction = await session.BeginTransactionAsync(config => config.WithMetadata(Metadata));

        var cursor = await transaction.RunAsync(ReadMetadata);
        var record = await cursor.SingleAsync();

        record["metadata"].As<Dictionary<string, object>>().Should().BeEquivalentTo(Metadata);
    }

    [QueryApiMinServerVersion("2026.04")]
    [RequireServerFact]
    public async Task AutoCommit_ShouldTerminateAQueryThatRunsLongerThanTheTimeout()
    {
        await using var session = Driver.AsyncSession();

        var exception = await Record.ExceptionAsync(
            async () =>
            {
                var cursor = await session.RunAsync(SlowQuery, config => config.WithTimeout(TimeSpan.FromSeconds(1)));
                await cursor.ConsumeAsync();
            });

        exception.Should().BeAssignableTo<Neo4jException>().Which.Code.Should().Contain("TransactionTimedOut");
    }

    [QueryApiMinServerVersion("5.26.0")]
    [RequireServerFact]
    public async Task ExplicitTransaction_ShouldTerminateAQueryThatRunsLongerThanTheTimeout()
    {
        await using var session = Driver.AsyncSession();

        var exception = await Record.ExceptionAsync(
            async () =>
            {
                await using var transaction = await session.BeginTransactionAsync(
                    config => config.WithTimeout(TimeSpan.FromSeconds(1)));

                var cursor = await transaction.RunAsync(SlowQuery);
                await cursor.ConsumeAsync();
            });

        exception.Should().BeAssignableTo<Neo4jException>().Which.Code.Should().Contain("TransactionTimedOut");
    }
}
