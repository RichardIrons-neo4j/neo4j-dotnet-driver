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
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Neo4j.Driver.Internal.QueryApi;
using Neo4j.Driver.Internal.Result;
using Neo4j.Driver.Tests.Internal.Core;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

public class QueryApiResultSummaryFactoryTests
{
    private static readonly Query AnyQuery = new("RETURN 1");

    private readonly AutoMocker _autoMocker = AutoMockerExtensions.ForTesting<QueryApiResultSummaryFactory>();

    private static QueryApiResultBody BodyWith(string[] fields, int rowCount)
    {
        var row = new[] { JsonSerializer.SerializeToElement(1) };
        return new QueryApiResultBody
        {
            Data = new QueryApiDataBody { Fields = fields, Values = [.. Enumerable.Repeat(row, rowCount)] }
        };
    }

    [Fact]
    public void Create_CarriesTheQueryServerAndDatabase()
    {
        var server = _autoMocker.GetMock<IServerInfo>().Object;
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();

        var summary = subject.Create(AnyQuery, "movies", new QueryApiResultBody());

        summary.Query.Should().BeSameAs(AnyQuery);
        summary.Server.Should().BeSameAs(server);
        summary.Database.Name.Should().Be("movies");
    }

    [Fact]
    public void Create_UsesTheDefaults_WhenTheBodyHasNoSummaryFields()
    {
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();

        var summary = subject.Create(AnyQuery, "neo4j", BodyWith(["n"], 1));

        summary.ResultAvailableAfter.Should().Be(TimeSpan.FromMilliseconds(-1));
        summary.ResultConsumedAfter.Should().Be(TimeSpan.FromMilliseconds(-1));
        summary.QueryType.Should().Be(QueryType.Unknown);
        summary.Counters.Should().BeEquivalentTo(new Counters());
        summary.HasPlan.Should().BeFalse();
        summary.HasProfile.Should().BeFalse();
    }

    [Fact]
    public void Create_TakesTheCountsFromTheBody()
    {
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();
        var body = new QueryApiResultBody
        {
            Counters = new QueryApiCountersBody
            {
                NodesCreated = 1007,
                NodesDeleted = 1008,
                RelationshipsCreated = 1010,
                RelationshipsDeleted = 1011,
                PropertiesSet = 1009,
                LabelsAdded = 1005,
                LabelsRemoved = 1006,
                IndexesAdded = 1003,
                IndexesRemoved = 1004,
                ConstraintsAdded = 1001,
                ConstraintsRemoved = 1002,
                SystemUpdates = 1012,
                ContainsUpdates = true,
                ContainsSystemUpdates = true
            }
        };

        var summary = subject.Create(AnyQuery, "neo4j", body);

        summary.Counters.Should().BeEquivalentTo(
            new Counters(1007, 1008, 1010, 1011, 1009, 1005, 1006, 1003, 1004, 1001, 1002, 1012, true, true));
    }

    [Fact]
    public void Create_TakesTheUpdateFlagsFromTheBody_RatherThanFromTheCounts()
    {
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();
        var body = new QueryApiResultBody
        {
            Counters = new QueryApiCountersBody { ContainsUpdates = true, ContainsSystemUpdates = true }
        };

        var summary = subject.Create(AnyQuery, "neo4j", body);

        summary.Counters.ContainsUpdates.Should().BeTrue();
        summary.Counters.ContainsSystemUpdates.Should().BeTrue();
    }

    [Fact]
    public void Create_TakesTheTimersFromTheBody()
    {
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();
        var body = new QueryApiResultBody { ResultAvailableAfter = 1337, ResultConsumedAfter = 42 };

        var summary = subject.Create(AnyQuery, "neo4j", body);

        summary.ResultAvailableAfter.Should().Be(TimeSpan.FromMilliseconds(1337));
        summary.ResultConsumedAfter.Should().Be(TimeSpan.FromMilliseconds(42));
    }

    [Theory]
    [InlineData("r", QueryType.ReadOnly)]
    [InlineData("rw", QueryType.ReadWrite)]
    [InlineData("w", QueryType.WriteOnly)]
    [InlineData("s", QueryType.SchemaWrite)]
    public void Create_TakesTheQueryTypeFromTheBody(string wireQueryType, QueryType expected)
    {
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();
        var body = new QueryApiResultBody { QueryType = wireQueryType };

        var summary = subject.Create(AnyQuery, "neo4j", body);

        summary.QueryType.Should().Be(expected);
    }

    [Fact]
    public void Create_TakesThePlanFromTheBody_DecodingEachArgument()
    {
        _autoMocker.GetMock<IJsonValueDecoder>()
            .Setup(x => x.Decode(It.IsAny<JsonElement>()))
            .Returns<JsonElement>(element => $"decoded {element.GetString()}");
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();
        var body = new QueryApiResultBody
        {
            QueryPlan = new QueryApiPlanBody
            {
                OperatorType = "ProduceResults@neo4j",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["planner"] = JsonSerializer.SerializeToElement("COST")
                },
                Identifiers = ["n"],
                Children =
                [
                    new QueryApiPlanBody
                    {
                        OperatorType = "Create@neo4j",
                        Arguments = new Dictionary<string, JsonElement>
                        {
                            ["Details"] = JsonSerializer.SerializeToElement("(n)")
                        },
                        Identifiers = ["n"],
                        Children = []
                    }
                ]
            }
        };

        var summary = subject.Create(AnyQuery, "neo4j", body);

        summary.Plan.Should().BeEquivalentTo(new
        {
            OperatorType = "ProduceResults@neo4j",
            Arguments = new Dictionary<string, object> { ["planner"] = "decoded COST" },
            Identifiers = new[] { "n" },
            Children = new[]
            {
                new
                {
                    OperatorType = "Create@neo4j",
                    Arguments = new Dictionary<string, object> { ["Details"] = "decoded (n)" },
                    Identifiers = new[] { "n" },
                    Children = Array.Empty<object>()
                }
            }
        });
    }

    [Theory]
    [InlineData(new[] { "n" }, 1, "00000")]
    [InlineData(new[] { "n" }, 0, "02000")]
    [InlineData(new string[0], 0, "00001")]
    public void Create_GqlStatusReflectsWhetherTheResultHadKeysAndRecords(
        string[] fields,
        int rowCount,
        string expectedStatus)
    {
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();

        var summary = subject.Create(AnyQuery, "neo4j", BodyWith(fields, rowCount));

        summary.GqlStatusObjects.Should().ContainSingle().Which.GqlStatus.Should().Be(expectedStatus);
    }
}
