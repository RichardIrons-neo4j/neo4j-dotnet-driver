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

using System.Collections.Generic;
using System.Threading.Tasks;
using Neo4j.Driver.Internal.IO;
using Neo4j.Driver.Internal.IO.MessageSerializers;
using Neo4j.Driver.Internal.MessageHandling;

namespace Neo4j.Driver.Internal.Messaging;

internal sealed class SuccessMessage : IResponseMessage
{
    public SuccessMessage(IDictionary<string, object> meta)
    {
        Meta = meta;
    }

    public IDictionary<string, object> Meta { get; }

    public override string ToString()
    {
        return $"SUCCESS {Meta.ToContentString()}";
    }

    public void Dispatch(IResponsePipeline pipeline)
    {
        pipeline.OnSuccess(Meta);
    }

    public IPackStreamSerializer Serializer => SuccessMessageSerializer.Instance;
}