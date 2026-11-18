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
using System.Diagnostics;
using Neo4j.Driver.Internal;
using Neo4j.Driver.Preview.QueryApi;
using Neo4j.Driver.Tests.TestUtil;

namespace Neo4j.Driver.IntegrationTests.Internals;

public static class DefaultInstallation
{
    public const string BoltHeader = "bolt://";
    public const string QueryApiDatabase = "neo4j";
    public static readonly string User = "neo4j";
    public static readonly string Password = "neo4j";
    public static readonly string BoltHost = "127.0.0.1";
    public static readonly string BoltPort = "7687";

    static DefaultInstallation()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST_NEO4J_USER")))
        {
            User = Environment.GetEnvironmentVariable("TEST_NEO4J_USER");
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST_NEO4J_PASS")))
        {
            Password = Environment.GetEnvironmentVariable("TEST_NEO4J_PASS");
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST_NEO4J_HOST")))
        {
            BoltHost = Environment.GetEnvironmentVariable("TEST_NEO4J_HOST");
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST_NEO4J_PORT")))
        {
            BoltPort = Environment.GetEnvironmentVariable("TEST_NEO4J_PORT");
        }
    }

    public static string BoltUri => BoltHeader + BoltHost + ":" + BoltPort;

    public static string HttpUri
    {
        get
        {
            if (!UseQueryApi)
            {
                return "http://127.0.0.1:7474";
            }

            return $"{TestScheme.ToLowerInvariant()}://{BoltHost}:{BoltPort}";
        }
    }

    private static string TestScheme => Environment.GetEnvironmentVariable("TEST_NEO4J_SCHEME") ?? "";

    /// <summary>
    /// True when <c>TEST_NEO4J_SCHEME</c> is <c>http</c> or <c>https</c>, which is how testkit asks for
    /// the suites to run over the HTTP Query API instead of Bolt.
    /// </summary>
    public static bool UseQueryApi =>
        TestScheme.Equals("http", StringComparison.OrdinalIgnoreCase)
        || TestScheme.Equals("https", StringComparison.OrdinalIgnoreCase);

    public static IDriver CreateDriver(Uri uri, IAuthToken authToken, Action<ConfigBuilder> configAction = null)
    {
        var driver = GraphDatabase.Driver(
            uri,
            authToken,
            o =>
            {
                configAction?.Invoke(o);
                o.EnableQueryApi();
            });
        return uri.Scheme is "http" or "https"
            ? new DefaultDatabaseDriver((IInternalDriver)driver, QueryApiDatabase)
            : driver;
    }

    public static IDriver NewDriver(Uri uri, IAuthToken authToken)
    {
        var configuredLevelStr = Environment.GetEnvironmentVariable("NEOLOGLEVEL");
        var logger = Enum.TryParse<ExtendedLogLevel>(configuredLevelStr ?? "", true, out var configuredLevel)
            ? new TestNeo4JLogger(Console.WriteLine, configuredLevel)
            : new TestNeo4JLogger(s => Debug.WriteLine(s), ExtendedLogLevel.Debug);

        return CreateDriver(uri, authToken, o => { o.WithLogger(logger); });
    }
}
