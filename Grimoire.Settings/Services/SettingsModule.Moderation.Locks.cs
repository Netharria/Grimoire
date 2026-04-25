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
    public async Task<Result<bool>> IsChannelLocked(ChannelId channelId, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var locks = await this._cache.GetOrCreateAsync(CacheKey.ChannelLocks(guildId),
            guildId,
            async (state, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var results = await dbContext.ChannelLocks
                    .AsNoTracking()
                    .OfType<ChannelLocked>()
                    .Where(x => x.GuildId == state)
                    // ReSharper disable once AccessToDisposedClosure
                    .Where(x => !dbContext.ChannelLocks
                        .Any(y => y.ChannelId == x.ChannelId &&
                                  y.GuildId == x.GuildId &&
                                  y.SetAt > x.SetAt))
                    .Select(x => x.ChannelId)
                    .ToHashSetAsync(ct);
                return results.ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);

        return Result<bool>.Ok(locks.Contains(channelId));
    }

    public async Task<Result<ChannelLocked>> AddChannelLock(
        ModeratorId moderatorId,
        GuildId guildId,
        ChannelId channelId,
        PreviouslyAllowedPermissions previouslyAllowed,
        PreviouslyDeniedPermissions previouslyDenied,
        string reason,
        DateTimeOffset lockEndTime,
        CancellationToken cancellationToken = default)
    {
        if (ModerationReason.Create(reason) is not Validation<ModerationReason>.Valid(var validReason))
            return Result<ChannelLocked>.Fail(new Error("moderation-reason.invalid",
                "Reason must be 1\u20134096 non-whitespace characters."));

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingPermissions = await dbContext.ChannelLocks
            .AsNoTracking()
            .OfType<ChannelLocked>()
            .Where(x => x.ChannelId == channelId && x.GuildId == guildId)
            // ReSharper disable once AccessToDisposedClosure
            .Where(x => !dbContext.ChannelLocks
                .Any(y => y.ChannelId == x.ChannelId &&
                          y.GuildId == x.GuildId &&
                          y.SetAt > x.SetAt))
            .Select(x => new { x.PreviouslyAllowed, x.PreviouslyDenied })
            .FirstOrDefaultAsync(cancellationToken);

        var setAt = DateTimeOffset.UtcNow;
        var finalAllowed = existingPermissions?.PreviouslyAllowed ?? previouslyAllowed;
        var finalDenied = existingPermissions?.PreviouslyDenied ?? previouslyDenied;

        if (ChannelLocked.Create(moderatorId, validReason, channelId, guildId, setAt, finalAllowed, finalDenied,
                lockEndTime)
            is not Validation<ChannelLocked>.Valid(var lockEvent))
            return Result<ChannelLocked>.Fail(new Error("channel-lock.end-time.invalid",
                "Lock end time must be after the current time."));

        dbContext.ChannelLocks.Add(lockEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        await this._cache.RemoveAsync(CacheKey.ChannelLocks(guildId), cancellationToken);
        return Result<ChannelLocked>.Ok(lockEvent);
    }

    public async Task<Result<ChannelLocked>> RemoveChannelLock(
        ChannelId channelId,
        GuildId guildId,
        ModeratorId moderatorId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingLock = await dbContext.ChannelLocks
            .OfType<ChannelLocked>()
            .Where(x => x.ChannelId == channelId && x.GuildId == guildId)
            // ReSharper disable once AccessToDisposedClosure
            .Where(x => !dbContext.ChannelLocks
                .Any(y => y.ChannelId == x.ChannelId &&
                          y.GuildId == x.GuildId &&
                          y.SetAt > x.SetAt))
            .FirstOrDefaultAsync(cancellationToken);
        if (existingLock is null)
            return new Result<ChannelLocked>.NotFound(
                new Error("channel-lock.not-found", "The channel is not currently locked."));

        if (ChannelUnlocked.Create(moderatorId, channelId, guildId, DateTimeOffset.UtcNow)
            is not Validation<ChannelUnlocked>.Valid(var unlock))
            return Result<ChannelLocked>.Fail(
                new Error("channel-unlock.invalid", "Unable to create channel unlock event."));
        dbContext.ChannelLocks.Add(unlock);
        await dbContext.SaveChangesAsync(cancellationToken);
        await this._cache.RemoveAsync(CacheKey.ChannelLocks(guildId), cancellationToken);
        return Result<ChannelLocked>.Ok(existingLock);
    }

    public async IAsyncEnumerable<ChannelLocked> GetAllExpiredChannelLocks(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var expired in dbContext.ChannelLocks
                           .AsNoTracking()
                           .OfType<ChannelLocked>()
                           .Where(x => x.EndTime <= DateTimeOffset.UtcNow)
                           // ReSharper disable once AccessToDisposedClosure
                           .Where(x => !dbContext.ChannelLocks
                               .Any(y => y.ChannelId == x.ChannelId &&
                                         y.GuildId == x.GuildId &&
                                         y.SetAt > x.SetAt))
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return expired;
    }

    public async Task<Result<bool>> IsThreadLocked(ChannelId channelId, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var locks = await this._cache.GetOrCreateAsync(CacheKey.ThreadLocks(guildId),
            guildId,
            async (state, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var results = await dbContext.ThreadLocks
                    .AsNoTracking()
                    .OfType<ThreadLocked>()
                    .Where(x => x.GuildId == state)
                    // ReSharper disable once AccessToDisposedClosure
                    .Where(x => !dbContext.ThreadLocks
                        .Any(y => y.ChannelId == x.ChannelId &&
                                  y.GuildId == x.GuildId &&
                                  y.SetAt > x.SetAt))
                    .Select(x => x.ChannelId)
                    .ToHashSetAsync(ct);
                return results.ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);

        return Result<bool>.Ok(locks.Contains(channelId));
    }

    public async Task<Result<ThreadLocked>> AddThreadLock(
        ModeratorId moderatorId,
        GuildId guildId,
        ChannelId channelId,
        string reason,
        DateTimeOffset lockEndTime,
        CancellationToken cancellationToken = default)
    {
        if (ModerationReason.Create(reason) is not Validation<ModerationReason>.Valid(var validReason))
            return Result<ThreadLocked>.Fail(new Error("moderation-reason.invalid",
                "Reason must be 1\u20134096 non-whitespace characters."));

        var setAt = DateTimeOffset.UtcNow;

        if (ThreadLocked.Create(moderatorId, validReason, channelId, guildId, setAt, lockEndTime)
            is not Validation<ThreadLocked>.Valid(var lockEvent))
            return Result<ThreadLocked>.Fail(new Error("thread-lock.end-time.invalid",
                "Lock end time must be after the current time."));

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.ThreadLocks.Add(lockEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        await this._cache.RemoveAsync(CacheKey.ThreadLocks(guildId), cancellationToken);
        return Result<ThreadLocked>.Ok(lockEvent);
    }

    public async Task<Result<ThreadLocked>> RemoveThreadLock(
        ChannelId channelId,
        GuildId guildId,
        ModeratorId moderatorId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingLock = await dbContext.ThreadLocks
            .OfType<ThreadLocked>()
            .Where(x => x.ChannelId == channelId && x.GuildId == guildId)
            // ReSharper disable once AccessToDisposedClosure
            .Where(x => !dbContext.ThreadLocks
                .Any(y => y.ChannelId == x.ChannelId &&
                          y.GuildId == x.GuildId &&
                          y.SetAt > x.SetAt))
            .FirstOrDefaultAsync(cancellationToken);
        if (existingLock is null)
            return new Result<ThreadLocked>.NotFound(
                new Error("thread-lock.not-found", "The thread is not currently locked."));

        if (ThreadUnlocked.Create(moderatorId, channelId, guildId, DateTimeOffset.UtcNow)
            is not Validation<ThreadUnlocked>.Valid(var unlock))
            return Result<ThreadLocked>.Fail(
                new Error("thread-unlock.invalid", "Unable to create thread unlock event."));
        dbContext.ThreadLocks.Add(unlock);
        await dbContext.SaveChangesAsync(cancellationToken);
        await this._cache.RemoveAsync(CacheKey.ThreadLocks(guildId), cancellationToken);
        return Result<ThreadLocked>.Ok(existingLock);
    }

    public async IAsyncEnumerable<ThreadLocked> GetAllExpiredThreadLocks(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var expired in dbContext.ThreadLocks
                           .AsNoTracking()
                           .OfType<ThreadLocked>()
                           .Where(x => x.EndTime <= DateTimeOffset.UtcNow)
                           // ReSharper disable once AccessToDisposedClosure
                           .Where(x => !dbContext.ThreadLocks
                               .Any(y => y.ChannelId == x.ChannelId &&
                                         y.GuildId == x.GuildId &&
                                         y.SetAt > x.SetAt))
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return expired;
    }
}
