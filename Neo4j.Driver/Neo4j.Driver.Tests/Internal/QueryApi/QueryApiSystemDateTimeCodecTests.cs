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

public class QueryApiSystemDateTimeCodecTests
{
    private readonly QueryApiSystemDateTimeCodec _subject = new();

    public static IEnumerable<object[]> WriteCases() =>
    [
        [new DateTime(2024, 3, 15, 10, 20, 30, DateTimeKind.Unspecified), "LocalDateTime", "2024-03-15T10:20:30"],
        [new DateTime(2024, 3, 15, 10, 20, 30, DateTimeKind.Local), "LocalDateTime", "2024-03-15T10:20:30"],
        [
            new DateTime(2024, 3, 15, 10, 20, 30, DateTimeKind.Unspecified).AddTicks(1234567),
            "LocalDateTime", "2024-03-15T10:20:30.123456700"
        ],
        [new DateTime(2024, 3, 15, 10, 20, 30, DateTimeKind.Utc), "OffsetDateTime", "2024-03-15T10:20:30Z"],
        [
            new DateTime(2024, 3, 15, 10, 20, 30, DateTimeKind.Utc).AddTicks(1234567),
            "OffsetDateTime", "2024-03-15T10:20:30.123456700Z"
        ]
    ];

    [Theory]
    [MemberData(nameof(WriteCases))]
    public void Write_ProducesTheEnvelopeForTheNeo4jType(DateTime value, string expectedType, string expectedValue)
    {
        var result = QueryApiSystemTemporalEncoder.Encode(_subject, value);

        result.Should().BeTypedEnvelope(expectedType, expectedValue);
    }

    [Fact]
    public void CanRead_NoTypeNames() => CanRead(_subject);

    [Fact]
    public void CanWrite_TrueForDateTime() => _subject.CanWrite(DateTime.UtcNow).Should().BeTrue();

    [Fact]
    public void CanWrite_FalseForOtherTypes() => CanWrite(_subject);
}
