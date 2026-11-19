// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Helpers;
using Grimoire.Settings.Rows;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public Task<Fin<bool>> IsChannelLocked(ChannelId channelId, GuildId guildId,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            var locks = await this._cache.GetOrCreateAsync(CacheKey.ChannelLocks(guildId),
                (guildId, this._dbContextFactory),
                static async (state, innerCt) =>
                {
                    await using var dbContext = await state._dbContextFactory.CreateDbContextAsync(innerCt);
                    return (await dbContext.ChannelLocks
                            .AsNoTracking()
                            .Where(row => row.GuildId == state.guildId)
                            .Where(InForce<ChannelLockRow>())
                            .Select(row => row.ChannelId)
                            .ToListAsync(innerCt))
                        .ToFrozenSet();
                }, this._cacheEntryOptions, cancellationToken: ct);
            return Fin.Succ<bool>(locks.Contains(channelId));
        }, "Failed to retrieve channel lock state.", cancellationToken);

    /// <summary>The lock in force on the channel, without changing anything.</summary>
    public Task<Fin<ChannelLock>> GetActiveChannelLockAsync(
        ChannelId channelId,
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            return await FindInForceChannelLock(dbContext, channelId, guildId, ct);
        }, "Failed to retrieve channel lock state.", cancellationToken);

    /// <summary>
    ///     Records a lock that has been applied, as Active. A lock on a locked channel replaces the one in force, which
    ///     is Removed by this lock's moderator, and keeps its permissions from before the first lock.
    /// </summary>
    public Task<Fin<ChannelLock>> AddChannelLock(
        NewChannelLock channelLock,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            var added = await InTransaction(dbContext, async innerCt =>
            {
                var existing = await ChannelLocksOf(dbContext, channelLock.ChannelId, channelLock.GuildId)
                    .Where(InForce<ChannelLockRow>())
                    .AsNoTracking()
                    .FirstOrDefaultAsync(innerCt);
                await EndInForce(ChannelLocksOf(dbContext, channelLock.ChannelId, channelLock.GuildId),
                    new ItemState.Removed(channelLock.SetAt, channelLock.ModeratorId, null), innerCt);
                var row = new ChannelLockRow
                {
                    GuildId = channelLock.GuildId,
                    ChannelId = channelLock.ChannelId,
                    Reason = channelLock.Reason,
                    ModeratorId = channelLock.ModeratorId,
                    SetAt = channelLock.SetAt,
                    EndTime = channelLock.EndTime,
                    PreviouslyAllowed = existing?.PreviouslyAllowed ?? channelLock.PreviouslyAllowed,
                    PreviouslyDenied = existing?.PreviouslyDenied ?? channelLock.PreviouslyDenied,
                    State = nameof(ItemState.Active),
                    ActivatedAt = channelLock.SetAt
                };
                dbContext.ChannelLocks.Add(row);
                await dbContext.SaveChangesAsync(innerCt);
                return row;
            }, ct);
            await this._cache.RemoveAsync(CacheKey.ChannelLocks(channelLock.GuildId), ct);
            return Fin.Succ<ChannelLock>(added.ToLock());
        }, "Could not save the channel lock.", cancellationToken);

    /// <summary>A moderator unlocks the channel: its lock becomes Removed. NotFound when it isn't locked.</summary>
    public Task<Fin<ChannelLock>> RemoveChannelLock(
        ChannelId channelId,
        GuildId guildId,
        ModeratorId moderatorId,
        ModerationReason? reason = null,
        CancellationToken cancellationToken = default)
        => EndChannelLock(channelId, guildId, new ItemState.Removed(DateTimeOffset.UtcNow, moderatorId, reason),
            "Could not unlock the channel.", cancellationToken);

    /// <summary>The lock ran out: it becomes Expired. NotFound when it already ended.</summary>
    public Task<Fin<ChannelLock>> ExpireChannelLock(
        ChannelLock channelLock,
        CancellationToken cancellationToken = default)
        => EndChannelLock(channelLock.ChannelId, channelLock.GuildId, new ItemState.Expired(DateTimeOffset.UtcNow),
            "Could not record the expired channel lock.", cancellationToken);

    private Task<Fin<ChannelLock>> EndChannelLock(ChannelId channelId, GuildId guildId, ItemState ended,
        string failure, CancellationToken cancellationToken)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            var result = await FindInForceChannelLock(dbContext, channelId, guildId, ct)
                .BindAsync(async channelLock =>
                    await EndInForce(ChannelLocksOf(dbContext, channelLock.ChannelId, channelLock.GuildId).Where(row => row.SetAt == channelLock.SetAt), ended, ct) == 1
                        ? Fin.Succ<ChannelLock>(channelLock with { State = ended })
                        : new NotFound("The channel is not currently locked."));
            await this._cache.RemoveAsync(CacheKey.ChannelLocks(guildId), ct);
            return result;
        }, failure, cancellationToken);

    private static async Task<Fin<ChannelLock>> FindInForceChannelLock(SettingsDbContext dbContext,
        ChannelId channelId, GuildId guildId, CancellationToken cancellationToken)
        => await ChannelLocksOf(dbContext, channelId, guildId)
                .Where(InForce<ChannelLockRow>())
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken) switch
            {
                { } row => Fin.Succ<ChannelLock>(row.ToLock()),
                null => new NotFound("The channel is not currently locked.")
            };

    private static IQueryable<ChannelLockRow> ChannelLocksOf(SettingsDbContext dbContext, ChannelId channelId,
        GuildId guildId)
        => dbContext.ChannelLocks.Where(row => row.ChannelId == channelId && row.GuildId == guildId);

    /// <summary>Applied channel locks past their end, for the expiry poll. A failed read yields nothing this tick.</summary>
    public async IAsyncEnumerable<ChannelLock> GetAllExpiredChannelLocks(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<ChannelLockRow> expired;
        try
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            expired = await dbContext.ChannelLocks
                .AsNoTracking()
                .Where(row => row.State == nameof(ItemState.Active) && row.EndTime <= now)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogOperationFailure(this._logger, ex.Message, ex);
            yield break;
        }

        foreach (var row in expired)
            yield return row.ToLock();
    }

    public Task<Fin<bool>> IsThreadLocked(ChannelId channelId, GuildId guildId,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            var locks = await this._cache.GetOrCreateAsync(CacheKey.ThreadLocks(guildId),
                (guildId, this._dbContextFactory),
                static async (state, innerCt) =>
                {
                    await using var dbContext = await state._dbContextFactory.CreateDbContextAsync(innerCt);
                    return (await dbContext.ThreadLocks
                            .AsNoTracking()
                            .Where(row => row.GuildId == state.guildId)
                            .Where(InForce<ThreadLockRow>())
                            .Select(row => row.ChannelId)
                            .ToListAsync(innerCt))
                        .ToFrozenSet();
                }, this._cacheEntryOptions, cancellationToken: ct);
            return Fin.Succ<bool>(locks.Contains(channelId));
        }, "Failed to retrieve thread lock state.", cancellationToken);

    /// <summary>Records a thread lock, as Active. A lock on a locked thread replaces the one in force.</summary>
    public Task<Fin<ThreadLock>> AddThreadLock(
        NewThreadLock threadLock,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            var added = await InTransaction(dbContext, async innerCt =>
            {
                await EndInForce(ThreadLocksOf(dbContext, threadLock.ChannelId, threadLock.GuildId),
                    new ItemState.Removed(threadLock.SetAt, threadLock.ModeratorId, null), innerCt);
                var row = new ThreadLockRow
                {
                    GuildId = threadLock.GuildId,
                    ChannelId = threadLock.ChannelId,
                    Reason = threadLock.Reason,
                    ModeratorId = threadLock.ModeratorId,
                    SetAt = threadLock.SetAt,
                    EndTime = threadLock.EndTime,
                    State = nameof(ItemState.Active),
                    ActivatedAt = threadLock.SetAt
                };
                dbContext.ThreadLocks.Add(row);
                await dbContext.SaveChangesAsync(innerCt);
                return row;
            }, ct);
            await this._cache.RemoveAsync(CacheKey.ThreadLocks(threadLock.GuildId), ct);
            return Fin.Succ<ThreadLock>(added.ToLock());
        }, "Could not save the thread lock.", cancellationToken);

    /// <summary>A moderator unlocks the thread: its lock becomes Removed. NotFound when it isn't locked.</summary>
    public Task<Fin<ThreadLock>> RemoveThreadLock(
        ChannelId channelId,
        GuildId guildId,
        ModeratorId moderatorId,
        ModerationReason? reason = null,
        CancellationToken cancellationToken = default)
        => EndThreadLock(channelId, guildId, new ItemState.Removed(DateTimeOffset.UtcNow, moderatorId, reason),
            "Could not unlock the thread.", cancellationToken);

    /// <summary>The lock ran out: it becomes Expired. NotFound when it already ended.</summary>
    public Task<Fin<ThreadLock>> ExpireThreadLock(
        ThreadLock threadLock,
        CancellationToken cancellationToken = default)
        => EndThreadLock(threadLock.ChannelId, threadLock.GuildId, new ItemState.Expired(DateTimeOffset.UtcNow),
            "Could not record the expired thread lock.", cancellationToken);

    private Task<Fin<ThreadLock>> EndThreadLock(ChannelId channelId, GuildId guildId, ItemState ended,
        string failure, CancellationToken cancellationToken)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            var result = await FindInForceThreadLock(dbContext, channelId, guildId, ct)
                .BindAsync(async threadLock =>
                    await EndInForce(ThreadLocksOf(dbContext, threadLock.ChannelId, threadLock.GuildId).Where(row => row.SetAt == threadLock.SetAt), ended, ct) == 1
                        ? Fin.Succ<ThreadLock>(threadLock with { State = ended })
                        : new NotFound("The thread is not currently locked."));
            await this._cache.RemoveAsync(CacheKey.ThreadLocks(guildId), ct);
            return result;
        }, failure, cancellationToken);

    private static async Task<Fin<ThreadLock>> FindInForceThreadLock(SettingsDbContext dbContext,
        ChannelId channelId, GuildId guildId, CancellationToken cancellationToken)
        => await ThreadLocksOf(dbContext, channelId, guildId)
                .Where(InForce<ThreadLockRow>())
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken) switch
            {
                { } row => Fin.Succ<ThreadLock>(row.ToLock()),
                null => new NotFound("The thread is not currently locked.")
            };

    private static IQueryable<ThreadLockRow> ThreadLocksOf(SettingsDbContext dbContext, ChannelId channelId,
        GuildId guildId)
        => dbContext.ThreadLocks.Where(row => row.ChannelId == channelId && row.GuildId == guildId);

    /// <summary>Applied thread locks past their end, for the expiry poll. A failed read yields nothing this tick.</summary>
    public async IAsyncEnumerable<ThreadLock> GetAllExpiredThreadLocks(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<ThreadLockRow> expired;
        try
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            expired = await dbContext.ThreadLocks
                .AsNoTracking()
                .Where(row => row.State == nameof(ItemState.Active) && row.EndTime <= now)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogOperationFailure(this._logger, ex.Message, ex);
            yield break;
        }

        foreach (var row in expired)
            yield return row.ToLock();
    }
}
