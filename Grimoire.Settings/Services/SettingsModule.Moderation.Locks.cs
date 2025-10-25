// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Lock = Grimoire.Settings.Domain.Lock;

namespace Grimoire.Settings.Services;

public partial class SettingsModule
{
    const string LocksCacheKeyPrefix = "Locks_{0}";

    public async Task<bool> IsChannelLocked(ulong channel, ulong guildId, CancellationToken cancellationToken = default)
    {
        var cacheKey = string.Format(LocksCacheKeyPrefix, guildId);
        var locks = await this._memoryCache.GetOrCreateAsync(cacheKey, async _ =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var results = await dbContext.Locks
                .AsNoTracking()
                .Where(x => x.GuildId == guildId)
                .Select(@lock => @lock.ChannelId)
                .ToHashSetAsync(cancellationToken);
            return results.ToFrozenSet();
        }, this._cacheEntryOptions);

        return locks?.Contains(channel) ?? false;
    }

    public async Task AddLock(
        ulong moderatorId,
        ulong guildId,
        ulong channelId,
        long previouslyAllowed,
        long previouslyDenied,
        string reason,
        DateTimeOffset lockEndTime,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingLock = await dbContext.Locks
            .Where(x => x.ChannelId == channelId && x.GuildId == guildId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingLock is not null)
        {
            existingLock.EndTime = lockEndTime;
            existingLock.ModeratorId = moderatorId;
        }
        else
        {
            var newLock = new Lock
            {
                ModeratorId = moderatorId,
                GuildId = guildId,
                ChannelId = channelId,
                EndTime = lockEndTime,
                PreviouslyAllowed = previouslyAllowed,
                PreviouslyDenied = previouslyDenied,
                Reason = reason
            };
            dbContext.Locks.Add(newLock);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        var cacheKey = string.Format(LocksCacheKeyPrefix, guildId);
        this._memoryCache.Remove(cacheKey);
    }
    public async Task<Lock?> RemoveLock(ulong channelId, ulong guildId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingLocks = await dbContext.Locks
            .Where(x => x.ChannelId == channelId && x.GuildId == guildId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingLocks is null)
            return null;
        dbContext.Locks.Remove(existingLocks);
        await dbContext.SaveChangesAsync(cancellationToken);
        var cacheKey = string.Format(LocksCacheKeyPrefix, guildId);
        this._memoryCache.Remove(cacheKey);
        return existingLocks;
    }

    public async IAsyncEnumerable<Lock> GetAllExpiredLocks([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var expiredLocks in dbContext.Locks
                           .AsNoTracking()
                           .Where(x => x.EndTime <= DateTime.UtcNow)
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return expiredLocks;
    }
}
