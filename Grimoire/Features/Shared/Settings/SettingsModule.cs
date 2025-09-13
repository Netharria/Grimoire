// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Features.Shared.Settings;

public class SettingsModule(IDbContextFactory<GrimoireDbContext> dbContextFactory, IMemoryCache memoryCache)
{
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly MemoryCacheEntryOptions _cacheEntryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
    };
    const string CacheKeyPrefix = "GuildSettings_{0}";
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    public async Task<Guild?> GetGuildSettings(ulong guildId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = string.Format(CacheKeyPrefix, guildId);
        return await this._memoryCache.GetOrCreateAsync(cacheKey, async _ =>
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Guilds
                .AsSplitQuery()
                .Where(x => x.Id == guildId)
                .Include(x => x.ModerationSettings)
                .Include(x => x.CommandsSettings)
                .Include(x => x.LevelSettings)
                .Include(x => x.MessageLogSettings)
                .Include(x => x.UserLogSettings)
                .Include(x => x.IgnoredChannels)
                .Include(x => x.IgnoredMembers)
                .Include(x => x.IgnoredRoles)
                .Include(x => x.MessageLogChannelOverrides)
                .Include(x => x.Rewards)
                .Include(x => x.Trackers)
                .FirstOrDefaultAsync(cancellationToken);
        }, this._cacheEntryOptions);
    }

    public async Task UpdateGuildSettings(Guild guild, CancellationToken cancellationToken = default)
    {
        var cacheKey = string.Format(CacheKeyPrefix, guild.Id);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Guilds.Update(guild);
        await dbContext.SaveChangesAsync(cancellationToken);
        this._memoryCache.Remove(cacheKey);
    }

    public async Task<bool> IsModuleEnabled(Module moduleType, ulong guildId, CancellationToken stoppingToken = default)
    {
        var guildSettings = await this.GetGuildSettings(guildId, stoppingToken);
        if (guildSettings is null) return false;
        return moduleType switch
        {
            Module.Leveling => guildSettings.LevelSettings.ModuleEnabled,
            Module.UserLog => guildSettings.UserLogSettings.ModuleEnabled,
            Module.Moderation => guildSettings.ModerationSettings.ModuleEnabled,
            Module.MessageLog => guildSettings.MessageLogSettings.ModuleEnabled,
            Module.Commands => guildSettings.CommandsSettings.ModuleEnabled,
            Module.General => true,
            _ => throw new ArgumentOutOfRangeException(nameof(moduleType), moduleType, "Unknown module type")
        };
    }
}
