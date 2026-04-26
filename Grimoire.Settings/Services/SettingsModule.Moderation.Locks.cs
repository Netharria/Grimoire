// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Diagnostics;
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
                        .ToHashSetAsync(innerCt);
                    return results.ToFrozenSet();
                }, this._cacheEntryOptions, cancellationToken: ct);
            return Result<bool>.Ok(locks.Contains(channelId));
        }, new Error("channel-lock.lookup-failed", "Failed to retrieve channel lock state."), cancellationToken);

    public Task<Result<ChannelLocked>> ApplyChannelLockAction(
        ChannelLock channelLock,
        CancellationToken cancellationToken = default)
        => channelLock switch
        {
            ChannelLocked v => AddChannelLock(v, cancellationToken),
            ChannelUnlocked v => RemoveChannelLock(v, cancellationToken),
            _ => throw new UnreachableException()
        };

    private Task<Result<ChannelLocked>> AddChannelLock(
        ChannelLocked channelLock,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            var existingPermissions = await dbContext.ChannelLocks
                .AsNoTracking()
                .OfType<ChannelLocked>()
                .Where(x => x.ChannelId == channelLock.ChannelId && x.GuildId == channelLock.GuildId)
                // ReSharper disable once AccessToDisposedClosure
                .Where(x => !dbContext.ChannelLocks
                    .Any(y => y.ChannelId == x.ChannelId &&
                              y.GuildId == x.GuildId &&
                              y.SetAt > x.SetAt))
                .Select(x => new { x.PreviouslyAllowed, x.PreviouslyDenied })
                .FirstOrDefaultAsync(ct);

            var finalAllowed = existingPermissions?.PreviouslyAllowed ?? channelLock.PreviouslyAllowed;
            var finalDenied = existingPermissions?.PreviouslyDenied ?? channelLock.PreviouslyDenied;

            return await ChannelLocked.Create(channelLock, finalAllowed, finalDenied)
                .ToResult()
                .BindAsync(async validLock =>
                {
                    dbContext.ChannelLocks.Add(validLock);
                    await dbContext.SaveChangesAsync(ct);
                    await this._cache.RemoveAsync(CacheKey.ChannelLocks(validLock.GuildId), ct);
                    return Result<ChannelLocked>.Ok(validLock);
                });
        }, new Error("channel-lock.save-failed", "Could not save the channel lock."), cancellationToken);

    private Task<Result<ChannelLocked>> RemoveChannelLock(
        ChannelUnlocked lockAction,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            var existingLock = await dbContext.ChannelLocks
                .OfType<ChannelLocked>()
                .Where(x => x.ChannelId == lockAction.ChannelId && x.GuildId == lockAction.GuildId)
                // ReSharper disable once AccessToDisposedClosure
                .Where(x => !dbContext.ChannelLocks
                    .Any(y => y.ChannelId == x.ChannelId &&
                              y.GuildId == x.GuildId &&
                              y.SetAt > x.SetAt))
                .FirstOrDefaultAsync(ct);
            if (existingLock is null)
                return new Result<ChannelLocked>.NotFound(
                    new Error("channel-lock.not-found", "The channel is not currently locked."));

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
            expired = await dbContext.ChannelLocks
                .AsNoTracking()
                .OfType<ChannelLocked>()
                .Where(x => x.EndTime <= DateTimeOffset.UtcNow)
                // ReSharper disable once AccessToDisposedClosure
                .Where(x => !dbContext.ChannelLocks
                    .Any(y => y.ChannelId == x.ChannelId &&
                              y.GuildId == x.GuildId &&
                              y.SetAt > x.SetAt))
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
                        .ToHashSetAsync(innerCt);
                    return results.ToFrozenSet();
                }, this._cacheEntryOptions, cancellationToken: ct);
            return Result<bool>.Ok(locks.Contains(channelId));
        }, new Error("thread-lock.lookup-failed", "Failed to retrieve thread lock state."), cancellationToken);

    public Task<Result<ThreadLocked>> ApplyThreadLockAction(
        ThreadLock lockAction,
        CancellationToken cancellationToken = default)
        => lockAction switch
        {
            ThreadLocked v => AddThreadLock(v, cancellationToken),
            ThreadUnlocked v => RemoveThreadLock(v, cancellationToken),
            _ => throw new UnreachableException()
        };

    private Task<Result<ThreadLocked>> AddThreadLock(
        ThreadLocked threadLock,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            dbContext.ThreadLocks.Add(threadLock);
            await dbContext.SaveChangesAsync(ct);
            await this._cache.RemoveAsync(CacheKey.ThreadLocks(threadLock.GuildId), ct);
            return Result<ThreadLocked>.Ok(threadLock);
        }, new Error("thread-lock.save-failed", "Could not save the thread lock."), cancellationToken);

    private Task<Result<ThreadLocked>> RemoveThreadLock(
        ThreadUnlocked lockAction,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            var existingLock = await dbContext.ThreadLocks
                .OfType<ThreadLocked>()
                .Where(x => x.ChannelId == lockAction.ChannelId && x.GuildId == lockAction.GuildId)
                // ReSharper disable once AccessToDisposedClosure
                .Where(x => !dbContext.ThreadLocks
                    .Any(y => y.ChannelId == x.ChannelId &&
                              y.GuildId == x.GuildId &&
                              y.SetAt > x.SetAt))
                .FirstOrDefaultAsync(ct);
            if (existingLock is null)
                return new Result<ThreadLocked>.NotFound(
                    new Error("thread-lock.not-found", "The thread is not currently locked."));
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
            expired = await dbContext.ThreadLocks
                .AsNoTracking()
                .OfType<ThreadLocked>()
                .Where(x => x.EndTime <= DateTimeOffset.UtcNow)
                // ReSharper disable once AccessToDisposedClosure
                .Where(x => !dbContext.ThreadLocks
                    .Any(y => y.ChannelId == x.ChannelId &&
                              y.GuildId == x.GuildId &&
                              y.SetAt > x.SetAt))
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
}
