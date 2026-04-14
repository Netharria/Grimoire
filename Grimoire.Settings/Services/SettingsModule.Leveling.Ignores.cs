// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public async Task<bool> IsMessageIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        ChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
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
        if (!await IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return true;
        var allIgnoredItems = await GetAllIgnoredItems(guildId, cancellationToken);
        var rawRoleIds = userRoleIds.Select(roleId => roleId.Value).ToHashSet();
        return allIgnoredItems
            .Any(ignoredItem => ignoredItem.Id == userId.Value
                                || rawRoleIds.Contains(ignoredItem.Id));
    }

    public async Task<IReadOnlySet<XpIgnoredItem>> GetAllIgnoredItems(
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => await this._cache.GetOrCreateAsync(
            CacheKey.XpIgnoredItems(guildId),
            guildId,
            async (guildIdState, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                // XpIgnoredItem uses TPH (discriminator column), which prevents EF Core from
                // translating a .Where() applied after GroupBy().Select(g => g.First()).
                // Stream rows as they arrive and filter by Enabled in memory.
                var enabledItems = new List<XpIgnoredItem>();
                await foreach (var item in dbContext
                                   .XpIgnoredItems
                                   .AsNoTracking()
                                   .Where(ignoredItem => ignoredItem.GuildId == guildIdState)
                                   .GroupBy(ignoredItem => ignoredItem.Id)
                                   .Select(ignoredGroup
                                       => ignoredGroup.OrderByDescending(item => item.SetAt)
                                           .First())
                                   .AsAsyncEnumerable()
                                   .WithCancellation(ct))
                    if (item.Enabled)
                        enabledItems.Add(item);
                return enabledItems.ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);


    public async Task<SettingsResult> AppendIgnoredItemsEvent(
        GuildId guildId,
        IReadOnlySet<XpIgnoredItem> itemsToIgnore,
        CancellationToken cancellationToken = default)
    {
        if (itemsToIgnore.Count == 0)
            return SettingsResult.Unchanged();
        if (itemsToIgnore.Any(ignoredItem => ignoredItem.GuildId != guildId))
            return SettingsResult.Invalid(
                "The guild id provided in the ignored items does not match the expected guild id.");
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.XpIgnoredItems.AddRange(itemsToIgnore);

        await dbContext.SaveChangesAsync(cancellationToken);

        await this._cache.RemoveAsync(CacheKey.XpIgnoredItems(guildId), cancellationToken);
        return SettingsResult.Written();
    }
}
