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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Neo4j.Driver.Internal;
using Xunit;

namespace Neo4j.Driver.Tests.Internal.Core;

public class BookmarkTrackerTests
{
    private const string Initial = "neo4j:bookmark:v1:initial";
    private const string FromManager = "neo4j:bookmark:v1:manager";
    private const string FromServer = "neo4j:bookmark:v1:server";

    private readonly Mock<IBookmarkManager> _bookmarkManager = new();

    public BookmarkTrackerTests()
    {
        _bookmarkManager
            .Setup(x => x.GetBookmarksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([FromManager]);
    }

    private BookmarkTracker TrackerWithBookmarkManager()
    {
        var config = SessionConfig.Builder
            .WithBookmarks(Bookmarks.From(Initial))
            .WithBookmarkManager(_bookmarkManager.Object)
            .Build();

        return new BookmarkTracker(config);
    }

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
    public async Task UpdateBookmarksAsync_KeepsThePreviousBookmarks_WhenTheUpdateHasNone()
    {
        var subject = new BookmarkTracker(SessionConfig.Builder.Build());
        await subject.UpdateBookmarksAsync(["neo4j:bookmark:v1:tx1"], TestContext.Current.CancellationToken);

        await subject.UpdateBookmarksAsync([], TestContext.Current.CancellationToken);
        var bookmarks = subject.CurrentBookmarks;

        bookmarks!.Values.Should().Equal("neo4j:bookmark:v1:tx1");
    }

    [Fact]
    public async Task UpdateBookmarksAsync_LeavesNoBookmarks_WhenTheUpdateHasNoneAndThereWereNone()
    {
        var subject = new BookmarkTracker(SessionConfig.Builder.Build());

        await subject.UpdateBookmarksAsync([], TestContext.Current.CancellationToken);
        var bookmarks = subject.CurrentBookmarks;

        bookmarks.Should().BeNull();
    }

    [Fact]
    public async Task GetBookmarksToSendAsync_WithoutABookmarkManager_ReturnsTheCurrentBookmarks()
    {
        var subject = new BookmarkTracker(SessionConfig.Builder.WithBookmarks(Bookmarks.From(Initial)).Build());

        var bookmarks = await subject.GetBookmarksToSendAsync(TestContext.Current.CancellationToken);

        bookmarks.Should().Equal(Initial);
    }

    [Fact]
    public async Task GetBookmarksToSendAsync_WithABookmarkManager_ReturnsItsBookmarksAndTheInitialOnes()
    {
        var subject = TrackerWithBookmarkManager();

        var bookmarks = await subject.GetBookmarksToSendAsync(TestContext.Current.CancellationToken);

        bookmarks.Should().Equal(FromManager, Initial);
    }

    [Fact]
    public async Task GetBookmarksToSendAsync_WithABookmarkManager_MakesThemTheCurrentBookmarks()
    {
        var subject = TrackerWithBookmarkManager();

        await subject.GetBookmarksToSendAsync(TestContext.Current.CancellationToken);
        var bookmarks = subject.CurrentBookmarks;

        bookmarks!.Values.Should().Equal(FromManager, Initial);
    }

    [Fact]
    public async Task GetBookmarksToSendAsync_WithABookmarkManager_DropsTheInitialOnesAfterAnyUpdate()
    {
        var subject = TrackerWithBookmarkManager();
        await subject.UpdateBookmarksAsync([], TestContext.Current.CancellationToken);

        var bookmarks = await subject.GetBookmarksToSendAsync(TestContext.Current.CancellationToken);

        bookmarks.Should().Equal(FromManager);
    }

    [Fact]
    public async Task UpdateBookmarksAsync_WithABookmarkManager_TellsItThePreviousAndNewBookmarks_OfNonEmptyUpdates()
    {
        var updates = new List<(string[] Previous, string[] New)>();
        _bookmarkManager
            .Setup(x => x.UpdateBookmarksAsync(It.IsAny<string[]>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .Callback<string[], string[], CancellationToken>((previous, next, _) => updates.Add((previous, next)))
            .Returns(Task.CompletedTask);

        var subject = TrackerWithBookmarkManager();
        await subject.GetBookmarksToSendAsync(TestContext.Current.CancellationToken);
        await subject.UpdateBookmarksAsync([], TestContext.Current.CancellationToken);
        await subject.UpdateBookmarksAsync([FromServer], TestContext.Current.CancellationToken);

        updates.Should().ContainSingle()
            .Which.Should().BeEquivalentTo((Previous: new[] { FromManager, Initial }, New: new[] { FromServer }));
    }
}
