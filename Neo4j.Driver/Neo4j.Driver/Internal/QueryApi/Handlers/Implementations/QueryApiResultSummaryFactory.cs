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

using Neo4j.Driver.Internal.MessageHandling.Metadata;
using Neo4j.Driver.Internal.Result;

namespace Neo4j.Driver.Internal.QueryApi;

internal class QueryApiResultSummaryFactory : IResultSummaryFactory
{
    private readonly IServerInfo _serverInfo;

    public QueryApiResultSummaryFactory(IServerInfo serverInfo)
    {
        _serverInfo = serverInfo;
    }

    public IResultSummary Create(Query query, string database, QueryApiResultBody body)
    {
        var builder = new SummaryBuilder(query, _serverInfo)
        {
            Database = new DatabaseInfo(database),
            Counters = ToCounters(body.Counters),
            StatusAndNotifications = new GqlStatusObjectsAndNotifications(null, null, false)
        };

        var hadRecords = body.Data?.Values?.Length > 0;
        var hadKeys = body.Data?.Fields?.Length > 0;
        return builder.Build(new CursorMetadata(hadRecords, hadKeys));
    }

    private ICounters ToCounters(QueryApiCountersBody counters)
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
}
