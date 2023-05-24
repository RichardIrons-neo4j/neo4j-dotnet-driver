﻿// Copyright (c) "Neo4j"
// Neo4j Sweden AB [http://neo4j.com]
// 
// This file is part of Neo4j.
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

using System;
using System.Threading.Tasks;
using Neo4j.Driver.Internal.Services;

namespace Neo4j.Driver.Tests.TestBackend;

public class FakeTime : IDateTimeProvider
{
    public static FakeTime Instance = new();

    private DateTime? _frozenTime;
    public DateTime Now() => _frozenTime ?? DateTime.Now;
    public void Freeze() => _frozenTime = DateTime.Now;
    public void Advance(int milliseconds) => _frozenTime = Now().AddMilliseconds(milliseconds);
    public void Unfreeze() => _frozenTime = null;
}

internal class FakeTimeInstall : IProtocolObject
{
    public object data { get; set; }

    public override Task Process()
    {
        FakeTime.Instance.Freeze();
        return Task.CompletedTask;
    }

    public override string Respond() => new ProtocolResponse("FakeTimeAck").Encode();
}

internal class FakeTimeTick : IProtocolObject
{
    public record DataType(int incrementMs);

    public DataType data { get; set; }

    public override Task Process()
    {
        FakeTime.Instance.Advance(data.incrementMs);
        return Task.CompletedTask;
    }

    public override string Respond() => new ProtocolResponse("FakeTimeAck").Encode();
}

internal class FakeTimeUninstall : IProtocolObject
{
    public object data { get; set; }

    public override Task Process()
    {
        FakeTime.Instance.Unfreeze();
        return Task.CompletedTask;
    }

    public override string Respond() => new ProtocolResponse("FakeTimeAck").Encode();
}