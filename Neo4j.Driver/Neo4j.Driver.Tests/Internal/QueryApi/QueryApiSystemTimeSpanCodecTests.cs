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
using FluentAssertions;
using Neo4j.Driver.Internal.QueryApi;
using Xunit;
using static Neo4j.Driver.Tests.Internal.QueryApi.QueryApiCodecAssert;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

public class QueryApiSystemTimeSpanCodecTests
{
    private readonly QueryApiSystemTimeSpanCodec _subject = new();

    public static IEnumerable<object[]> WriteCases() =>
    [
        [TimeSpan.Zero, "00:00:00"],
        [new TimeSpan(13, 45, 30), "13:45:30"],
        [new TimeSpan(13, 45, 30).Add(TimeSpan.FromTicks(1234567)), "13:45:30.123456700"],
        [TimeSpan.FromDays(1) - TimeSpan.FromTicks(1), "23:59:59.999999900"]
    ];

    public static IEnumerable<object[]> NotATimeOfDay() =>
    [
        [TimeSpan.FromTicks(-1)],
        [TimeSpan.FromDays(1)],
        [TimeSpan.FromDays(2)]
    ];

    [Theory]
    [MemberData(nameof(WriteCases))]
    public void Write_ProducesALocalTimeEnvelope(TimeSpan value, string expectedValue)
    {
        var result = QueryApiSystemTemporalEncoder.Encode(_subject, value);

        result.Should().BeTypedEnvelope("LocalTime", expectedValue);
    }

    [Theory]
    [MemberData(nameof(NotATimeOfDay))]
    public void Write_Throws_WhenTheValueIsNotATimeOfDay(TimeSpan value)
    {
        var act = () => QueryApiSystemTemporalEncoder.Encode(_subject, value);

        act.Should().Throw<ProtocolException>();
    }

    [Fact]
    public void CanRead_NoTypeNames() => CanRead(_subject);

    [Fact]
    public void CanWrite_TrueForTimeSpan() => _subject.CanWrite(TimeSpan.Zero).Should().BeTrue();

    [Fact]
    public void CanWrite_FalseForOtherTypes() => CanWrite(_subject);
}
