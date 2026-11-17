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

using System.Threading;
using System.Threading.Tasks;

namespace Neo4j.Driver.Internal;

internal class BookmarkTracker : IBookmarkTracker
{
    private readonly IBookmarkManager? _bookmarkManager;
    private Bookmarks? _currentBookmarks;
    private string[] _initialBookmarks = [];

    public BookmarkTracker(SessionConfig config)
    {
        _bookmarkManager = config.BookmarkManager;
        if (config.Bookmarks != null)
        {
            _currentBookmarks = Bookmarks.From(config.Bookmarks);
            _initialBookmarks = _currentBookmarks.Values;
        }
    }

    public Bookmarks? GetCurrentBookmarks()
    {
        return _currentBookmarks;
    }

    public async Task<string[]> GetBookmarksToSendAsync(CancellationToken cancellationToken = default)
    {
        if (_bookmarkManager is not null)
        {
            var managed = await _bookmarkManager.GetBookmarksAsync(cancellationToken).ConfigureAwait(false);
            _currentBookmarks = Bookmarks.From([.. managed, .. _initialBookmarks]);
        }

        return _currentBookmarks?.Values ?? [];
    }

    public async Task UpdateBookmarksAsync(string[] bookmarks, CancellationToken cancellationToken = default)
    {
        _initialBookmarks = [];
        if (bookmarks.Length == 0)
        {
            return;
        }

        var previousBookmarks = _currentBookmarks?.Values ?? [];
        _currentBookmarks = Bookmarks.From(bookmarks);
        if (_bookmarkManager is not null)
        {
            await _bookmarkManager
                .UpdateBookmarksAsync(previousBookmarks, bookmarks, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
