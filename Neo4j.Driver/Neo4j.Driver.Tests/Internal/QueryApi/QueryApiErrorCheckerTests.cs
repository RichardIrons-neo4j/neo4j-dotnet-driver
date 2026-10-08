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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Neo4j.Driver.Internal;
using Neo4j.Driver.Internal.ExceptionHandling;
using Neo4j.Driver.Internal.GqlCompliance;
using Neo4j.Driver.Internal.QueryApi;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

/// <summary>
/// The Query API returns 202 Accepted on success and 401 Unauthorized on authentication failure.
/// Application-level errors (e.g. syntax errors) appear in the response body at 202. Spec:
/// https://neo4j.com/docs/query-api/current/#_response_status_codes
/// </summary>
public class QueryApiErrorCheckerTests
{
    private static QueryApiErrorChecker BuildChecker(IJsonDeserializer? deserializer = null) =>
        new(
            deserializer ?? new Mock<IJsonDeserializer>().Object,
            new QueryApiFailureReader(new Neo4jExceptionFactory(new GqlErrorDefaults()), new Mock<ILogger>().Object));

    private static HttpResponseMessage StringResponse(HttpStatusCode status, string body = "") =>
        new(status) { Content = new StringContent(body) };

    public class EnsureSuccessAsync
    {
        [Fact]
        public async Task DoesNotThrow_WhenStatusIs202Accepted()
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted);
            await BuildChecker().Invoking(x => x.EnsureSuccessAsync(response)).Should().NotThrowAsync();
        }

        [Fact]
        public async Task DoesNotThrow_WhenStatusIs200Ok()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            await BuildChecker().Invoking(x => x.EnsureSuccessAsync(response)).Should().NotThrowAsync();
        }

        [Fact]
        public async Task ThrowsAuthenticationException_WhenStatusIs401_AndBodyContainsErrorCode()
        {
            const string responseBody = "test-response-body";
            var errorBody = new QueryApiErrorChecker.ErrorResponseBody
            {
                Errors =
                [
                    new QueryApiErrorBody
                    {
                        Code = "Neo.ClientError.Security.Unauthorized",
                        Message = "No authentication was supplied."
                    }
                ]
            };

            var mockDeserializer = new Mock<IJsonDeserializer>();
            mockDeserializer
                .Setup(x => x.DeserializeAsync<QueryApiErrorChecker.ErrorResponseBody>(
                    responseBody, It.IsAny<CancellationToken>()))
                .ReturnsAsync(errorBody);

            var response = StringResponse(HttpStatusCode.Unauthorized, responseBody);

            await BuildChecker(mockDeserializer.Object)
                .Invoking(x => x.EnsureSuccessAsync(response))
                .Should().ThrowAsync<AuthenticationException>()
                .WithMessage("*No authentication was supplied.*");
        }

        [Fact]
        public async Task ThrowsAuthenticationException_WhenStatusIs401_AndBodyIsUnparseable()
        {
            const string responseBody = "not json";

            var mockDeserializer = new Mock<IJsonDeserializer>();
            mockDeserializer
                .Setup(x => x.DeserializeAsync<QueryApiErrorChecker.ErrorResponseBody>(
                    responseBody, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new JsonException("Invalid JSON"));

            var response = StringResponse(HttpStatusCode.Unauthorized, responseBody);
            response.ReasonPhrase = "Unauthorized";

            await BuildChecker(mockDeserializer.Object)
                .Invoking(x => x.EnsureSuccessAsync(response))
                .Should().ThrowAsync<AuthenticationException>();
        }

        [Fact]
        public async Task ThrowsClientException_WhenStatusIs400_AndBodyContainsErrorCode()
        {
            const string responseBody = "test-response-body";
            var errorBody = new QueryApiErrorChecker.ErrorResponseBody
            {
                Errors =
                [
                    new QueryApiErrorBody
                    {
                        Code = "Neo.ClientError.Statement.SyntaxError",
                        Message = "Invalid input 'Invalid': expected ..."
                    }
                ]
            };

            var mockDeserializer = new Mock<IJsonDeserializer>();
            mockDeserializer
                .Setup(x => x.DeserializeAsync<QueryApiErrorChecker.ErrorResponseBody>(
                    responseBody, It.IsAny<CancellationToken>()))
                .ReturnsAsync(errorBody);

            var response = StringResponse(HttpStatusCode.BadRequest, responseBody);

            await BuildChecker(mockDeserializer.Object)
                .Invoking(x => x.EnsureSuccessAsync(response))
                .Should().ThrowAsync<ClientException>()
                .WithMessage("*Invalid input*");
        }

        [Fact]
        public async Task ThrownException_CarriesDefaultGqlFields_WhenStatusIs400()
        {
            const string responseBody = "test-response-body";
            var errorBody = new QueryApiErrorChecker.ErrorResponseBody
            {
                Errors =
                [
                    new QueryApiErrorBody
                    {
                        Code = "Neo.ClientError.Statement.SyntaxError",
                        Message = "Invalid input"
                    }
                ]
            };

            var mockDeserializer = new Mock<IJsonDeserializer>();
            mockDeserializer
                .Setup(x => x.DeserializeAsync<QueryApiErrorChecker.ErrorResponseBody>(
                    responseBody, It.IsAny<CancellationToken>()))
                .ReturnsAsync(errorBody);

            var response = StringResponse(HttpStatusCode.BadRequest, responseBody);

            var act = () => BuildChecker(mockDeserializer.Object).EnsureSuccessAsync(response);

            var thrown = (await act.Should().ThrowAsync<Neo4jException>()).Which;
            thrown.GqlStatus.Should().Be("50N42");
            thrown.GqlClassification.Should().Be("UNKNOWN");
            thrown.GqlDiagnosticRecord.Should().Contain("OPERATION_CODE", "0");
        }

        [Fact]
        public async Task ThrowsTheLastError_WhenBodyContainsSeveral()
        {
            const string responseBody = "test-response-body";
            var errorBody = new QueryApiErrorChecker.ErrorResponseBody
            {
                Errors =
                [
                    new QueryApiErrorBody { Code = "Neo.ClientError.Oh.Err1", Message = "Error message 1" },
                    new QueryApiErrorBody { Code = "Neo.ClientError.Oh.Err2", Message = "Error message 2" },
                    new QueryApiErrorBody { Code = "Neo.ClientError.Oh.Err3", Message = "Error message 3" }
                ]
            };

            var mockDeserializer = new Mock<IJsonDeserializer>();
            mockDeserializer
                .Setup(x => x.DeserializeAsync<QueryApiErrorChecker.ErrorResponseBody>(
                    responseBody, It.IsAny<CancellationToken>()))
                .ReturnsAsync(errorBody);

            var response = StringResponse(HttpStatusCode.BadRequest, responseBody);

            var act = () => BuildChecker(mockDeserializer.Object).EnsureSuccessAsync(response);

            var thrown = (await act.Should().ThrowAsync<Neo4jException>()).Which;
            thrown.Code.Should().Be("Neo.ClientError.Oh.Err3");
            thrown.Message.Should().Be("Error message 3");
        }

        [Fact]
        public async Task ThrowsServiceUnavailableException_WhenStatusIsUnexpected()
        {
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

            await BuildChecker()
                .Invoking(x => x.EnsureSuccessAsync(response))
                .Should().ThrowAsync<ServiceUnavailableException>()
                .WithMessage("*503*");
        }

        [Fact]
        public async Task ThrowsServiceUnavailableException_WithStatusCode_WhenServerReturnsUnexpected5xx()
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

            await BuildChecker()
                .Invoking(x => x.EnsureSuccessAsync(response))
                .Should().ThrowAsync<ServiceUnavailableException>()
                .WithMessage("*500*");
        }
    }
}
