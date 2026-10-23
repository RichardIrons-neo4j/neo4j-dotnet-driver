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

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Neo4j.Driver.Internal;
using Neo4j.Driver.Internal.QueryApi;
using Neo4j.Driver.Tests.Internal.Core;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

/// <summary>
/// A transaction is one unit of work, so it asks the token manager once, when it begins, and every later request in
/// it sends that token. These tests wire the real enricher, beginner and context tracker together.
/// </summary>
public class TransactionAuthTokenTests
{
    private readonly AutoMocker _autoMocker = AutoMockerExtensions.ForTesting<TransactionBeginner>();

    public TransactionAuthTokenTests()
    {
        _autoMocker.Use<IBookmarkTracker>(new BookmarkTracker(SessionConfig.Builder.Build()));
        _autoMocker.Use<IQueryApiTransactionContextTracker>(
            new QueryApiTransactionContextTracker(new LoggingContextTracker()));

        _autoMocker.GetMock<ISessionContext>()
            .SetupSequence(c => c.GetAuthTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthTokens.Bearer("first-token"))
            .ReturnsAsync(AuthTokens.Bearer("second-token"));

        _autoMocker.GetMock<IQueryApiClient>()
            .Setup(x => x.ExecuteAsync<TransactionBeginner.ResponseBody>(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryApiResult<TransactionBeginner.ResponseBody>(
                new TransactionBeginner.ResponseBody
                {
                    Transaction = new TransactionBeginner.TransactionInfo { Id = "tx-1" }
                },
                new HttpResponseMessage().Headers));
    }

    private async Task<QueryApiAuthEnricher> EnricherAfterBeginningATransactionAsync()
    {
        var enricher = _autoMocker.CreateInstance<QueryApiAuthEnricher>();

        _autoMocker.GetMock<IQueryApiRequestBuilder>()
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<IQueryApiRequestBody>(), It.IsAny<CancellationToken>()))
            .Returns(
                async () =>
                {
                    var request = new HttpRequestMessage();
                    await enricher.Enrich(request);
                    return request;
                });

        var beginner = _autoMocker.CreateInstance<TransactionBeginner>();
        await beginner.BeginAsync(new TransactionOptions(AccessMode.Write));
        return enricher;
    }

    [Fact]
    public async Task ALaterRequest_SendsTheTokenTheBeginRequestUsed_ThoughTheManagerHasIssuedAnother()
    {
        var enricher = await EnricherAfterBeginningATransactionAsync();
        var laterRequest = new HttpRequestMessage();

        await enricher.Enrich(laterRequest);

        laterRequest.Headers.Authorization!.Parameter.Should().Be("first-token");
    }

    [Fact]
    public async Task TheManagerIsAskedOnce_ForABeginAndALaterRequest()
    {
        var enricher = await EnricherAfterBeginningATransactionAsync();

        await enricher.Enrich(new HttpRequestMessage());

        _autoMocker.GetMock<ISessionContext>()
            .Verify(c => c.GetAuthTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
