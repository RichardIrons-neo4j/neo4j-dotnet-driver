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
using FluentAssertions;
using Moq;
using Neo4j.Driver.Internal.QueryApi;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.QueryApi;

public class QueryApiWriteCodecSelectorTests
{
    private static IQueryApiTypeCodec ContainerCodec(object value, bool canWrite)
    {
        var codec = new Mock<IQueryApiContainerCodec>();
        codec.Setup(c => c.CanWrite(value)).Returns(canWrite);
        return codec.Object;
    }

    [Fact]
    public void Select_ReturnsTheContainerCodec_WhenItIsTheOnlyMatch()
    {
        var value = new object();
        var container = ContainerCodec(value, canWrite: true);
        var subject = new QueryApiWriteCodecSelector([ContainerCodec(value, canWrite: false), container]);

        var selected = subject.Select(value);

        selected.Should().BeSameAs(container);
    }

    [Fact]
    public void Select_Throws_WhenSeveralContainerCodecsMatch()
    {
        var value = new object();
        var subject = new QueryApiWriteCodecSelector(
            [ContainerCodec(value, canWrite: true), ContainerCodec(value, canWrite: true)]);

        var act = () => subject.Select(value);

        act.Should().Throw<NotSupportedException>();
    }
}
