// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    private static string GetXpIgnoredItemsCacheKey(GuildId guildId) =>
        $"XpIgnoredItems_{guildId.Value}";

    public async Task<bool> IsMessageIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        ChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        if(!await IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return true;
        var allIgnoredItems = await GetAllIgnoredItems(guildId, cancellationToken);
        var rawRoleIds = userRoleIds.Select(roleId => roleId.Value).ToHashSet();
        return allIgnoredItems
            .Any(ignoredItem => ignoredItem.Id == userId.Value
                                || ignoredItem.Id == channelId.Value
                || rawRoleIds.Contains(ignoredItem.Id));
    }

    public async Task<bool> IsMemberIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        CancellationToken cancellationToken = default)
    {
        if(!await IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return true;
        var allIgnoredItems = await GetAllIgnoredItems(guildId, cancellationToken);
        var rawRoleIds = userRoleIds.Select(roleId => roleId.Value).ToHashSet();
        return allIgnoredItems
            .Any(ignoredItem => ignoredItem.Id == userId.Value
                                || rawRoleIds.Contains(ignoredItem.Id));
    }

    private async Task<IReadOnlySet<XpIgnoredItem>> GetAllIgnoredItems(
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => await this._cache.GetOrCreateAsync(
            GetXpIgnoredItemsCacheKey(guildId),
            guildId,
            async (guildIdState, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var results = await dbContext
                    .XpIgnoredItems
                    .AsNoTracking()
                    .Where(ignoredItem => ignoredItem.GuildId == guildIdState)
                    .GroupBy(ignoredItem => new { ignoredItem.Id, ignoredItem.GuildId })
                    .Select(ignoredGroup
                        => ignoredGroup.OrderByDescending(item => item.SetAt)
                            .ThenBy(item => item.SetBy)
                            .First())
                    .Where(ignoredItem => ignoredItem.Enabled)
                    .ToListAsync(ct);
                return results.ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);


    public async Task AppendIgnoredItemsEvent(
        GuildId guildId,
        IReadOnlySet<XpIgnoredItem> itemsToIgnore,
        CancellationToken cancellationToken = default)
    {
        if (itemsToIgnore.Count == 0)
            return;
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.XpIgnoredItems.AddRangeAsync(itemsToIgnore, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await this._cache.RemoveAsync(GetXpIgnoredItemsCacheKey(guildId), cancellationToken);
    }
}
