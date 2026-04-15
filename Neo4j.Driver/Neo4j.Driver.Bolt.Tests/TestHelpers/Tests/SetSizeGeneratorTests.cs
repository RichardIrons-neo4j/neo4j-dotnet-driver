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

using FluentAssertions;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests.TestHelpers.Tests;

public class SetSizeGeneratorTests
{
    [Test]
    public void SplitIndicesNoRecursion()
    {
        var indices = SetSizeGenerator.GenerateSizes(5, 2, 0);
        indices.Should().BeEquivalentTo<List<int>>([
            [5],
            [1, 4],
            [2, 3],
            [3, 2],
            [4, 1]
        ]);
    }

    [Test]
    public void SplitIndicesNoRecursionMinValue()
    {
        var indices = SetSizeGenerator.GenerateSizes(5, 2, 2);
        indices.Should()
            .BeEquivalentTo<List<int>>(
            [
                [5],
                [2, 3],
                [3, 2],
            ]);
    }

    [Test]
    public void SplitIndicesWithRecursionLargerValue()
    {
        var indices = SetSizeGenerator.GenerateSizes(10, 3, 2);
        indices.Should()
            .BeEquivalentTo<List<int>>(
            [
                [10], [2, 8], [3, 7], [4, 6], [5, 5], [6, 4], [7, 3], [8, 2], [2, 2, 6], [2, 3, 5], [2, 4, 4],
                [2, 5, 3], [2, 6, 2], [3, 2, 5], [3, 3, 4], [3, 4, 3], [3, 5, 2], [4, 2, 4], [4, 3, 3], [4, 4, 2],
                [5, 2, 3], [5, 3, 2], [6, 2, 2]
            ]);
    }
    
    [Test]
    public void SplitIndicesWithRecursion()
    {
        var indices = SetSizeGenerator.GenerateSizes(5, 3, 0);
        indices.Should().BeEquivalentTo<List<int>>([
            [5],
            [1,4],
            [2,3],
            [3,2],
            [4,1],
            [1,1,3],
            [1,2,2],
            [1,3,1],
            [2,1,2],
            [2,2,1],
            [3,1,1]
        ]);
    }
}
