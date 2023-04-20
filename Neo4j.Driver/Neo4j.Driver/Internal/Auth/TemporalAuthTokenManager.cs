﻿// Copyright (c) "Neo4j"
// Neo4j Sweden AB [http://neo4j.com]
//
// This file is part of Neo4j.
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

using System;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver.Auth;
using Neo4j.Driver.Internal.Services;

namespace Neo4j.Driver.Internal.Auth;

internal class TemporalAuthTokenManager : IAuthTokenManager
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly Func<Task<TemporalAuthData>> _tokenProviderAsync;
    private Task<TemporalAuthData> _lastAuthRequest;
    private TemporalAuthData _currentAuthData;
    private SemaphoreSlim _sync;

    /// <summary>
    /// Constructs a new instance of the <see cref="TemporalAuthTokenManager"/> class.
    /// </summary>
    /// <param name="tokenProviderAsync">The sync method that will be used to obtain new tokens.</param>
    public TemporalAuthTokenManager(Func<Task<TemporalAuthData>> tokenProviderAsync)
        : this(new DateTimeProvider(), tokenProviderAsync)
    {
    }

    internal TemporalAuthTokenManager(
        IDateTimeProvider dateTimeProvider,
        Func<Task<TemporalAuthData>> tokenProviderAsync)
    {
        _dateTimeProvider = dateTimeProvider;
        _tokenProviderAsync = tokenProviderAsync;
        _sync = new SemaphoreSlim(1);
    }

    /// <inheritdoc/>
    public async Task<IAuthToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);

        try
        {
            if (_currentAuthData is not null && _currentAuthData.Expiry > _dateTimeProvider.Now())
            {
                return _currentAuthData.Token;
            }

            if (_lastAuthRequest is null)
            {
                ScheduleTokenFetch();
            }

            _currentAuthData = await _lastAuthRequest!;
            _lastAuthRequest = null;
            return _currentAuthData.Token;
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc/>
    public async Task OnTokenExpiredAsync(IAuthToken token, CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);

        try
        {
            if (token == _currentAuthData?.Token && _lastAuthRequest is null)
            {
                ScheduleTokenFetch();
            }
        }
        finally
        {
            _sync.Release();
        }
    }

    private void ScheduleTokenFetch()
    {
        _currentAuthData = null;
        _lastAuthRequest = _tokenProviderAsync(); // storing the task here, not waiting for the token
    }
}