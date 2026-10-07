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
using System.Linq;
using System.Reflection;
using Neo4j.Driver.Internal.GqlCompliance;
using Neo4j.Driver.Internal.Helpers;
using Neo4j.Driver.Internal.Messaging;

namespace Neo4j.Driver.Internal.ExceptionHandling;

internal class Neo4jExceptionFactory : INeo4jExceptionFactory
{
    private static readonly SimpleWildcardHelper SimpleWildcardHelper = new();
    private static readonly List<FactoryInfo> ExceptionFactories = BuildExceptionFactories();

    private readonly IGqlErrorDefaults _gqlErrorDefaults;

    public Neo4jExceptionFactory(IGqlErrorDefaults gqlErrorDefaults)
    {
        _gqlErrorDefaults = gqlErrorDefaults;
    }

    public Neo4jException GetException(FailureMessage failureMessage)
    {
        _gqlErrorDefaults.Fill(failureMessage);

        var factoryInfo = ExceptionFactories.First(
            f => SimpleWildcardHelper.StringMatches(failureMessage.CodeInternal, f.Code));

        var innerException = failureMessage.GqlCause != null ? GetException(failureMessage.GqlCause) : null;

        return factoryInfo.ExceptionFactory(failureMessage, innerException);
    }

    private static List<FactoryInfo> BuildExceptionFactories()
    {
        var codesAndExceptions = GetCodesAndExceptions();
        codesAndExceptions.Sort(CompareByCode);
        return codesAndExceptions.Select(x => new FactoryInfo(x.code, CreateFactory(x.exceptionType))).ToList();
    }

    private static List<(string code, Type exceptionType)> GetCodesAndExceptions()
    {
        return typeof(Neo4jException).Assembly
            .GetExportedTypes()
            .Where(t => typeof(Neo4jException).IsAssignableFrom(t))
            .Select(
                exceptionType => new
                {
                    exceptionType,
                    attr = exceptionType.GetCustomAttribute<ErrorCodeAttribute>()
                })
            .Where(t => t.attr is not null)
            .Select(t => (t.attr.Code, t.exceptionType))
            .ToList();
    }

    private static int CompareByCode((string code, Type exceptionType) x, (string code, Type exceptionType) y)
    {
        // x comes before y if y matches x - this would happen if:
        // x = Error.Specific
        // y = Error.*
        // this means that less-specific wildcards are at the end of the list, so the first
        // matching wildcard will always be the most specific

        if (SimpleWildcardHelper.StringMatches(x.code, y.code))
        {
            return -1;
        }

        if (SimpleWildcardHelper.StringMatches(y.code, x.code))
        {
            return 1;
        }

        // otherwise, just compare the codes
        return string.Compare(x.code, y.code, StringComparison.InvariantCultureIgnoreCase);
    }

    private static Func<FailureMessage, Exception, Neo4jException> CreateFactory(Type type)
    {
        var ctor = type.GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            [typeof(FailureMessage), typeof(Exception)],
            null);

        if (ctor is null)
        {
            throw new Neo4jException(
                $"Neo4jException type {type.FullName} does not have a constructor that takes " +
                $"a {nameof(FailureMessage)} and an {nameof(Exception)}");
        }

        return (message, inner) => (Neo4jException)ctor.Invoke([message, inner]);
    }

    private record FactoryInfo(string Code, Func<FailureMessage, Exception, Neo4jException> ExceptionFactory);
}
