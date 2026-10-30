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
using System.Text.Json;

namespace Neo4j.Driver.Internal.QueryApi;

internal record QueryApiErrorBody(string Code = "", string Message = "");

internal record QueryApiDataBody
{
    public string[]? Fields { get; init; }
    public JsonElement[][]? Values { get; init; }
}

internal record QueryApiCountersBody
{
    public bool ContainsUpdates { get; init; }
    public int NodesCreated { get; init; }
    public int NodesDeleted { get; init; }
    public int PropertiesSet { get; init; }
    public int RelationshipsCreated { get; init; }
    public int RelationshipsDeleted { get; init; }
    public int LabelsAdded { get; init; }
    public int LabelsRemoved { get; init; }
    public int IndexesAdded { get; init; }
    public int IndexesRemoved { get; init; }
    public int ConstraintsAdded { get; init; }
    public int ConstraintsRemoved { get; init; }
    public bool ContainsSystemUpdates { get; init; }
    public int SystemUpdates { get; init; }
}

internal record QueryApiPlanBody
{
    public string? OperatorType { get; init; }
    public Dictionary<string, JsonElement>? Arguments { get; init; }
    public string[]? Identifiers { get; init; }
    public QueryApiPlanBody[]? Children { get; init; }
}

internal record QueryApiResultBody : QueryApiResponse
{
    public QueryApiDataBody? Data { get; init; }
    public string[]? Bookmarks { get; init; }
    public QueryApiCountersBody? Counters { get; init; }
    public long? ResultAvailableAfter { get; init; }
    public long? ResultConsumedAfter { get; init; }
    public string? QueryType { get; init; }
    public QueryApiPlanBody? QueryPlan { get; init; }
}
