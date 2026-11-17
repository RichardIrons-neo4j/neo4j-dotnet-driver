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

namespace Neo4j.Driver.Preview.QueryApi;

/// <summary>
/// Extension methods that enable the Neo4j Query API transport on <see cref="ConfigBuilder"/>. This class is
/// part of the Query API Preview feature, and is subject to change or removal.
/// </summary>
public static class QueryApiPreviewExtensions
{
    extension(ConfigBuilder configBuilder)
    {
        /// <summary>
        /// Enables drivers created with an <c>http</c> or <c>https</c> URI, which connect over the Neo4j Query
        /// API instead of Bolt. Without it, creating such a driver throws <see cref="NotSupportedException"/>.
        /// This method is part of the Query API Preview feature, and is subject to change or removal.
        /// </summary>
        /// <returns>The current <see cref="ConfigBuilder"/> instance to allow method chaining.</returns>
        public ConfigBuilder EnableQueryApi()
        {
            return configBuilder.Preview_EnableQueryApi();
        }
    }
}
