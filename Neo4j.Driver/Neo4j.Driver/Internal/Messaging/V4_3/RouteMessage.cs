﻿// Copyright (c) "Neo4j"
// Neo4j Sweden AB [http://neo4j.com]
// 
// This file is part of Neo4j.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
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

using System;
using System.Collections.Generic;
using System.Text;
using Neo4j.Driver.Internal.IO;
using Neo4j.Driver.Internal.IO.MessageSerializers.V4_3;

namespace Neo4j.Driver.Internal.Messaging.V4_3;

internal sealed class RouteMessage : IRequestMessage
{
    public RouteMessage(IDictionary<string, string> routingContext, Bookmarks bookmarks, string db)
    {
        Routing = routingContext ?? new Dictionary<string, string>();
        Bookmarks = bookmarks ?? Bookmarks.From(Array.Empty<string>());
        DatabaseParam = db;
    }

    public IDictionary<string, string> Routing { get; }
    public Bookmarks Bookmarks { get; }
    public string DatabaseParam { get; }

    public IPackStreamSerializer Serializer => RouteMessageSerializer.Instance;

    public override string ToString()
    {
        var stringBuilder = new StringBuilder(50);

        stringBuilder.Append("ROUTE {");
        foreach (var data in Routing)
        {
            stringBuilder.Append(" '")
                .Append(data.Key)
                .Append("':'")
                .Append(data.Value)
                .Append("'");
        }

        stringBuilder.Append(" } ");

        if (Bookmarks?.Values.Length > 0)
        {
            stringBuilder.Append("{ bookmarks, ")
                .Append(Bookmarks.Values.ToContentString())
                .Append(" }");
        }
        else
        {
            stringBuilder.Append("[]");
        }

        if (!string.IsNullOrEmpty(DatabaseParam))
        {
            stringBuilder.Append(" '")
                .Append(DatabaseParam)
                .Append("'");
        }
        else
        {
            stringBuilder.Append(" None");
        }

        return stringBuilder.ToString();
    }
}
