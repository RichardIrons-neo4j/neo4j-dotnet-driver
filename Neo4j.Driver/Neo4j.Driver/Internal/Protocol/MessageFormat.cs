// Copyright (c) "Neo4j"
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
using Neo4j.Driver.Internal.IO;
using System.Collections.Generic;
using System.Linq;
using Neo4j.Driver.Internal.IO.MessageSerializers;
using Neo4j.Driver.Internal.IO.ValueSerializers;
using Neo4j.Driver.Internal.IO.ValueSerializers.Temporal;

namespace Neo4j.Driver.Internal.Protocol;

internal sealed class MessageFormat
{
    private readonly Dictionary<byte, IPackStreamSerializer> _readerStructHandlers = new();
    private readonly Dictionary<Type, IPackStreamSerializer> _writerStructHandlers = new();

    private void AddHandler<T>() where T : IPackStreamSerializer, new()
    {
        var handler = new T();

        foreach (var readableStruct in handler.ReadableStructs)
            _readerStructHandlers.Add(readableStruct, handler);

        foreach (var writableType in handler.WritableTypes)
            _writerStructHandlers.Add(writableType, handler);
    }

    private void RemoveHandler<T>()
    {
        _readerStructHandlers
            .Where(kvp => kvp.Value is T)
            .Select(kvp => kvp.Key)
            .ToList()
            .ForEach(b => _readerStructHandlers.Remove(b));

        _writerStructHandlers
            .Where(kvp => kvp.Value is T)
            .Select(kvp => kvp.Key)
            .ToList()
            .ForEach(t => _writerStructHandlers.Remove(t));
    }

    public IReadOnlyDictionary<byte, IPackStreamSerializer> ReaderStructHandlers => _readerStructHandlers;
    public IReadOnlyDictionary<Type, IPackStreamSerializer> WriteStructHandlers => _writerStructHandlers;

    public BoltProtocolVersion Version { get; }

    // Message Constants Inherited Over Older Versions
    public const byte MsgReset = 0x0F;
    public const byte MsgRun = 0x10;

    public const byte MsgDiscard = 0x2F;
    public const byte MsgPull = 0x3F;

    public const byte MsgRecord = 0x71;
    public const byte MsgSuccess = 0x70;
    public const byte MsgIgnored = 0x7E;
    public const byte MsgFailure = 0x7F;

    // Message Constants

    public const byte MsgHello = 0x01;
    public const byte MsgGoodbye = 0x02;
    public const byte MsgBegin = 0x11;
    public const byte MsgCommit = 0x12;
    public const byte MsgRollback = 0x13;

    //4.3+
    public const byte MsgRoute = 0x66;

    internal MessageFormat(BoltProtocolVersion version)
    {
        Version = version;
        // Response Message Types
        AddHandler<FailureMessageSerializer>();
        AddHandler<IgnoredMessageSerializer>();
        AddHandler<RecordMessageSerializer>();
        AddHandler<SuccessMessageSerializer>();

        // Struct Data Types
        AddHandler<NodeSerializer>();
        AddHandler<RelationshipSerializer>();
        AddHandler<UnboundRelationshipSerializer>();
        AddHandler<PathSerializer>();

        // Add V2 Spatial Types
        AddHandler<PointSerializer>();

        // Add V2 Temporal Types
        AddHandler<LocalDateSerializer>();
        AddHandler<LocalTimeSerializer>();
        AddHandler<LocalDateTimeSerializer>();
        AddHandler<OffsetTimeSerializer>();
        AddHandler<ZonedDateTimeSerializer>();
        AddHandler<DurationSerializer>();

        // Add BCL Handlers
        AddHandler<SystemDateTimeSerializer>();
        AddHandler<SystemDateTimeOffsetHandler>();
        AddHandler<SystemTimeSpanSerializer>();

        if (version < BoltProtocolVersion.V5_0)
            return;

        //5.0+
        AddHandler<UtcZonedDateTimeSerializer>();

        RemoveHandler<NodeSerializer>();
        AddHandler<ElementNodeSerializer>();

        RemoveHandler<RelationshipSerializer>();
        AddHandler<ElementRelationshipSerializer>();

        RemoveHandler<UnboundRelationshipSerializer>();
        AddHandler<ElementUnboundRelationshipSerializer>();
    }

    public void UseUtcEncoder()
    {
        if (Version > BoltProtocolVersion.V4_4 || Version <= BoltProtocolVersion.V4_3)
            return;

        AddHandler<UtcZonedDateTimeSerializer>();
    }
}