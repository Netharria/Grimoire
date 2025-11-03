// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    private const string TrackersCacheKeyPrefix = "Trackers_{0}";

    public async Task<ChannelId?> GetTrackerChannelAsync(UserId memberId, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = string.Format(TrackersCacheKeyPrefix, guildId);
        var trackers = await this._memoryCache.GetOrCreateAsync(cacheKey, async _ =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var results = await dbContext.Trackers
                .AsNoTracking()
                .Where(x => x.GuildId == guildId)
                .ToDictionaryAsync(
                    tracker => tracker.UserId,
                    tracker => tracker.LogChannelId,
                    cancellationToken);
            return results.ToFrozenDictionary();
        }, this._cacheEntryOptions);

        return trackers?.GetValueOrDefault(memberId);
    }

    public async Task AddTracker(
        UserId memberId,
        ModeratorId moderatorId,
        GuildId guildId,
        ChannelId channelId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingTracker = await dbContext.Trackers
            .Where(x => x.UserId == memberId && x.GuildId == guildId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingTracker is not null)
        {
            existingTracker.LogChannelId = channelId;
            existingTracker.EndTime = DateTime.UtcNow.Add(duration);
            existingTracker.ModeratorId = moderatorId;
        }
        else
        {
            var newTracker = new Tracker
            {
                UserId = memberId,
                ModeratorId = moderatorId,
                GuildId = guildId,
                LogChannelId = channelId,
                EndTime = DateTime.UtcNow.Add(duration)
            };
            dbContext.Trackers.Add(newTracker);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var cacheKey = string.Format(TrackersCacheKeyPrefix, guildId);
        this._memoryCache.Remove(cacheKey);
    }

    public async Task<Tracker?> RemoveTracker(UserId memberId, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingTracker = await dbContext.Trackers
            .Where(x => x.UserId == memberId && x.GuildId == guildId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingTracker is null)
            return null;
        dbContext.Trackers.Remove(existingTracker);
        await dbContext.SaveChangesAsync(cancellationToken);
        var cacheKey = string.Format(TrackersCacheKeyPrefix, guildId);
        this._memoryCache.Remove(cacheKey);
        return existingTracker;
    }

    public async Task<IReadOnlyList<Tracker>> RemoveAllExpiredTrackers(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var expiredTrackers = await dbContext.Trackers
            .Where(x => x.EndTime <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);
        dbContext.Trackers.RemoveRange(expiredTrackers);
        await dbContext.SaveChangesAsync(cancellationToken);
        var affectedGuilds = expiredTrackers.Select(x => x.GuildId).Distinct();
        foreach (var guildId in affectedGuilds)
        {
            var cacheKey = string.Format(TrackersCacheKeyPrefix, guildId);
            this._memoryCache.Remove(cacheKey);
        }

        return expiredTrackers;
    }
}
