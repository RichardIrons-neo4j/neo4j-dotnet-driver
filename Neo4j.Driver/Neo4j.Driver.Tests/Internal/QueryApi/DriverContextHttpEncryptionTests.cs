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
using System.Net.Security;
using FluentAssertions;
using Neo4j.Driver.Internal;
using Neo4j.Driver.Internal.Connector.Trust;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

public class DriverContextHttpEncryptionTests
{
    private const string HttpUri = "http://localhost:7474";
    private const string HttpsUri = "https://localhost:7473";

    private static DriverContext CreateContext(string uri, Action<ConfigBuilder>? config = null)
    {
        return TestDriverContext.With(new Uri(uri), config: config);
    }

    [Fact]
    public void Https_UsesTlsWithChainTrust_ByDefault()
    {
        var context = CreateContext(HttpsUri);

        var encryption = context.EncryptionManager;

        encryption.Should().NotBeNull();
        encryption!.UseTls.Should().BeTrue();
        encryption.TrustManager.Should().BeOfType<ChainTrustManager>();
    }

    [Fact]
    public void Https_UsesTheConfiguredTrustManager()
    {
        var trustManager = TrustManager.CreateInsecure();

        var context = CreateContext(HttpsUri, builder => builder.WithTrustManager(trustManager));

        var encryption = context.EncryptionManager;

        encryption.Should().NotBeNull();
        encryption!.TrustManager.Should().BeSameAs(trustManager);
    }

    [Fact]
    public void Http_WithAnEncryptionLevel_Throws()
    {
        var act = () => CreateContext(HttpUri, builder => builder.WithEncryptionLevel(EncryptionLevel.Encrypted));

        act.Should().Throw<ArgumentException>().WithMessage("*conflicting*");
    }

    [Fact]
    public void Http_WithATrustManager_Throws()
    {
        var act = () => CreateContext(HttpUri, builder => builder.WithTrustManager(TrustManager.CreateInsecure()));

        act.Should().Throw<ArgumentException>().WithMessage("*conflicting*");
    }

    [Theory]
    [InlineData(HttpUri)]
    [InlineData(HttpsUri)]
    public void WithATlsNegotiator_Throws(string uri)
    {
        var act = () => CreateContext(uri, builder => builder.WithTlsNegotiator((_, stream) => new SslStream(stream)));

        act.Should().Throw<NotSupportedException>().WithMessage("*TlsNegotiator*");
    }
}
