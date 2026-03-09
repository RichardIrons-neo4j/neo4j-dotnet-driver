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

using System.Buffers;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Neo4j.Driver.Bolt.PackStream;
using Neo4j.Driver.Bolt.PackStream.Abstractions;
using Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;
using Neo4j.Driver.Bolt.PackStream.Implementations;
using Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;
using Neo4j.Driver.Bolt.Transport.Abstractions;
using NUnit.Framework;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Sinks.SystemConsole.Themes;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Neo4j.Driver.Bolt.Tests.PackStream.Integration;

[TestFixture]
internal class PackStreamDecoderIntegrationTests
{
    private AutoMocker AutoMocker = new();
    private IPackStreamDecoder Subject => AutoMocker.CreateInstance<PackStreamDecoder>();

    [SetUp]
    public void SetUp()
    {
        AutoMocker = new AutoMocker();
        var logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Debug()
            .CreateLogger();

        var frameworkLogger = new SerilogLoggerProvider(logger).CreateLogger("Neo4j.Driver.Bolt.Tests");
        AutoMocker.Use<ILogger>(frameworkLogger);
        AutoMocker.Use<IPackStreamSizeReader>(new PackStreamSizeReader());
        // Create all decoders with AutoMocker (ILogger and IPackStreamSizeReader are resolved from container)
        var decoders = new IValueDecoder[]
        {
            AutoMocker.CreateInstance<NullDecoder>(),
            AutoMocker.CreateInstance<BooleanDecoder>(),
            AutoMocker.CreateInstance<TinyIntDecoder>(),
            AutoMocker.CreateInstance<IntegerDecoder>(),
            AutoMocker.CreateInstance<FloatDecoder>(),
            AutoMocker.CreateInstance<StringDecoder>(),
            AutoMocker.CreateInstance<BytesDecoder>(),
            AutoMocker.CreateInstance<ListDecoder>(),
            AutoMocker.CreateInstance<MapDecoder>(),
            AutoMocker.CreateInstance<StructDecoder>(),
        };

        AutoMocker.Use<IValueDecoder[]>(decoders);
        var provider = new ValueDecoderProvider(decoders, frameworkLogger);
        AutoMocker.Use<IValueDecoderProvider>(provider);
        AutoMocker.Use<IChunkAssembler>(Mock.Of<IChunkAssembler>());
    }

    [Test]
    public void Decodes_nested_list_and_map_mixed_types()
    {
        // PackStream: [ 42, { "a": 1, "b": [ 2, 3 ] } ]
        // 92       = TinyList(2), 2A = 42, A2 = TinyMap(2),
        // 81 61 01 = "a", 1 | 81 62 92 02 03 = "b", [2,3]
        byte[] data =
        [
            0x92, 0x2A,
            0xA2, 0x81, 0x61, 0x01, 0x81, 0x62, 0x92, 0x02, 0x03
        ];

        var buffer = new ReadOnlySequence<byte>(data);
        var result = Subject.Decode(buffer);
        result.BytesConsumed.Should().Be(data.Length);
        result.Value.Type.Should().Be(PackStreamType.List);
        var list = result.Value.ListValue;
        list.Count.Should().Be(2);
        var items = list.ToEnumerable().ToList();
        items[0].IntValue.Should().Be(42);
        items[1].Type.Should().Be(PackStreamType.Map);
        var entries = items[1].MapValue.ToEnumerable().ToList();
        entries[0].Key.StringValue.ToString().Should().Be("a");
        entries[0].Value.IntValue.Should().Be(1);
        entries[1].Key.StringValue.ToString().Should().Be("b");
        entries[1].Value.ListValue.ToEnumerable().Select(v => v.IntValue).Should().BeEquivalentTo([2, 3]);
    }
}
