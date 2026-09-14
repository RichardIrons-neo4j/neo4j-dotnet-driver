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

using System.Diagnostics.CodeAnalysis;
using Neo4j.Driver.Internal.Caching;
using Neo4j.Driver.Internal.Services;

namespace Neo4j.Driver.Internal.Encryption;

internal class EncryptionKeyCache : IEncryptionKeyCache
{
    private readonly PerProfileBoundedCache<byte[]> _cache;

    public EncryptionKeyCache(IDateTimeProvider clock)
    {
        _cache = new PerProfileBoundedCache<byte[]>(clock);
    }

    public bool TryGet(IEnvelopeEncryptionProfile profile, string keyId, [NotNullWhen(true)] out byte[]? value)
    {
        var config = profile.KeyCacheConfig;
        if (config is null)
        {
            value = null;
            return false;
        }

        return _cache.TryGet(profile.Name, config, keyId, out value);
    }

    public void Set(IEnvelopeEncryptionProfile profile, string keyId, byte[] value)
    {
        var config = profile.KeyCacheConfig;
        if (config is null)
        {
            return;
        }

        _cache.Set(profile.Name, config, keyId, value);
    }
}
