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
    public Task<Result<bool>> IsMessageIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        ChannelId channelId,
        ChannelId? parentChannelId = null,
        CancellationToken cancellationToken = default)
        => GetAllIgnoredItems(guildId, cancellationToken)
            .Map(ignoredItems =>
                ignoredItems.Any(ignoredItem =>
                    ignoredItem switch
                    {
                        IgnoredChannel channel => channel.ChannelId == channelId
                                                  || channel.ChannelId == parentChannelId,
                        IgnoredMember member => member.UserId == userId,
                        IgnoredRole role => userRoleIds.Contains(role.RoleId),
                        _ => throw new InvariantViolationException()
                    }));

    public Task<Result<bool>> IsMemberIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        CancellationToken cancellationToken = default)
        => GetAllIgnoredItems(guildId, cancellationToken)
            .Map(ignoredItems =>
                ignoredItems.Any(ignoredItem =>
                    ignoredItem switch
                    {
                        IgnoredChannel => false,
                        IgnoredMember member => member.UserId == userId,
                        IgnoredRole role => userRoleIds.Contains(role.RoleId),
                        _ => throw new InvariantViolationException()
                    }));

    /// <remarks>A failure is returned, not an empty set, so a caller can't mistake an outage for "nothing ignored".</remarks>
    public Task<Result<FrozenSet<XpIgnoredItem>>> GetAllIgnoredItems(
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct => Result<FrozenSet<XpIgnoredItem>>.Ok(await this._cache.GetOrCreateAsync(
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
            cancellationToken: ct)),
            new Error("xp-ignored-items.lookup-failed", "Could not load the ignored items."), cancellationToken);


    /// <summary>
    ///     Saves the items that change something, and returns them: an ignore for something not ignored, or a watch for
    ///     something that is. NotModified when none do, so nothing is saved and nothing is reported as changed.
    /// </summary>
    public Task<Result<IReadOnlySet<XpTrackedItem>>> AppendIgnoredItemsEvent(
        GuildId guildId,
        IReadOnlySet<XpTrackedItem> itemsToIgnore,
        CancellationToken cancellationToken = default)
        => ValidateNotEmpty(itemsToIgnore)
            .Bind(items => ValidateAllSameGuild(guildId, items))
            .BindAsync(async validatedItems => (await this.GetAllIgnoredItems(guildId, cancellationToken))
                .Bind(ignored => OnlyChanges(validatedItems, ignored)))
            .BindAsync(validatedItems => ExecuteSafelyAsync(async ct =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                dbContext.XpTrackedItems.AddRange(validatedItems);
                await dbContext.SaveChangesAsync(ct);
                await this._cache.RemoveAsync(CacheKey.XpIgnoredItems(guildId), ct);
                return Result<IReadOnlySet<XpTrackedItem>>.Ok(validatedItems);
            }, new Error("xp-ignored-items.save-failed", "Could not save the ignored items."), cancellationToken));

    private static Result<IReadOnlySet<XpTrackedItem>> OnlyChanges(
        IReadOnlySet<XpTrackedItem> items,
        FrozenSet<XpIgnoredItem> ignored)
    {
        var ignoredIds = ignored.Select(item => item.TargetId).ToHashSet();
        IReadOnlySet<XpTrackedItem> changes = items
            .Where(item => item is XpIgnoredItem != ignoredIds.Contains(item.TargetId))
            .ToHashSet();
        return changes.Count > 0
            ? Result<IReadOnlySet<XpTrackedItem>>.Ok(changes)
            : new Result<IReadOnlySet<XpTrackedItem>>.NotModified(
                new Error("xp-ignored-items.unchanged", "Nothing changed: every item was already in that state."));
    }

    private static Result<IReadOnlySet<XpTrackedItem>> ValidateNotEmpty(
        IReadOnlySet<XpTrackedItem> items) =>
        items.Count > 0
            ? Result<IReadOnlySet<XpTrackedItem>>.Ok(items)
            : new Result<IReadOnlySet<XpTrackedItem>>.NotModified(
                new Error("xp-ignored-items.empty", "No items were provided to ignore."));

    private static Result<IReadOnlySet<XpTrackedItem>> ValidateAllSameGuild(
        GuildId guildId,
        IReadOnlySet<XpTrackedItem> items) =>
        items.All(item => item.GuildId == guildId)
            ? Result<IReadOnlySet<XpTrackedItem>>.Ok(items)
            : Result<IReadOnlySet<XpTrackedItem>>.Fail(
                new Error("xp-ignored-items.guild-id.mismatch",
                    "The guild id provided in the ignored items does not match the expected guild id."));
}
