// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule(
    IDbContextFactory<SettingsDbContext> dbContextFactory,
    IMemoryCache memoryCache)
{
    private readonly MemoryCacheEntryOptions _cacheEntryOptions = new() { SlidingExpiration = TimeSpan.FromHours(2) };

    private readonly IDbContextFactory<SettingsDbContext> _dbContextFactory = dbContextFactory;
    private readonly IMemoryCache _memoryCache = memoryCache;
}
