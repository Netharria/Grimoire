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

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public async Task<bool> IsChannelLocked(ChannelId channelId, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var locks = await this._cache.GetOrCreateAsync(CacheKey.ChannelLocks(guildId),
            guildId,
            async (state, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var results = await dbContext.ChannelLocks
                    .AsNoTracking()
                    .OfType<ChannelLockEvent>()
                    .Where(x => x.GuildId == state)
                    .Where(x => !dbContext.ChannelLocks
                        .Any(y => y.ChannelId == x.ChannelId &&
                                  y.GuildId == x.GuildId &&
                                  y.SetAt > x.SetAt))
                    .Select(x => x.ChannelId)
                    .ToHashSetAsync(ct);
                return results.ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);

        return locks.Contains(channelId);
    }

    public async Task<SettingsResult> AddChannelLock(
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
        var existingPermissions = await dbContext.ChannelLocks
            .AsNoTracking()
            .OfType<ChannelLockEvent>()
            .Where(x => x.ChannelId == channelId && x.GuildId == guildId)
            .Where(x => !dbContext.ChannelLocks
                .Any(y => y.ChannelId == x.ChannelId &&
                          y.GuildId == x.GuildId &&
                          y.SetAt > x.SetAt))
            .Select(x => new { x.PreviouslyAllowed, x.PreviouslyDenied })
            .FirstOrDefaultAsync(cancellationToken);

        dbContext.ChannelLocks.Add(new ChannelLockEvent
        {
            ChannelId = channelId,
            GuildId = guildId,
            ModeratorId = moderatorId,
            Reason = reason,
            EndTime = lockEndTime,
            SetAt = DateTimeOffset.UtcNow,
            PreviouslyAllowed = existingPermissions?.PreviouslyAllowed ?? previouslyAllowed,
            PreviouslyDenied = existingPermissions?.PreviouslyDenied ?? previouslyDenied,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await this._cache.RemoveAsync(CacheKey.ChannelLocks(guildId), cancellationToken);
        return SettingsResult.Written();
    }

    public async Task<SettingsResult<ChannelLockEvent?>> RemoveChannelLock(
        ChannelId channelId,
        GuildId guildId,
        ModeratorId moderatorId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingLock = await dbContext.ChannelLocks
            .OfType<ChannelLockEvent>()
            .Where(x => x.ChannelId == channelId && x.GuildId == guildId)
            .Where(x => !dbContext.ChannelLocks
                .Any(y => y.ChannelId == x.ChannelId &&
                          y.GuildId == x.GuildId &&
                          y.SetAt > x.SetAt))
            .FirstOrDefaultAsync(cancellationToken);
        if (existingLock is null)
            return SettingsResult.Unchanged<ChannelLockEvent?>(null);

        dbContext.ChannelLocks.Add(new ChannelUnlockEvent
        {
            ChannelId = channelId,
            GuildId = guildId,
            ModeratorId = moderatorId,
            Reason = string.Empty,
            SetAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await this._cache.RemoveAsync(CacheKey.ChannelLocks(guildId), cancellationToken);
        return SettingsResult.Written<ChannelLockEvent?>(existingLock);
    }

    public async IAsyncEnumerable<ChannelLockEvent> GetAllExpiredChannelLocks(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var expired in dbContext.ChannelLocks
                           .AsNoTracking()
                           .OfType<ChannelLockEvent>()
                           .Where(x => x.EndTime <= DateTimeOffset.UtcNow)
                           .Where(x => !dbContext.ChannelLocks
                               .Any(y => y.ChannelId == x.ChannelId &&
                                         y.GuildId == x.GuildId &&
                                         y.SetAt > x.SetAt))
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return expired;
    }

    public async Task<bool> IsThreadLocked(ChannelId channelId, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var locks = await this._cache.GetOrCreateAsync(CacheKey.ThreadLocks(guildId),
            guildId,
            async (state, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var results = await dbContext.ThreadLocks
                    .AsNoTracking()
                    .OfType<ThreadLockEvent>()
                    .Where(x => x.GuildId == state)
                    .Where(x => !dbContext.ThreadLocks
                        .Any(y => y.ChannelId == x.ChannelId &&
                                  y.GuildId == x.GuildId &&
                                  y.SetAt > x.SetAt))
                    .Select(x => x.ChannelId)
                    .ToHashSetAsync(ct);
                return results.ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);

        return locks.Contains(channelId);
    }

    public async Task<SettingsResult> AddThreadLock(
        ModeratorId moderatorId,
        GuildId guildId,
        ChannelId channelId,
        string reason,
        DateTimeOffset lockEndTime,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.ThreadLocks.Add(new ThreadLockEvent
        {
            ChannelId = channelId,
            GuildId = guildId,
            ModeratorId = moderatorId,
            Reason = reason,
            EndTime = lockEndTime,
            SetAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await this._cache.RemoveAsync(CacheKey.ThreadLocks(guildId), cancellationToken);
        return SettingsResult.Written();
    }

    public async Task<SettingsResult<ThreadLockEvent?>> RemoveThreadLock(
        ChannelId channelId,
        GuildId guildId,
        ModeratorId moderatorId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingLock = await dbContext.ThreadLocks
            .OfType<ThreadLockEvent>()
            .Where(x => x.ChannelId == channelId && x.GuildId == guildId)
            .Where(x => !dbContext.ThreadLocks
                .Any(y => y.ChannelId == x.ChannelId &&
                          y.GuildId == x.GuildId &&
                          y.SetAt > x.SetAt))
            .FirstOrDefaultAsync(cancellationToken);
        if (existingLock is null)
            return SettingsResult.Unchanged<ThreadLockEvent?>(null);

        dbContext.ThreadLocks.Add(new ThreadUnlockEvent
        {
            ChannelId = channelId,
            GuildId = guildId,
            ModeratorId = moderatorId,
            Reason = string.Empty,
            SetAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await this._cache.RemoveAsync(CacheKey.ThreadLocks(guildId), cancellationToken);
        return SettingsResult.Written<ThreadLockEvent?>(existingLock);
    }

    public async IAsyncEnumerable<ThreadLockEvent> GetAllExpiredThreadLocks(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var expired in dbContext.ThreadLocks
                           .AsNoTracking()
                           .OfType<ThreadLockEvent>()
                           .Where(x => x.EndTime <= DateTimeOffset.UtcNow)
                           .Where(x => !dbContext.ThreadLocks
                               .Any(y => y.ChannelId == x.ChannelId &&
                                         y.GuildId == x.GuildId &&
                                         y.SetAt > x.SetAt))
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return expired;
    }
}
