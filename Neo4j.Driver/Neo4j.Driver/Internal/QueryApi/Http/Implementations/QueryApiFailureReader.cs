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

using System.Net;
using System.Net.Http;
using Neo4j.Driver.Internal.Messaging;
using Neo4j.Driver.Internal.ExceptionHandling;

namespace Neo4j.Driver.Internal.QueryApi;

internal class QueryApiFailureReader : IQueryApiFailureReader
{
    private readonly INeo4jExceptionFactory _exceptionFactory;
    private readonly ILogger _logger;

    public QueryApiFailureReader(INeo4jExceptionFactory exceptionFactory, ILogger logger)
    {
        _exceptionFactory = exceptionFactory;
        _logger = logger;
    }

    public Neo4jException? Read(HttpResponseMessage response, string responseText, QueryApiErrorBody[]? errors)
    {
        if (errors is { Length: > 0 })
        {
            var last = errors[^1];
            return _exceptionFactory.GetException(new FailureMessage(last.Code, last.Message));
        }

        if (response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return _exceptionFactory.GetException(
                new FailureMessage(
                    "Neo.ClientError.Security.Unauthorized",
                    response.ReasonPhrase ?? "Unauthorized"));
        }

        var method = response.RequestMessage?.Method;
        var uri = response.RequestMessage?.RequestUri;
        var message = $"HTTP {(int)response.StatusCode} {method} {uri}: {responseText}";

        _logger.LogDebug("{message}", message);
        return new ServiceUnavailableException(message);
    }
}
