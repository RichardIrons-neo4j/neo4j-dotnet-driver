﻿// Copyright (c) 2002-2022 "Neo4j,"
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

namespace Neo4j.Driver;

/// <summary>
/// Factory for <see cref="IBookmarkManager"/>.
/// </summary>
public interface IBookmarkManagerFactory
{
    /// <summary>
    /// Create an <see cref="IBookmarkManager"/> with specified configuration.
    /// </summary>
    /// <param name="config">Configuration object. If this is null or not specified,
    /// default configuration is used.</param>
    /// <returns>New configured instance of <see cref="IBookmarkManager"/>.</returns>
    IBookmarkManager NewBookmarkManager(BookmarkManagerConfig config = null);
}