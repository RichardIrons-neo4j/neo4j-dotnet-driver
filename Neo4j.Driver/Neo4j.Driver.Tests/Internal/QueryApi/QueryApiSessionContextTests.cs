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
#nullable enable

using System;
using FluentAssertions;
using Moq.AutoMock;
using Neo4j.Driver.Internal.QueryApi;
using Neo4j.Driver.Tests.Internal.Core;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

/// <summary>
/// The database name becomes part of the request URL path, so the Query API only accepts names matching
/// <c>[a-zA-Z0-9.-]{3,}</c>, and has no home database to fall back to when none is configured.
/// </summary>
public class QueryApiSessionContextTests
{
    private readonly AutoMocker _autoMocker = AutoMockerExtensions.ForTesting<QueryApiSessionContext>();

    private QueryApiSessionContext SessionContextFor(string? database)
    {
        var builder = SessionConfig.Builder;
        if (database is not null)
        {
            builder.WithDatabase(database);
        }

        _autoMocker.Use(builder.Build());
        return _autoMocker.CreateInstance<QueryApiSessionContext>();
    }

    [Theory]
    [InlineData("aaa")]
    [InlineData("aaaaa")]
    [InlineData("-----")]
    [InlineData(".....")]
    [InlineData("a-.AzZ")]
    [InlineData("AabcdefghijklmnopqrstuvwxyzZ")]
    [InlineData("aABCDEFGHIJKLMNOPQRSTUVWXYZz")]
    public void Database_ReturnsTheConfiguredName_WhenTheQueryApiAcceptsIt(string database)
    {
        var subject = SessionContextFor(database);

        var result = subject.Database;

        result.Should().Be(database);
    }

    [Theory]
    [InlineData("aa_aa")]
    [InlineData("aa/aa")]
    [InlineData("aa~aa")]
    [InlineData("aa,aa")]
    [InlineData("aa;aa")]
    [InlineData("aa=aa")]
    [InlineData("aa+aa")]
    [InlineData("aa?aa")]
    [InlineData("aa!aa")]
    [InlineData("aa'aa")]
    [InlineData("aa\"aa")]
    [InlineData("aa`aa")]
    [InlineData("aa%61aa")]
    [InlineData("aa\\aa")]
    [InlineData("aa aa")]
    [InlineData("aa🔥aa")]
    [InlineData("aaäaa")]
    [InlineData("aa")]
    [InlineData("a")]
    public void Database_Throws_AnErrorNamingTheDatabase_WhenTheQueryApiDoesNotAcceptIt(string database)
    {
        var subject = SessionContextFor(database);

        var act = () => subject.Database;

        var thrown = act.Should().Throw<ClientException>().Which;
        thrown.Message.Should().Contain("Query API");
        thrown.Message.Should().Contain("database name");
        thrown.Message.Should().Contain(database);
    }

    [Theory]
    [InlineData("aa\naa")]
    [InlineData("aa\taa")]
    [InlineData("aa\0aa")]
    [InlineData("aaa\n")]
    public void Database_Throws_WhenTheNameContainsAControlCharacter(string database)
    {
        var subject = SessionContextFor(database);

        var act = () => subject.Database;

        var thrown = act.Should().Throw<ClientException>().Which;
        thrown.Message.Should().Contain("Query API");
        thrown.Message.Should().Contain("database name");
    }

    [Fact]
    public void Database_Throws_WhenNoDatabaseIsConfigured()
    {
        var subject = SessionContextFor(null);

        var act = () => subject.Database;

        var thrown = act.Should().Throw<ClientException>().Which;
        thrown.Message.Should().Contain("Query API");
        thrown.Message.Should().Contain("database name");
    }
}
