// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Diagnostics;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public Task<Result<bool>> IsMessageIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        ChannelId channelId,
        CancellationToken cancellationToken = default)
        => GetAllIgnoredItems(guildId, cancellationToken)
            .OrElse(() => Result<FrozenSet<XpIgnoredItem>>.Ok(FrozenSet<XpIgnoredItem>.Empty))
            .Map(ignoredItems =>
                ignoredItems.Any(ignoredItem =>
                    ignoredItem switch
                    {
                        IgnoredChannel channel => channel.ChannelId == channelId,
                        IgnoredMember member => member.UserId == userId,
                        IgnoredRole role => userRoleIds.Contains(role.RoleId),
                        _ => throw new UnreachableException()
                    }));

    public Task<Result<bool>> IsMemberIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        CancellationToken cancellationToken = default)
        => GetAllIgnoredItems(guildId, cancellationToken)
            .OrElse(() => Result<FrozenSet<XpIgnoredItem>>.Ok(FrozenSet<XpIgnoredItem>.Empty))
            .Map(ignoredItems =>
                ignoredItems.Any(ignoredItem =>
                    ignoredItem switch
                    {
                        IgnoredChannel => false,
                        IgnoredMember member => member.UserId == userId,
                        IgnoredRole role => userRoleIds.Contains(role.RoleId),
                        _ => throw new UnreachableException()
                    }));

    public async Task<Result<FrozenSet<XpIgnoredItem>>> GetAllIgnoredItems(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var items = await this._cache.GetOrCreateAsync(
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
                    .ToListAsync(cancellationToken: ct))
                    .OfType<XpIgnoredItem>()
                    .ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);
        return Result<FrozenSet<XpIgnoredItem>>.Ok(items);
    }


    public Task<Result<IReadOnlySet<XpTrackedItem>>> AppendIgnoredItemsEvent(
        GuildId guildId,
        IReadOnlySet<XpTrackedItem> itemsToIgnore,
        CancellationToken cancellationToken = default)
        => ValidateNotEmpty(itemsToIgnore)
            .Bind(items => ValidateAllSameGuild(guildId, items))
            .BindAsync(async validatedItems =>
            {
                await using var dbContext =
                    await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
                dbContext.XpTrackedItems.AddRange(validatedItems);
                await dbContext.SaveChangesAsync(cancellationToken);
                await this._cache.RemoveAsync(CacheKey.XpIgnoredItems(guildId), cancellationToken);
                return Result<IReadOnlySet<XpTrackedItem>>.Ok(validatedItems);
            });

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
