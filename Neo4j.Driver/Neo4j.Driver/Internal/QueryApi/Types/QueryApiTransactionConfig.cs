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

namespace Neo4j.Driver.Internal.QueryApi;

internal class QueryApiTransactionConfig
{
    public QueryApiTransactionConfig(TransactionConfig config)
    {
        MaxExecutionTime = ToWholeSeconds(config.Timeout);
        TxMetadata = config.Metadata.Count > 0 ? new QueryApiParameterDictionary(config.Metadata) : null;
    }

    public int? MaxExecutionTime { get; }

    public QueryApiParameterDictionary? TxMetadata { get; }

    private static int? ToWholeSeconds(TimeSpan? timeout)
    {
        if (timeout is not {} value)
        {
            return null;
        }

        var seconds = Math.Ceiling(value.TotalSeconds);
        return seconds >= int.MaxValue ? int.MaxValue : (int)seconds;
    }
}
