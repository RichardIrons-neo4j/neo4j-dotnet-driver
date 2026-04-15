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

namespace Neo4j.Driver.Bolt.Tests.TestHelpers;

public static class SetSizeGenerator
{
    public static List<int[]> GenerateSizes(int target, int maxParts, int minValue)
    {
        return Enumerable.Range(1, maxParts)
            .SelectMany(k => GenerateExactAllowingZero(target, k))
            .Select(RemoveZeroes)
            .Where(a => a.Length > 0) // avoid empty result (e.g. target==0)
            .Where(a => a.All(x => x >= minValue)) // enforce minValue after zero-removal
            .Distinct(IntArrayComparer.Instance)
            .ToList();
    }

    private static IEnumerable<int[]> GenerateExactAllowingZero(int target, int parts)
    {
        var current = new int[parts];
        foreach (var r in Recurse(target, parts, 0, current))
        {
            yield return r;
        }
    }

    private static IEnumerable<int[]> Recurse(int remaining, int slotsLeft, int index, int[] current)
    {
        if (slotsLeft == 1)
        {
            current[index] = remaining;
            yield return (int[])current.Clone();

            yield break;
        }

        for (int v = 0; v <= remaining; v++)
        {
            current[index] = v;
            foreach (var r in Recurse(remaining - v, slotsLeft - 1, index + 1, current))
            {
                yield return r;
            }
        }
    }

    private static int[] RemoveZeroes(int[] arr) =>
        arr.Where(x => x != 0).ToArray();

    private sealed class IntArrayComparer : IEqualityComparer<int[]>
    {
        public static readonly IntArrayComparer Instance = new();

        public bool Equals(int[]? x, int[]? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            if (x.Length != y.Length)
            {
                return false;
            }

            return !x.Where((t, i) => t != y[i]).Any();
        }

        public int GetHashCode(int[] obj)
        {
            return obj.Aggregate(0, HashCode.Combine);
        }
    }
}
