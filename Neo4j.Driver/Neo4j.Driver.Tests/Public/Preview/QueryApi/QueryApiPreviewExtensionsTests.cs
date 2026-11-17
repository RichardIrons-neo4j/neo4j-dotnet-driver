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
using System.Threading.Tasks;
using FluentAssertions;
using Neo4j.Driver.Preview.QueryApi;
using Xunit;

namespace Neo4j.Driver.Tests.Public.Preview.QueryApi;

public class QueryApiPreviewExtensionsTests
{
    private static readonly IAuthToken Auth = AuthTokens.Basic("neo4j", "password");

    [Theory]
    [InlineData("http://localhost:7474")]
    [InlineData("https://localhost:7473")]
    public void Driver_ForAQueryApiUriWithoutTheOptIn_ThrowsNotSupportedExceptionNamingTheOptIn(string uri)
    {
        var act = () => GraphDatabase.Driver(uri, Auth);

        act.Should().Throw<NotSupportedException>().WithMessage("*EnableQueryApi*");
    }

    [Theory]
    [InlineData("http://localhost:7474")]
    [InlineData("https://localhost:7473")]
    public async Task Driver_ForAQueryApiUriWithTheOptIn_ReturnsADriver(string uri)
    {
        await using var driver = GraphDatabase.Driver(uri, Auth, o => o.EnableQueryApi());

        driver.Should().NotBeNull();
    }
}
