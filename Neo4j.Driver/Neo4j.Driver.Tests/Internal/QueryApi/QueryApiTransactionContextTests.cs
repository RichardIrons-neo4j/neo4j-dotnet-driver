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

using FluentAssertions;
using Neo4j.Driver.Internal.QueryApi;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

public class QueryApiTransactionContextTests
{
    [Fact]
    public void ToString_LeavesTheTokenOut_BecauseTheContextIsWrittenToTheLog()
    {
        var subject = new QueryApiTransactionContext("tx-1", "shard-9", AuthTokens.Basic("alice", "s3cret"));

        var text = subject.ToString();

        text.Should().Be("tx-1/shard-9");
    }

    [Fact]
    public void ToString_IsJustTheTransactionId_WhenThereIsNoClusterAffinity()
    {
        var subject = new QueryApiTransactionContext("tx-1", null, AuthTokens.Basic("alice", "s3cret"));

        var text = subject.ToString();

        text.Should().Be("tx-1");
    }
}
