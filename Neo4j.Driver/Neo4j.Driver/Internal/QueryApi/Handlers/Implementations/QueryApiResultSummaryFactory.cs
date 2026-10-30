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
using System.Linq;
using Neo4j.Driver.Internal.MessageHandling.Metadata;
using Neo4j.Driver.Internal.Result;

namespace Neo4j.Driver.Internal.QueryApi;

internal class QueryApiResultSummaryFactory : IResultSummaryFactory
{
    private readonly IJsonValueDecoder _jsonValueDecoder;
    private readonly IServerInfo _serverInfo;

    public QueryApiResultSummaryFactory(IServerInfo serverInfo, IJsonValueDecoder jsonValueDecoder)
    {
        _serverInfo = serverInfo;
        _jsonValueDecoder = jsonValueDecoder;
    }

    public IResultSummary Create(Query query, string database, QueryApiResultBody body)
    {
        var builder = new SummaryBuilder(query, _serverInfo)
        {
            Database = new DatabaseInfo(database),
            Counters = ToCounters(body.Counters),
            QueryType = ToQueryType(body.QueryType),
            ResultAvailableAfter = body.ResultAvailableAfter ?? -1,
            ResultConsumedAfter = body.ResultConsumedAfter ?? -1,
            Plan = body.QueryPlan is null ? null : ToPlan(body.QueryPlan),
            Profile = body.ProfiledQueryPlan is null ? null : ToProfiledPlan(body.ProfiledQueryPlan),
            QueryProfile = body.ProfiledQueryPlan is null ? null : ToQueryProfile(body.ProfiledQueryPlan),
            StatusAndNotifications = new GqlStatusObjectsAndNotifications(null, null, false)
        };

        var hadRecords = body.Data?.Values?.Length > 0;
        var hadKeys = body.Data?.Fields?.Length > 0;
        return builder.Build(new CursorMetadata(hadRecords, hadKeys));
    }

    private ICounters? ToCounters(QueryApiCountersBody? counters)
    {
        if (counters is null)
        {
            return null;
        }

        return new Counters(
            counters.NodesCreated,
            counters.NodesDeleted,
            counters.RelationshipsCreated,
            counters.RelationshipsDeleted,
            counters.PropertiesSet,
            counters.LabelsAdded,
            counters.LabelsRemoved,
            counters.IndexesAdded,
            counters.IndexesRemoved,
            counters.ConstraintsAdded,
            counters.ConstraintsRemoved,
            counters.SystemUpdates,
            counters.ContainsSystemUpdates,
            counters.ContainsUpdates);
    }

    private IPlan ToPlan(QueryApiPlanBody plan)
    {
        var children = (plan.Children ?? []).Select(ToPlan).ToList();
        return new Plan(plan.OperatorType, ToArguments(plan), plan.Identifiers ?? [], children);
    }

    private IQueryProfile ToQueryProfile(QueryApiPlanBody plan)
    {
        var children = (plan.Children ?? []).Select(ToQueryProfile).ToList();
        return new QueryProfile(
            plan.OperatorType,
            ToArguments(plan),
            plan.Identifiers ?? [],
            children,
            plan.DbHits,
            plan.Records,
            plan.PageCacheHits,
            plan.PageCacheMisses,
            plan.PageCacheHitRatio,
            plan.Time);
    }

#pragma warning disable CS0618
    private IProfiledPlan ToProfiledPlan(QueryApiPlanBody plan)
    {
        var children = (plan.Children ?? []).Select(ToProfiledPlan).ToList();
        var hasPageCacheStats =
            plan.PageCacheHits.HasValue || plan.PageCacheMisses.HasValue || plan.PageCacheHitRatio.HasValue;

        return new ProfiledPlan(
            plan.OperatorType,
            ToArguments(plan),
            plan.Identifiers ?? [],
            children,
            plan.DbHits ?? 0,
            plan.Records ?? 0,
            plan.PageCacheHits ?? 0,
            plan.PageCacheMisses ?? 0,
            plan.PageCacheHitRatio ?? 0,
            plan.Time ?? 0,
            hasPageCacheStats);
    }
#pragma warning restore CS0618

    private Dictionary<string, object?> ToArguments(QueryApiPlanBody plan)
    {
        return (plan.Arguments ?? [])
            .ToDictionary(argument => argument.Key, argument => _jsonValueDecoder.Decode(argument.Value));
    }

    private QueryType ToQueryType(string? queryType)
    {
        return queryType switch
        {
            null => QueryType.Unknown,
            "r" => QueryType.ReadOnly,
            "rw" => QueryType.ReadWrite,
            "w" => QueryType.WriteOnly,
            "s" => QueryType.SchemaWrite,
            _ => throw new ProtocolException($"The server sent an unknown query type '{queryType}'.")
        };
    }
}
