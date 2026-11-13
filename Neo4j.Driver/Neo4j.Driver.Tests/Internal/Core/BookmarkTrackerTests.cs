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

using FluentAssertions;
using Neo4j.Driver.Internal;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.Core;

public class BookmarkTrackerTests
{
    [Fact]
    public void CurrentBookmarks_IsNull_WhenTheSessionHasNoBookmarksYet()
    {
        var subject = new BookmarkTracker(SessionConfig.Builder.Build());

        var bookmarks = subject.CurrentBookmarks;

        bookmarks.Should().BeNull();
    }

    [Fact]
    public void CurrentBookmarks_IsEmpty_WhenTheSessionWasGivenAnEmptyList()
    {
        var subject = new BookmarkTracker(SessionConfig.Builder.WithBookmarks().Build());

        var bookmarks = subject.CurrentBookmarks;

        bookmarks!.Values.Should().BeEmpty();
    }

    [Fact]
    public void UpdateBookmarks_KeepsThePreviousBookmarks_WhenTheUpdateHasNone()
    {
        var subject = new BookmarkTracker(SessionConfig.Builder.Build());
        subject.UpdateBookmarks(["neo4j:bookmark:v1:tx1"]);

        subject.UpdateBookmarks([]);
        var bookmarks = subject.CurrentBookmarks;

        bookmarks!.Values.Should().Equal("neo4j:bookmark:v1:tx1");
    }

    [Fact]
    public void UpdateBookmarks_LeavesNoBookmarks_WhenTheUpdateHasNoneAndThereWereNone()
    {
        var subject = new BookmarkTracker(SessionConfig.Builder.Build());

        subject.UpdateBookmarks([]);
        var bookmarks = subject.CurrentBookmarks;

        bookmarks.Should().BeNull();
    }
}
