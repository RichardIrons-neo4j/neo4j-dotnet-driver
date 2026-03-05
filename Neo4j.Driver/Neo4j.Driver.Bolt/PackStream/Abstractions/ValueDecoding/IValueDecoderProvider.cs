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

using Microsoft.Extensions.Logging;

namespace Neo4j.Driver.Bolt.PackStream.Abstractions.ValueDecoding;

internal interface IValueDecoderProvider
{
    IValueDecoder GetDecoder(byte markerByte, IPackStreamDecoder recursionDecoder);
}

internal class ValueDecoderProvider : IValueDecoderProvider
{
    private readonly ILogger _logger;
    private readonly Dictionary<byte, IValueDecoder> _decoders = new();
    
    public ValueDecoderProvider(IEnumerable<IValueDecoder> decoders, ILogger logger)
    {
        _logger = logger;
        foreach (var decoder in decoders)
        {
            foreach (var markerByte in decoder.HandledMarkerBytes)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Registering decoder {decoder} for marker byte: {markerByte}",
                        decoder.GetType().Name,
                        $"0x{markerByte:X2}");
                }

                _decoders[markerByte] = decoder;
            }
        }
    }
    
    public IValueDecoder GetDecoder(byte markerByte, IPackStreamDecoder recursionDecoder)
    {
        if (!_decoders.TryGetValue(markerByte, out var decoder))
        {
            throw new InvalidOperationException($"Unknown marker byte: 0x{markerByte:X2}");
        }

        if (decoder is IRecursiveValueDecoder recursiveValueDecoder)
        {
            recursiveValueDecoder.SetRecursionDecoder(recursionDecoder);
        }
        
        return decoder;
    }
}
