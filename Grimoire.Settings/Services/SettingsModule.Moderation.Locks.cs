// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Helpers;
using Microsoft.EntityFrameworkCore;
using Lock = Grimoire.Settings.Domain.Lock;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public async Task<bool> IsChannelLocked(ChannelId channelId, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey.Locks(guildId);
        var locks = await this._cache.GetOrCreateAsync(cacheKey,
            guildId,
            async (guildIdState, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var results = await dbContext.Locks
                    .AsNoTracking()
                    .Where(x => x.GuildId == guildIdState)
                    .Select(@lock => @lock.ChannelId)
                    .ToHashSetAsync(ct);
                return results.ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);

        return locks.Contains(channelId);
    }

    public async Task<SettingsResult> AddLock(
        ModeratorId moderatorId,
        GuildId guildId,
        ChannelId channelId,
        PreviouslyAllowedPermissions previouslyAllowed,
        PreviouslyDeniedPermissions previouslyDenied,
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
            existingLock.Reason = reason;
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
        var cacheKey = CacheKey.Locks(guildId);
        await this._cache.RemoveAsync(cacheKey, cancellationToken);
        return SettingsResult.Written();
    }

    public async Task<SettingsResult<Lock?>> RemoveLock(ChannelId channelId, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingLocks = await dbContext.Locks
            .Where(x => x.ChannelId == channelId && x.GuildId == guildId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingLocks is null)
            return SettingsResult.Unchanged<Lock?>(null);
        dbContext.Locks.Remove(existingLocks);
        await dbContext.SaveChangesAsync(cancellationToken);
        var cacheKey = CacheKey.Locks(guildId);
        await this._cache.RemoveAsync(cacheKey, cancellationToken);
        return SettingsResult.Written<Lock?>(existingLocks);
    }

    public async IAsyncEnumerable<Lock> GetAllExpiredLocks(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var expiredLocks in dbContext.Locks
                           .AsNoTracking()
                           .Where(x => x.EndTime <= DateTimeOffset.UtcNow)
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return expiredLocks;
    }
}
