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
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Neo4j.Driver.Internal.QueryApi;

internal class QueryApiSystemTimeSpanCodec : IQueryApiTypeCodec
{
    public bool CanRead(string typeName)
    {
        return false;
    }

    public object? Read(JsonElement element, IJsonValueDecoder recurse)
    {
        throw new NotSupportedException($"{nameof(QueryApiSystemTimeSpanCodec)} cannot read values.");
    }

    public bool CanWrite(object? value)
    {
        return value is TimeSpan;
    }

    public JsonNode? Write(object? value, IJsonValueEncoder recurse)
    {
        var time = (TimeSpan)value!;

        if (time.Ticks < 0 || time.Ticks >= TimeSpan.TicksPerDay)
        {
            throw new ProtocolException($"TimeSpan instance ({time}) is not a valid time of day.");
        }

        return recurse.Encode(new LocalTime(time));
    }
}
