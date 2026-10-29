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
using Moq.AutoMock;
using Neo4j.Driver.Internal.QueryApi;
using Neo4j.Driver.Tests.Internal.Core;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

public class QueryApiResultSummaryFactoryTests
{
    private static readonly Query AnyQuery = new("RETURN 1");

    private readonly AutoMocker _autoMocker = AutoMockerExtensions.ForTesting<QueryApiResultSummaryFactory>();

    [Fact]
    public void Create_CarriesTheQueryServerAndDatabase()
    {
        var server = _autoMocker.GetMock<IServerInfo>().Object;
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();

        var summary = subject.Create(AnyQuery, "movies");

        summary.Query.Should().BeSameAs(AnyQuery);
        summary.Server.Should().BeSameAs(server);
        summary.Database.Name.Should().Be("movies");
    }
}
