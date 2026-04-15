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

namespace Neo4j.Driver.Internal.HomeDbCaching;

internal class HomeDbCache : IHomeDbCache
{
    private const int PurgeThreshold = 10_000;
    private const int PurgeAmount = PurgeThreshold / 10;
    private readonly LinkedList<CacheItem> _cachedItems = new();
    private readonly Dictionary<HomeDbCacheKey, LinkedListNode<CacheItem>> _cacheLookup = new();

    private readonly object _lock = new();

    public bool TryGetCached(HomeDbCacheKey key, out string value)
    {
        lock (_lock)
        {
            value = null;
            var found = _cacheLookup.TryGetValue(key, out var node);
            if (!found)
            {
                return false;
            }

            _cachedItems.Remove(node);
            _cachedItems.AddFirst(node);
            value = node.Value.DatabaseName;
            return true;
        }
    }

    public void AddOrUpdate(HomeDbCacheKey key, string value)
    {
        lock (_lock)
        {
            // if we already have an entry
            if (_cacheLookup.TryGetValue(key, out var node))
            {
                _cachedItems.Remove(node);
            }
            else
            {
                node = new LinkedListNode<CacheItem>(new CacheItem(key, value));
                _cacheLookup[key] = node;
            }

            node.Value.DatabaseName = value;
            _cachedItems.AddFirst(node);
            PurgeOldItems();
        }
    }

    private void PurgeOldItems()
    {
        lock (_lock)
        {
            if (_cachedItems.Count < PurgeThreshold)
            {
                return;
            }

            for (var i = 0; i < PurgeAmount; i++)
            {
                RemoveLastItem();
            }
        }
    }

    private void RemoveLastItem()
    {
        var last = _cachedItems.Last;

        if (last == null)
        {
            return;
        }

        _cacheLookup.Remove(last!.Value.Key);
        _cachedItems.RemoveLast();
    }

    private class CacheItem
    {
        public CacheItem(HomeDbCacheKey key, string databaseName)
        {
            Key = key;
            DatabaseName = databaseName;
        }

        public HomeDbCacheKey Key { get; }
        public string DatabaseName { get; set; }
    }
}
