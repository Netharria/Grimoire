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
    public Task<Result<bool>> IsChannelLocked(ChannelId channelId, GuildId guildId,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            var locks = await this._cache.GetOrCreateAsync(CacheKey.ChannelLocks(guildId),
                guildId,
                async (state, innerCt) =>
                {
                    await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(innerCt);
                    var results = await ActiveChannelLocks(dbContext)
                        .AsNoTracking()
                        .Where(x => x.GuildId == state)
                        .Select(x => x.ChannelId)
                        .ToHashSetAsync(innerCt);
                    return results.ToFrozenSet();
                }, this._cacheEntryOptions, cancellationToken: ct);
            return Result<bool>.Ok(locks.Contains(channelId));
        }, new Error("channel-lock.lookup-failed", "Failed to retrieve channel lock state."), cancellationToken);

    /// <summary>The lock currently in force on the channel, without changing anything.</summary>
    public Task<Result<ChannelLocked>> GetActiveChannelLockAsync(
        ChannelId channelId,
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            var activeLock = await ActiveChannelLocks(dbContext)
                .AsNoTracking()
                .Where(x => x.ChannelId == channelId && x.GuildId == guildId)
                .FirstOrDefaultAsync(ct);
            return activeLock is null
                ? new Result<ChannelLocked>.NotFound(
                    new Error("channel-lock.not-found", "The channel is not currently locked."))
                : Result<ChannelLocked>.Ok(activeLock);
        }, new Error("channel-lock.lookup-failed", "Failed to retrieve channel lock state."), cancellationToken);

    public Task<Result<ChannelLocked>> AddChannelLock(
        ChannelLocked channelLock,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            var existingLock = await ActiveChannelLocks(dbContext)
                .Where(x => x.ChannelId == channelLock.ChannelId && x.GuildId == channelLock.GuildId)
                .FirstOrDefaultAsync(ct);

            // A lock on a locked channel replaces it, keeping the permissions from before the first lock.
            var validLock = existingLock is null
                ? channelLock
                : channelLock.WithPermissions(existingLock.PreviouslyAllowed, existingLock.PreviouslyDenied);
            existingLock?.Close(validLock.SetAt);
            dbContext.ChannelLocks.Add(validLock);
            await dbContext.SaveChangesAsync(ct);
            await this._cache.RemoveAsync(CacheKey.ChannelLocks(validLock.GuildId), ct);
            return Result<ChannelLocked>.Ok(validLock);
        }, new Error("channel-lock.save-failed", "Could not save the channel lock."), cancellationToken);

    public Task<Result<ChannelLocked>> RemoveChannelLock(
        ChannelUnlocked lockAction,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            var existingLock = await ActiveChannelLocks(dbContext)
                .Where(x => x.ChannelId == lockAction.ChannelId && x.GuildId == lockAction.GuildId)
                .FirstOrDefaultAsync(ct);
            if (existingLock is null)
                return new Result<ChannelLocked>.NotFound(
                    new Error("channel-lock.not-found", "The channel is not currently locked."));

            existingLock.Close(lockAction.SetAt);
            dbContext.ChannelLocks.Add(lockAction);
            await dbContext.SaveChangesAsync(ct);
            await this._cache.RemoveAsync(CacheKey.ChannelLocks(lockAction.GuildId), ct);
            return Result<ChannelLocked>.Ok(existingLock);
        }, new Error("channel-lock.save-failed", "Could not unlock the channel."), cancellationToken);

    public async IAsyncEnumerable<ChannelLocked> GetAllExpiredChannelLocks(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<ChannelLocked> expired;
        try
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            expired = await ActiveChannelLocks(dbContext)
                .AsNoTracking()
                .Where(x => x.EndTime <= DateTimeOffset.UtcNow)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogOperationFailure(this._logger, ex.Message, ex);
            yield break;
        }

        foreach (var item in expired)
            yield return item;
    }

    public Task<Result<bool>> IsThreadLocked(ChannelId channelId, GuildId guildId,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            var locks = await this._cache.GetOrCreateAsync(CacheKey.ThreadLocks(guildId),
                guildId,
                async (state, innerCt) =>
                {
                    await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(innerCt);
                    var results = await ActiveThreadLocks(dbContext)
                        .AsNoTracking()
                        .Where(x => x.GuildId == state)
                        .Select(x => x.ChannelId)
                        .ToHashSetAsync(innerCt);
                    return results.ToFrozenSet();
                }, this._cacheEntryOptions, cancellationToken: ct);
            return Result<bool>.Ok(locks.Contains(channelId));
        }, new Error("thread-lock.lookup-failed", "Failed to retrieve thread lock state."), cancellationToken);

    public Task<Result<ThreadLocked>> AddThreadLock(
        ThreadLocked threadLock,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            // A lock on a locked thread replaces it.
            (await ActiveThreadLocks(dbContext)
                .Where(x => x.ChannelId == threadLock.ChannelId && x.GuildId == threadLock.GuildId)
                .FirstOrDefaultAsync(ct))?.Close(threadLock.SetAt);
            dbContext.ThreadLocks.Add(threadLock);
            await dbContext.SaveChangesAsync(ct);
            await this._cache.RemoveAsync(CacheKey.ThreadLocks(threadLock.GuildId), ct);
            return Result<ThreadLocked>.Ok(threadLock);
        }, new Error("thread-lock.save-failed", "Could not save the thread lock."), cancellationToken);

    public Task<Result<ThreadLocked>> RemoveThreadLock(
        ThreadUnlocked lockAction,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            var existingLock = await ActiveThreadLocks(dbContext)
                .Where(x => x.ChannelId == lockAction.ChannelId && x.GuildId == lockAction.GuildId)
                .FirstOrDefaultAsync(ct);
            if (existingLock is null)
                return new Result<ThreadLocked>.NotFound(
                    new Error("thread-lock.not-found", "The thread is not currently locked."));
            existingLock.Close(lockAction.SetAt);
            dbContext.ThreadLocks.Add(lockAction);
            await dbContext.SaveChangesAsync(ct);
            await this._cache.RemoveAsync(CacheKey.ThreadLocks(lockAction.GuildId), ct);
            return Result<ThreadLocked>.Ok(existingLock);
        }, new Error("thread-lock.save-failed", "Could not unlock the thread."), cancellationToken);

    public async IAsyncEnumerable<ThreadLocked> GetAllExpiredThreadLocks(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<ThreadLocked> expired;
        try
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            expired = await ActiveThreadLocks(dbContext)
                .AsNoTracking()
                .Where(x => x.EndTime <= DateTimeOffset.UtcNow)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogOperationFailure(this._logger, ex.Message, ex);
            yield break;
        }

        foreach (var item in expired)
            yield return item;
    }

    /// <summary>
    ///     Locks still in force: the newest event for the channel is a lock. Every later event closes the lock it
    ///     follows, so these are the locks not yet closed.
    /// </summary>
    private static IQueryable<ChannelLocked> ActiveChannelLocks(SettingsDbContext dbContext)
        => dbContext.ChannelLocks.OfType<ChannelLocked>().Where(x => x.ClosedAt == null);

    /// <summary>The same as <see cref="ActiveChannelLocks" />, for threads.</summary>
    private static IQueryable<ThreadLocked> ActiveThreadLocks(SettingsDbContext dbContext)
        => dbContext.ThreadLocks.OfType<ThreadLocked>().Where(x => x.ClosedAt == null);
}
