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

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Moq.Language;
using Neo4j.Driver.Internal.Encryption;
using Neo4j.Driver.Preview.Encryption;
using Neo4j.Driver.Tests.TestUtil;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.Encryption;

public class InMemoryEncapsulatedKeyRepositoryTests
{
    private static readonly byte[] Encapsulation = [1, 2, 3, 4];

    private static readonly IReadOnlyDictionary<string, string> Metadata =
        new Dictionary<string, string> { ["iv"] = "abc" };

    private readonly AutoMocker _autoMock = new(MockBehavior.Loose);

    private InMemoryEncapsulatedKeyRepository CreateSubject()
    {
        return _autoMock.CreateInstance<InMemoryEncapsulatedKeyRepository>();
    }

    private void SetGeneratedIds(params string[] ids)
    {
        _autoMock
            .GetMock<IKeyIdGenerator>()
            .SetupSequence(g => g.Get())
            .ReturnsSequence(ids);
    }

    [Fact]
    public async Task Save_UsesTheGeneratedIdAndPreservesTheStoredData()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1");

        var saved = await subject.CreateAsync("primary", Encapsulation, Metadata);

        saved!.Id.Should().Be("key-1");
        saved!.Alias.Should().Be("primary");
        saved.Encapsulation.Should().Equal(Encapsulation);
        saved.Metadata.Should().Equal(Metadata);
    }

    [Fact]
    public async Task Save_WithNoAliasSavesAnUnaliasedKey()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1");

        var saved = await subject.CreateAsync(null, Encapsulation, Metadata);

        saved!.Alias.Should().BeNull();
    }

    [Fact]
    public async Task Save_UsesAFreshIdFromTheGeneratorForEachKey()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1", "key-2");

        var first = await subject.CreateAsync(null, Encapsulation, Metadata);
        var second = await subject.CreateAsync(null, Encapsulation, Metadata);

        first!.Id.Should().Be("key-1");
        second!.Id.Should().Be("key-2");
    }

    [Fact]
    public async Task FindById_ReturnsTheSavedKey()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1");

        var saved = await subject.CreateAsync("primary", Encapsulation, Metadata);

        var found = await subject.FindByIdAsync("key-1");

        found.Should().BeEquivalentTo(saved);
    }

    [Fact]
    public async Task FindByAlias_ReturnsTheKeySavedUnderThatAlias()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1");

        await subject.CreateAsync("primary", Encapsulation, Metadata);

        var found = await subject.FindByAliasAsync("primary");

        found!.Id.Should().Be("key-1");
    }

    [Fact]
    public async Task FindById_ReturnsNullWhenTheKeyIsUnknown()
    {
        var subject = CreateSubject();

        var found = await subject.FindByIdAsync("missing");

        found.Should().BeNull();
    }

    [Fact]
    public async Task FindByAlias_ReturnsNullWhenTheAliasIsUnknown()
    {
        var subject = CreateSubject();

        var found = await subject.FindByAliasAsync("missing");

        found.Should().BeNull();
    }

    [Fact]
    public async Task SetAliasById_MakesTheKeyDiscoverableByTheNewAlias()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1");

        await subject.CreateAsync("primary", Encapsulation, Metadata);

        await subject.SetAliasByIdAsync("key-1", "extra");

        var found = await subject.FindByAliasAsync("extra");
        found!.Id.Should().Be("key-1");
        found!.Alias.Should().Be("extra");
    }

    [Fact]
    public async Task SetAliasById_ReplacesAnyExistingAliasOnTheSameKey()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1");

        await subject.CreateAsync("primary", Encapsulation, Metadata);

        await subject.SetAliasByIdAsync("key-1", "extra");

        var key = await subject.FindByIdAsync("key-1");
        key!.Alias.Should().Be("extra");

        var gone = await subject.FindByAliasAsync("primary");
        gone.Should().BeNull();
    }

    [Fact]
    public async Task SetAliasById_ThrowsWhenTheIdIsUnknown()
    {
        var subject = CreateSubject();

        var act = () => subject.SetAliasByIdAsync("missing", "extra");

        await act.Should().ThrowAsync<EncapsulatedKeyNotFoundException>();
    }

    [Fact]
    public async Task SetAliasByIdToNull_RemovesTheAliasButKeepsTheKey()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1");

        await subject.CreateAsync("primary", Encapsulation, Metadata);

        await subject.SetAliasByIdAsync("key-1", null);

        var byId = await subject.FindByIdAsync("key-1");
        byId!.Alias.Should().BeNull();

        var gone = await subject.FindByAliasAsync("primary");
        gone.Should().BeNull();
    }

    [Fact]
    public async Task SetAliasByIdToNull_OnAKeyWithNoAlias_IsANoOp()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1");

        await subject.CreateAsync(null, Encapsulation, Metadata);

        await subject.SetAliasByIdAsync("key-1", null);

        var key = await subject.FindByIdAsync("key-1");
        key!.Alias.Should().BeNull();
    }

    [Fact]
    public async Task DeleteById_RemovesTheKeyAndItsAlias()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1");

        await subject.CreateAsync("primary", Encapsulation, Metadata);

        await subject.DeleteByIdAsync("key-1");

        var byId = await subject.FindByIdAsync("key-1");
        byId.Should().BeNull();

        var byAlias = await subject.FindByAliasAsync("primary");
        byAlias.Should().BeNull();
    }

    [Fact]
    public async Task DeleteById_ThrowsWhenTheIdIsUnknown()
    {
        var subject = CreateSubject();

        var act = () => subject.DeleteByIdAsync("missing");

        await act.Should().ThrowAsync<EncapsulatedKeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteById_ThenTheAliasCanBeReusedByAnotherKey()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1", "key-2");

        await subject.CreateAsync("primary", Encapsulation, Metadata);
        await subject.DeleteByIdAsync("key-1");

        await subject.CreateAsync("primary", Encapsulation, Metadata);

        var byAlias = await subject.FindByAliasAsync("primary");
        byAlias!.Id.Should().Be("key-2");
    }

    [Fact]
    public async Task SetAliasById_ThrowsWhenAnotherKeyAlreadyHoldsTheAlias()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1", "key-2");

        await subject.CreateAsync("shared", Encapsulation, Metadata);
        await subject.CreateAsync(null, Encapsulation, Metadata);

        var act = () => subject.SetAliasByIdAsync("key-2", "shared");

        await act.Should().ThrowAsync<EncapsulatedAliasInUseException>().WithMessage("*shared*");
    }

    [Fact]
    public async Task SetAliasById_AfterTheHoldingKeyReleasesIt_BindsTheAlias()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1", "key-2");

        await subject.CreateAsync("shared", Encapsulation, Metadata);
        await subject.CreateAsync(null, Encapsulation, Metadata);

        await subject.SetAliasByIdAsync("key-1", null);
        await subject.SetAliasByIdAsync("key-2", "shared");

        var byAlias = await subject.FindByAliasAsync("shared");
        byAlias!.Id.Should().Be("key-2");
    }

    [Fact]
    public async Task SetAliasById_IsIdempotentWhenTheAliasIsAlreadyOnTheKey()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1");

        await subject.CreateAsync("primary", Encapsulation, Metadata);

        await subject.SetAliasByIdAsync("key-1", "primary");

        var key = await subject.FindByIdAsync("key-1");
        key!.Alias.Should().Be("primary");

        var byAlias = await subject.FindByAliasAsync("primary");
        byAlias!.Id.Should().Be("key-1");
    }

    [Fact]
    public async Task Create_ThrowsWhenAnotherKeyAlreadyHoldsTheAlias()
    {
        var subject = CreateSubject();
        SetGeneratedIds("key-1", "key-2");

        await subject.CreateAsync("shared", Encapsulation, Metadata);

        var act = () => subject.CreateAsync("shared", Encapsulation, Metadata);

        await act.Should().ThrowAsync<EncapsulatedAliasInUseException>().WithMessage("*shared*");
    }
}
