// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    /// <param name="parentChannelId">The channel a thread is in, so ignoring a channel also covers its threads.</param>
    public Task<Fin<bool>> IsMessageIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        ChannelId channelId,
        ChannelId? parentChannelId = null,
        CancellationToken cancellationToken = default)
        => GetAllIgnoredItems(guildId, cancellationToken)
            .MapFin(ignoredItems =>
                ignoredItems.Any(ignoredItem =>
                    ignoredItem switch
                    {
                        IgnoredChannel channel => channel.ChannelId == channelId
                                                  || channel.ChannelId == parentChannelId,
                        IgnoredMember member => member.UserId == userId,
                        IgnoredRole role => userRoleIds.Contains(role.RoleId),
                        _ => throw new InvariantViolationException()
                    }));

    public Task<Fin<bool>> IsMemberIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        CancellationToken cancellationToken = default)
        => GetAllIgnoredItems(guildId, cancellationToken)
            .MapFin(ignoredItems =>
                ignoredItems.Any(ignoredItem =>
                    ignoredItem switch
                    {
                        IgnoredChannel => false,
                        IgnoredMember member => member.UserId == userId,
                        IgnoredRole role => userRoleIds.Contains(role.RoleId),
                        _ => throw new InvariantViolationException()
                    }));

    /// <remarks>A failure is returned, not an empty set, so a caller can't mistake an outage for "nothing ignored".</remarks>
    public Task<Fin<FrozenSet<XpIgnoredItem>>> GetAllIgnoredItems(
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct => Fin.Succ<FrozenSet<XpIgnoredItem>>(await this._cache.GetOrCreateAsync(
            CacheKey.XpIgnoredItems(guildId),
            guildId,
            async (guildIdState, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                return (await dbContext
                        .XpTrackedItems
                        .AsNoTracking()
                        .Where(ignoredItem => ignoredItem.GuildId == guildIdState)
                        .GroupBy(ignoredItem => EF.Property<ulong>(ignoredItem, "Id"))
                        .Select(ignoredGroup
                            => ignoredGroup.OrderByDescending(item => item.SetAt)
                                .First())
                        .ToListAsync(ct))
                    .OfType<XpIgnoredItem>()
                    .ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: ct)), "Could not load the ignored items.", cancellationToken);


    /// <summary>
    ///     Saves the items that change something, and returns them: an ignore for something not ignored, or a watch for
    ///     something that is. Unchanged when none do, so nothing is saved and nothing is reported as changed.
    /// </summary>
    public async Task<Fin<Write<IReadOnlySet<XpTrackedItem>>>> AppendIgnoredItemsEvent(
        GuildId guildId,
        IReadOnlySet<XpTrackedItem> itemsToIgnore,
        CancellationToken cancellationToken = default)
    {
        if (itemsToIgnore.Count == 0)
            return new Write<IReadOnlySet<XpTrackedItem>>.Unchanged("No items were provided to ignore.");
        if (itemsToIgnore.Any(item => item.GuildId != guildId))
            return new Invalid("The guild id provided in the ignored items does not match the expected guild id.");

        return await this.GetAllIgnoredItems(guildId, cancellationToken)
            .BindAsync(ignored => OnlyChanges(itemsToIgnore, ignored) is { Count: > 0 } changes
                ? SaveIgnoredItems(guildId, changes, cancellationToken)
                : Task.FromResult<Fin<Write<IReadOnlySet<XpTrackedItem>>>>(
                    new Write<IReadOnlySet<XpTrackedItem>>.Unchanged(
                        "Nothing changed: every item was already in that state.")));
    }

    private Task<Fin<Write<IReadOnlySet<XpTrackedItem>>>> SaveIgnoredItems(
        GuildId guildId,
        IReadOnlySet<XpTrackedItem> changes,
        CancellationToken cancellationToken)
        => ExecuteSafelyAsync<Write<IReadOnlySet<XpTrackedItem>>>(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            dbContext.XpTrackedItems.AddRange(changes);
            await dbContext.SaveChangesAsync(ct);
            await this._cache.RemoveAsync(CacheKey.XpIgnoredItems(guildId), ct);
            return new Write<IReadOnlySet<XpTrackedItem>>.Saved(changes);
        }, "Could not save the ignored items.", cancellationToken);

    private static IReadOnlySet<XpTrackedItem> OnlyChanges(
        IReadOnlySet<XpTrackedItem> items,
        FrozenSet<XpIgnoredItem> ignored)
    {
        var ignoredIds = ignored.Select(item => item.TargetId).ToHashSet();
        return items
            .Where(item => item is XpIgnoredItem != ignoredIds.Contains(item.TargetId))
            .ToHashSet();
    }
}
