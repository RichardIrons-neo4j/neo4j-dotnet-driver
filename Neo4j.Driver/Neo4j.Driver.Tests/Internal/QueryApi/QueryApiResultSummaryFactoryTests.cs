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

    private static readonly QueryApiNotificationBody InformationWithPosition = new()
    {
        Code = "Neo.ClientNotification.Statement.UnboundedVariableLengthPattern",
        Title = "The provided pattern is unbounded.",
        Description = "Using shortest path with an unbounded pattern will likely be slow.",
        Severity = "INFORMATION",
        Category = "PERFORMANCE",
        Position = new QueryApiPositionBody { Offset = 21, Line = 1, Column = 22 }
    };

    private static readonly QueryApiNotificationBody WarningWithoutPosition = new()
    {
        Code = "Neo.ClientNotification.Statement.JoinHintUnfulfillableWarning",
        Title = "The database was unable to plan a hinted join.",
        Description = "The hinted join was not planned.",
        Severity = "WARNING",
        Category = "HINT"
    };

    private readonly AutoMocker _autoMocker = AutoMocker.ForTesting<QueryApiResultSummaryFactory>();

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

    [Fact]
    public void Create_TakesTheProfileFromTheBody_KeepingAbsentStatsAsNull()
    {
        _autoMocker.GetMock<IJsonValueDecoder>()
            .Setup(x => x.Decode(It.IsAny<JsonElement>()))
            .Returns<JsonElement>(element => $"decoded {element.GetString()}");
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();
        var body = new QueryApiResultBody
        {
            ProfiledQueryPlan = new QueryApiPlanBody
            {
                OperatorType = "ProduceResults@neo4j",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["planner"] = JsonSerializer.SerializeToElement("COST")
                },
                Identifiers = ["n"],
                DbHits = 1,
                Records = 2,
                PageCacheHits = 3,
                PageCacheMisses = 4,
                PageCacheHitRatio = 0.5,
                Time = 6,
                Children = [new QueryApiPlanBody { OperatorType = "Create@neo4j", Identifiers = ["n"] }]
            }
        };

        var summary = subject.Create(AnyQuery, "neo4j", body);

        summary.QueryProfile.Should().BeEquivalentTo(new
        {
            OperatorType = "ProduceResults@neo4j",
            Arguments = new Dictionary<string, object> { ["planner"] = "decoded COST" },
            Identifiers = new[] { "n" },
            DbHits = 1L,
            Rows = 2L,
            PageCacheHits = 3L,
            PageCacheMisses = 4L,
            PageCacheHitRatio = 0.5,
            Time = 6L,
            Children = new[]
            {
                new
                {
                    OperatorType = "Create@neo4j",
                    Arguments = new Dictionary<string, object>(),
                    Identifiers = new[] { "n" },
                    DbHits = (long?)null,
                    Rows = (long?)null,
                    PageCacheHits = (long?)null,
                    PageCacheMisses = (long?)null,
                    PageCacheHitRatio = (double?)null,
                    Time = (long?)null,
                    Children = Array.Empty<object>()
                }
            }
        });
    }

    [Fact]
    public void Create_TakesTheObsoleteProfileFromTheBody_ZeroingAbsentStats_AndReportingPageCacheStatsWhenPresent()
    {
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();
        var body = new QueryApiResultBody
        {
            ProfiledQueryPlan = new QueryApiPlanBody
            {
                OperatorType = "ProduceResults@neo4j",
                Identifiers = ["n"],
                DbHits = 1,
                Records = 2,
                PageCacheHits = 0,
                PageCacheMisses = 0,
                PageCacheHitRatio = 0.0,
                Time = 6,
                Children = [new QueryApiPlanBody { OperatorType = "Create@neo4j", Identifiers = ["n"] }]
            }
        };

        var summary = subject.Create(AnyQuery, "neo4j", body);

#pragma warning disable CS0618
        summary.Profile.Should().BeEquivalentTo(new
        {
            OperatorType = "ProduceResults@neo4j",
            Arguments = new Dictionary<string, object>(),
            Identifiers = new[] { "n" },
            DbHits = 1L,
            Records = 2L,
            HasPageCacheStats = true,
            PageCacheHits = 0L,
            PageCacheMisses = 0L,
            PageCacheHitRatio = 0.0,
            Time = 6L,
            Children = new[]
            {
                new
                {
                    OperatorType = "Create@neo4j",
                    Arguments = new Dictionary<string, object>(),
                    Identifiers = new[] { "n" },
                    DbHits = 0L,
                    Records = 0L,
                    HasPageCacheStats = false,
                    PageCacheHits = 0L,
                    PageCacheMisses = 0L,
                    PageCacheHitRatio = 0.0,
                    Time = 0L,
                    Children = Array.Empty<object>()
                }
            }
        });
#pragma warning restore CS0618
    }

    [Fact]
    public void Create_TakesTheNotificationsFromTheBody()
    {
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();
        var body = new QueryApiResultBody { Notifications = [InformationWithPosition, WarningWithoutPosition] };

        var summary = subject.Create(AnyQuery, "neo4j", body);

#pragma warning disable CS0618
        summary.Notifications.Should().BeEquivalentTo(
            new[]
            {
                new
                {
                    Code = "Neo.ClientNotification.Statement.UnboundedVariableLengthPattern",
                    Title = "The provided pattern is unbounded.",
                    Description = "Using shortest path with an unbounded pattern will likely be slow.",
                    RawSeverityLevel = "INFORMATION",
                    RawCategory = "PERFORMANCE",
                    Position = (InputPosition?)new InputPosition(21, 1, 22)
                },
                new
                {
                    Code = "Neo.ClientNotification.Statement.JoinHintUnfulfillableWarning",
                    Title = "The database was unable to plan a hinted join.",
                    Description = "The hinted join was not planned.",
                    RawSeverityLevel = "WARNING",
                    RawCategory = "HINT",
                    Position = (InputPosition?)null
                }
            },
            options => options.WithStrictOrdering());
#pragma warning restore CS0618
    }

    [Fact]
    public void Create_FillsAbsentNotificationTextWithEmptyStrings()
    {
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();
        var body = new QueryApiResultBody
        {
            Notifications = [new QueryApiNotificationBody { Code = "Neo.ClientNotification.Foo.Bar" }]
        };

        var summary = subject.Create(AnyQuery, "neo4j", body);

#pragma warning disable CS0618
        summary.Notifications.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Code = "Neo.ClientNotification.Foo.Bar",
            Title = "",
            Description = "",
            RawSeverityLevel = "",
            RawCategory = (string?)null,
            Position = (InputPosition?)null
        });
