// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Diagnostics;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public async Task<Result<bool>> IsMessageIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        ChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        if (!(await IsModuleEnabled(Module.Leveling, guildId, cancellationToken)).OrElse(false))
            return Result<bool>.Ok(true);
        var allIgnoredItems = (await GetAllIgnoredItems(guildId, cancellationToken)).OrElse(FrozenSet<XpIgnoredItem>.Empty);
        return Result<bool>.Ok(allIgnoredItems
            .Any(ignoredItem =>
                ignoredItem switch
                {
                    IgnoredChannel channel => channel.ChannelId == channelId,
                    IgnoredMember member => member.UserId == userId && member.GuildId == guildId,
                    IgnoredRole role => userRoleIds.Contains(role.RoleId),
                    _ => throw new UnreachableException()
                }));
    }

    public async Task<Result<bool>> IsMemberIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        CancellationToken cancellationToken = default)
    {
        if (!(await IsModuleEnabled(Module.Leveling, guildId, cancellationToken)).OrElse(false))
            return Result<bool>.Ok(true);
        var allIgnoredItems = (await GetAllIgnoredItems(guildId, cancellationToken)).OrElse(FrozenSet<XpIgnoredItem>.Empty);
        return Result<bool>.Ok(allIgnoredItems
            .Any(ignoredItem =>
                ignoredItem switch
                {
                    IgnoredChannel => false,
                    IgnoredMember member => member.UserId == userId && member.GuildId == guildId,
                    IgnoredRole role => userRoleIds.Contains(role.RoleId),
                    _ => throw new UnreachableException()
                }));
    }

    public async Task<Result<IReadOnlySet<XpIgnoredItem>>> GetAllIgnoredItems(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var items = await this._cache.GetOrCreateAsync(
            CacheKey.XpIgnoredItems(guildId),
            guildId,
            async (guildIdState, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var enabledItems = new List<XpIgnoredItem>();
                await foreach (var item in dbContext
                                   .XpIgnoredItems
                                   .AsNoTracking()
                                   .Where(ignoredItem => ignoredItem.GuildId == guildIdState)
                                   .GroupBy(ignoredItem => EF.Property<ulong>(ignoredItem, "Id"))
                                   .Select(ignoredGroup
                                       => ignoredGroup.OrderByDescending(item => item.SetAt)
                                           .First())
                                   .AsAsyncEnumerable()
                                   .WithCancellation(ct))
                    if (item is IgnoredChannel or IgnoredMember or IgnoredRole)
                        enabledItems.Add(item);
                return enabledItems.ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);
        return Result<IReadOnlySet<XpIgnoredItem>>.Ok(items);
    }


    public async Task<Result<IReadOnlySet<XpIgnoredItem>>> AppendIgnoredItemsEvent(
        GuildId guildId,
        IReadOnlySet<XpIgnoredItem> itemsToIgnore,
        CancellationToken cancellationToken = default)
    {
        if (itemsToIgnore.Count == 0)
            return new Result<IReadOnlySet<XpIgnoredItem>>.NotModified(
                new Error("xp-ignored-items.empty", "No items were provided to ignore."));
        if (itemsToIgnore.Any(ignoredItem => ignoredItem.GuildId != guildId))
            return Result<IReadOnlySet<XpIgnoredItem>>.Fail(
                new Error("xp-ignored-items.guild-id.mismatch",
                    "The guild id provided in the ignored items does not match the expected guild id."));
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.XpIgnoredItems.AddRange(itemsToIgnore);

        await dbContext.SaveChangesAsync(cancellationToken);

        await this._cache.RemoveAsync(CacheKey.XpIgnoredItems(guildId), cancellationToken);
        return Result<IReadOnlySet<XpIgnoredItem>>.Ok(itemsToIgnore);
    }
}
