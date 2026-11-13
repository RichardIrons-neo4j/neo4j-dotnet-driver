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
using System.Collections.Generic;
using System.Linq;

namespace Neo4j.Driver.Internal.QueryApi;

internal class QueryApiWriteCodecSelector : IQueryApiWriteCodecSelector
{
    private readonly IEnumerable<IQueryApiTypeCodec> _codecs;

    public QueryApiWriteCodecSelector(IEnumerable<IQueryApiTypeCodec> codecs)
    {
        _codecs = codecs;
    }

    public IQueryApiTypeCodec Select(object? value)
    {
        var matchingCodecs = _codecs.Where(c => c.CanWrite(value)).ToList();
        var specificCodecs = matchingCodecs.Where(c => c is not IQueryApiContainerCodec).ToList();

        return (matchingCodecs.Count, specificCodecs.Count) switch
        {
            (_, 1) => specificCodecs[0],

            (_, > 1) => throw new NotSupportedException(
                $"Multiple codecs can write value of type '{value?.GetType().Name ?? "null"}'."),

            (0, _) => throw new NotSupportedException(
                "No codec can write value of type '" + value?.GetType().Name + "'."),

            (1, _) => matchingCodecs[0],

            _ => throw new NotSupportedException(
                $"Multiple container codecs can write value of type '{value?.GetType().Name ?? "null"}'.")
        };
    }
}
