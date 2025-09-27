// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule(IDbContextFactory<SettingsDbContext> dbContextFactory, IMemoryCache memoryCache)
{
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly MemoryCacheEntryOptions _cacheEntryOptions = new()
    {
        SlidingExpiration = TimeSpan.FromHours(2)
    };
    const string CacheKeyPrefix = "GuildSettings_{0}";
    private readonly IDbContextFactory<SettingsDbContext> _dbContextFactory = dbContextFactory;

    public async Task<GuildSettings> GetGuildSettings(ulong guildId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = string.Format(CacheKeyPrefix, guildId);
        var guildSettings = await this._memoryCache.GetOrCreateAsync(cacheKey, async _ =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.GuildSettings
                .AsSplitQuery()
                .FirstOrDefaultAsync(cancellationToken);
        }, this._cacheEntryOptions);
        return guildSettings ?? await this.AddNewGuildSettings(guildId, cancellationToken);
    }

    private async Task<GuildSettings> AddNewGuildSettings(ulong guildId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var newSettings = new GuildSettings
        {
            Id = guildId
        };
        dbContext.GuildSettings.Add(newSettings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return newSettings;
    }

    public async Task UpdateGuildSettings(GuildSettings guild, CancellationToken cancellationToken = default)
    {
        var cacheKey = string.Format(CacheKeyPrefix, guild.Id);
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.GuildSettings.Update(guild);
        await dbContext.SaveChangesAsync(cancellationToken);
        this._memoryCache.Remove(cacheKey);
    }
}
