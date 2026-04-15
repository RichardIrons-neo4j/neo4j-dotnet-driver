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

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Neo4j.Driver.Bolt.Messages;
using Neo4j.Driver.Bolt.PackStream.Ephemeral;

namespace Neo4j.Driver.Bolt.Messages.Decoding;

internal sealed class MessageDecoderProvider
{
    private readonly IReadOnlyDictionary<byte, IMessageDecoder> _decodersByTag;

    public MessageDecoderProvider(IEnumerable<IMessageDecoder> decoders)
    {
        var dict = new Dictionary<byte, IMessageDecoder>();
        foreach (var decoder in decoders)
        {
            dict[decoder.HandledTag] = decoder;
        }

        _decodersByTag = dict;
    }

    /// <summary>
    /// Decodes a Bolt message from a struct view. The struct tag determines which decoder is used.
    /// </summary>
    public BoltMessage Decode(PackStreamStructView structView)
    {
        if (!_decodersByTag.TryGetValue(structView.Tag, out var decoder))
        {
            throw new KeyNotFoundException($"No message decoder registered for tag 0x{structView.Tag:X2}.");
        }

        return decoder.Decode(structView);
    }
}
