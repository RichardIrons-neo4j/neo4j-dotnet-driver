﻿// Copyright (c) "Neo4j"
// Neo4j Sweden AB [http://neo4j.com]
// 
// This file is part of Neo4j.
// 
// Licensed under the Apache License, Version 2.0 (the "License"):
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;

namespace Neo4j.Driver.Internal;

internal class SimpleWildcardHelper
{
    /// <summary>
    /// Returns true if the two strings are the same, or, if <see cref="y"/> ends with an asterisk (*),
    /// returns true if <see cref="x"/> starts with <see cref="y"/> (minus the asterisk).
    /// </summary>
    /// <param name="x">The string to check.</param>
    /// <param name="y">The (potential) wildcard to compare with</param>
    /// <returns>True if the strings match; false otherwise.</returns>
    public bool StringMatches(string x, string y)
    {
        if (!y.EndsWith("*"))
        {
            return x == y;
        }

        return x.StartsWith(y.Substring(0, y.Length - 1));
    }
}