#pragma warning restore CS0618
    }

    [Fact]
    public void Create_ReportsEachNotificationAsAGqlStatus_InPrecedenceOrderAroundTheResultStatus()
    {
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();
        var body = BodyWith(["n"], 1) with { Notifications = [InformationWithPosition, WarningWithoutPosition] };

        var summary = subject.Create(AnyQuery, "neo4j", body);

        summary.GqlStatusObjects.Select(status => status.GqlStatus).Should().Equal("01N42", "00000", "03N42");
        summary.GqlStatusObjects[0].Should().BeEquivalentTo(new
        {
            StatusDescription = "The hinted join was not planned.",
            Position = (InputPosition?)null,
            RawClassification = "HINT",
            RawSeverity = "WARNING",
            DiagnosticRecord = new Dictionary<string, object>
            {
                ["CURRENT_SCHEMA"] = "/",
                ["OPERATION"] = "",
                ["OPERATION_CODE"] = "0",
                ["_severity"] = "WARNING",
                ["_classification"] = "HINT"
            },
            Title = "The database was unable to plan a hinted join.",
            IsNotification = true
        });
        summary.GqlStatusObjects[2].Should().BeEquivalentTo(new
        {
            StatusDescription = "Using shortest path with an unbounded pattern will likely be slow.",
            Position = (InputPosition?)new InputPosition(21, 1, 22),
            RawClassification = "PERFORMANCE",
            RawSeverity = "INFORMATION",
            DiagnosticRecord = new Dictionary<string, object>
            {
                ["CURRENT_SCHEMA"] = "/",
                ["OPERATION"] = "",
                ["OPERATION_CODE"] = "0",
                ["_severity"] = "INFORMATION",
                ["_classification"] = "PERFORMANCE",
                ["_position"] = new Dictionary<string, object> { ["offset"] = 21L, ["line"] = 1L, ["column"] = 22L }
            },
            Title = "The provided pattern is unbounded.",
            IsNotification = true
        });
    }

    [Theory]
    [InlineData("WARNING", "warn: unknown warning")]
    [InlineData("INFORMATION", "info: unknown notification")]
    public void Create_GivesANotificationsStatusTheDefaultDescription_WhenTheNotificationHasNone(
        string severity,
        string expectedDescription)
    {
        var subject = _autoMocker.CreateInstance<QueryApiResultSummaryFactory>();
        var notification = new QueryApiNotificationBody { Code = "Neo.ClientNotification.Foo.Bar", Severity = severity };
        var body = new QueryApiResultBody { Notifications = [notification] };

        var summary = subject.Create(AnyQuery, "neo4j", body);

        summary.GqlStatusObjects.OfType<GqlStatusObject>().Should().ContainSingle(status => status.IsNotification)
            .Which.StatusDescription.Should().Be(expectedDescription);
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
