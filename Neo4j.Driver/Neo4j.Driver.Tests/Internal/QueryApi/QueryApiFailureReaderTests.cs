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
using FluentAssertions;
using Moq.AutoMock;
using Neo4j.Driver.Internal.ExceptionHandling;
using Neo4j.Driver.Internal.GqlCompliance;
using Neo4j.Driver.Internal.QueryApi;
using Neo4j.Driver.Tests.Internal.Core;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

public class QueryApiFailureReaderTests
{
    private readonly AutoMocker _autoMocker = AutoMockerExtensions.ForTesting<QueryApiFailureReader>();

    public QueryApiFailureReaderTests()
    {
        _autoMocker.Use<INeo4jExceptionFactory>(new Neo4jExceptionFactory(new GqlErrorDefaults()));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Accepted)]
    public void ReturnsNull_WhenStatusIsSuccessAndThereAreNoErrors(HttpStatusCode status)
    {
        var subject = _autoMocker.CreateInstance<QueryApiFailureReader>();

        var failure = subject.Read(new HttpResponseMessage(status), "", null);

        failure.Should().BeNull();
    }

    [Fact]
    public void ReturnsNull_WhenTheErrorsArrayIsEmpty()
    {
        var subject = _autoMocker.CreateInstance<QueryApiFailureReader>();

        var failure = subject.Read(new HttpResponseMessage(HttpStatusCode.Accepted), "", []);

        failure.Should().BeNull();
    }

    [Fact]
    public void ReturnsTheLastError_WhenASuccessStatusCarriesSeveral()
    {
        var subject = _autoMocker.CreateInstance<QueryApiFailureReader>();

        var failure = subject.Read(
            new HttpResponseMessage(HttpStatusCode.Accepted),
            "",
            [
                new QueryApiErrorBody("Neo.ClientError.Oh.Err1", "Error message 1"),
                new QueryApiErrorBody("Neo.ClientError.Oh.Err2", "Error message 2"),
                new QueryApiErrorBody("Neo.ClientError.Oh.Err3", "Error message 3")
            ]);

        failure!.Code.Should().Be("Neo.ClientError.Oh.Err3");
        failure.Message.Should().Be("Error message 3");
    }

    [Fact]
    public void ReturnsTheEnvelopeError_WhenStatusIsNotSuccess()
    {
        var subject = _autoMocker.CreateInstance<QueryApiFailureReader>();

        var failure = subject.Read(
            new HttpResponseMessage(HttpStatusCode.BadRequest),
            "",
            [new QueryApiErrorBody("Neo.ClientError.Statement.SyntaxError", "Invalid input")]);

        failure.Should().BeOfType<ClientException>().Which.Message.Should().Be("Invalid input");
    }

    [Fact]
    public void ReturnsAuthenticationException_WhenStatusIs401WithoutAnEnvelope()
    {
        var subject = _autoMocker.CreateInstance<QueryApiFailureReader>();
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized) { ReasonPhrase = "Unauthorized" };

        var failure = subject.Read(response, "not json", null);

        failure.Should().BeOfType<AuthenticationException>().Which.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public void ReturnsServiceUnavailableException_WhenStatusIsUnexpectedWithoutAnEnvelope()
    {
        var subject = _autoMocker.CreateInstance<QueryApiFailureReader>();

        var failure = subject.Read(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable), "boom", null);

        failure.Should().BeOfType<ServiceUnavailableException>().Which.Message.Should().Contain("503").And.Contain("boom");
    }
}
