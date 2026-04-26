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

    public Task<Result<ChannelLocked>> ApplyChannelLockAction(
        ChannelLock channelLock,
        CancellationToken cancellationToken = default)
        => channelLock switch
        {
            ChannelLocked v => AddChannelLock(v, cancellationToken),
            ChannelUnlocked v => RemoveChannelLock(v, cancellationToken),
            _ => throw new UnreachableException()
        };

    private async Task<Result<ChannelLocked>> AddChannelLock(
        ChannelLocked channelLock,
        CancellationToken cancellationToken = default)
    {

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
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
            .FirstOrDefaultAsync(cancellationToken);

        var finalAllowed = existingPermissions?.PreviouslyAllowed ?? channelLock.PreviouslyAllowed;
        var finalDenied = existingPermissions?.PreviouslyDenied ?? channelLock.PreviouslyDenied;

      return await ChannelLocked.Create(channelLock, finalAllowed, finalDenied)
          .ToResult()
          .TapAsync(async validLock =>
          {
              dbContext.ChannelLocks.Add(validLock);
              await dbContext.SaveChangesAsync(cancellationToken);
              await this._cache.RemoveAsync(CacheKey.ChannelLocks(validLock.GuildId), cancellationToken);
          });
    }

    private async Task<Result<ChannelLocked>> RemoveChannelLock(
        ChannelUnlocked lockAction,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingLock = await dbContext.ChannelLocks
            .OfType<ChannelLocked>()
            .Where(x => x.ChannelId == lockAction.ChannelId && x.GuildId == lockAction.GuildId)
            // ReSharper disable once AccessToDisposedClosure
            .Where(x => !dbContext.ChannelLocks
                .Any(y => y.ChannelId == x.ChannelId &&
                          y.GuildId == x.GuildId &&
                          y.SetAt > x.SetAt))
            .FirstOrDefaultAsync(cancellationToken);
        if (existingLock is null)
            return new Result<ChannelLocked>.NotFound(
                new Error("channel-lock.not-found", "The channel is not currently locked."));

        dbContext.ChannelLocks.Add(lockAction);
        await dbContext.SaveChangesAsync(cancellationToken);
        await this._cache.RemoveAsync(CacheKey.ChannelLocks(lockAction.GuildId), cancellationToken);
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

    public Task<Result<ThreadLocked>> ApplyThreadLockAction(
        ThreadLock lockAction,
        CancellationToken cancellationToken = default)
        => lockAction switch
        {
            ThreadLocked v => AddThreadLock(v, cancellationToken),
            ThreadUnlocked v => RemoveThreadLock(v, cancellationToken),
            _ => throw new UnreachableException()
        };

    private async Task<Result<ThreadLocked>> AddThreadLock(
        ThreadLocked threadLock,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.ThreadLocks.Add(threadLock);
        await dbContext.SaveChangesAsync(cancellationToken);
        await this._cache.RemoveAsync(CacheKey.ThreadLocks(threadLock.GuildId), cancellationToken);
        return Result<ThreadLocked>.Ok(threadLock);
    }

    private async Task<Result<ThreadLocked>> RemoveThreadLock(
        ThreadUnlocked lockAction,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingLock = await dbContext.ThreadLocks
            .OfType<ThreadLocked>()
            .Where(x => x.ChannelId == lockAction.ChannelId && x.GuildId == lockAction.GuildId)
            // ReSharper disable once AccessToDisposedClosure
            .Where(x => !dbContext.ThreadLocks
                .Any(y => y.ChannelId == x.ChannelId &&
                          y.GuildId == x.GuildId &&
                          y.SetAt > x.SetAt))
            .FirstOrDefaultAsync(cancellationToken);
        if (existingLock is null)
            return new Result<ThreadLocked>.NotFound(
                new Error("thread-lock.not-found", "The thread is not currently locked."));
        dbContext.ThreadLocks.Add(lockAction);
        await dbContext.SaveChangesAsync(cancellationToken);
        await this._cache.RemoveAsync(CacheKey.ThreadLocks(lockAction.GuildId), cancellationToken);
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
