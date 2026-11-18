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

using System;
using System.Collections.Generic;
using FluentAssertions;
using Neo4j.Driver.Internal.QueryApi;
using Xunit;
using static Neo4j.Driver.Tests.Internal.QueryApi.QueryApiCodecAssert;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

public class QueryApiSystemDateTimeOffsetCodecTests
{
    private readonly QueryApiSystemDateTimeOffsetCodec _subject = new();

    public static IEnumerable<object[]> WriteCases() =>
    [
        [new DateTimeOffset(2024, 3, 15, 10, 20, 30, TimeSpan.FromMinutes(90)), "2024-03-15T10:20:30+01:30"],
        [new DateTimeOffset(2024, 3, 15, 10, 20, 30, TimeSpan.FromMinutes(-150)), "2024-03-15T10:20:30-02:30"],
        [new DateTimeOffset(2024, 3, 15, 10, 20, 30, TimeSpan.Zero), "2024-03-15T10:20:30Z"],
        [
            new DateTimeOffset(2024, 3, 15, 10, 20, 30, TimeSpan.FromMinutes(90)).AddTicks(1234567),
            "2024-03-15T10:20:30.123456700+01:30"
        ]
    ];

    [Theory]
    [MemberData(nameof(WriteCases))]
    public void Write_ProducesAnOffsetDateTimeEnvelope(DateTimeOffset value, string expectedValue)
    {
        var result = QueryApiSystemTemporalEncoder.Encode(_subject, value);

        result.Should().BeTypedEnvelope("OffsetDateTime", expectedValue);
    }

    [Fact]
    public void CanRead_NoTypeNames() => CanRead(_subject);

    [Fact]
    public void CanWrite_TrueForDateTimeOffset() => _subject.CanWrite(DateTimeOffset.UtcNow).Should().BeTrue();

    [Fact]
    public void CanWrite_FalseForOtherTypes() => CanWrite(_subject);
}
