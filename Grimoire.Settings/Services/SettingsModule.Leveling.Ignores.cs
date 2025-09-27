// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using Grimoire.Settings.Domain.Shared;
using Grimoire.Settings.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{

    const string IgnoredChannelsCacheKeyPrefix = "IgnoredChannels_{0}";
    const string IgnoredMembersCacheKeyPrefix = "IgnoredMembers_{0}";
    const string IgnoredRolesCacheKeyPrefix = "IgnoredRoles_{0}";

    public async Task<bool> IsMessageIgnored(
        ulong guildId,
        ulong userId,
        IReadOnlyList<ulong> userRoleIds,
        ulong channelId,
        CancellationToken cancellationToken = default)
    {
        if (await this.IsMemberIgnored(guildId, userId, cancellationToken))
            return true;
        if (await this.IsChannelIgnored(guildId, channelId, cancellationToken))
            return true;
        return await this.AreRolesIgnored(guildId, userRoleIds, cancellationToken);
    }

    public async Task<bool> IsMemberIgnored(
        ulong guildId,
        ulong userId,
        IReadOnlyList<ulong> userRoleIds,
        CancellationToken cancellationToken = default)
    {
        if (await this.IsMemberIgnored(guildId, userId, cancellationToken))
            return true;
        return await this.AreRolesIgnored(guildId, userRoleIds, cancellationToken);
    }

    private async Task<bool> IsMemberIgnored(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        if (!await this.IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return true;

        var ignoredMembers = await this.GetAllIgnoredMembers(guildId, cancellationToken);

        return ignoredMembers.Contains(userId);
    }

    private async Task<IReadOnlySet<ulong>> GetAllIgnoredMembers(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await this.IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return FrozenSet<ulong>.Empty;
        var cacheKey = string.Format(IgnoredMembersCacheKeyPrefix, guildId);

        var ignoredMembers = await this._memoryCache.GetOrCreateAsync(cacheKey, async _ =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var results = await dbContext
                .IgnoredMembers
                .AsNoTracking()
                .Where(reward => reward.GuildId == guildId)
                .Select(reward => reward.Id)
                .ToHashSetAsync(cancellationToken);
            return results.ToFrozenSet();
        }, this._cacheEntryOptions) ?? [];

        return ignoredMembers;
    }

    private async Task<bool> IsChannelIgnored(
        ulong guildId,
        ulong channelId,
        CancellationToken cancellationToken = default)
    {
        if (!await this.IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return true;

        var ignoredChannels = await this.GetAllIgnoredChannels(guildId, cancellationToken);

        return ignoredChannels.Contains(channelId);
    }

    private async Task<IReadOnlySet<ulong>> GetAllIgnoredChannels(ulong guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await this.IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return FrozenSet<ulong>.Empty;
        var cacheKey = string.Format(IgnoredChannelsCacheKeyPrefix, guildId);
        return await this._memoryCache.GetOrCreateAsync(cacheKey, async _ =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var results = await dbContext
                .IgnoredChannels
                .AsNoTracking()
                .Where(reward => reward.GuildId == guildId)
                .Select(reward => reward.Id)
                .ToHashSetAsync(cancellationToken);
            return results.ToFrozenSet();
        }, this._cacheEntryOptions) ?? [];
    }

    private async Task<bool> AreRolesIgnored(
        ulong guildId,
        IReadOnlyList<ulong> roleIds,
        CancellationToken cancellationToken = default)
    {
        if (!await this.IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return true;

        var ignoredRoles = await this.GetAllIgnoredRoles(guildId, cancellationToken);

        return ignoredRoles.Overlaps(roleIds);
    }

    private async Task<IReadOnlySet<ulong>> GetAllIgnoredRoles(ulong guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await this.IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return FrozenSet<ulong>.Empty;
        var cacheKey = string.Format(IgnoredRolesCacheKeyPrefix, guildId);
        return await this._memoryCache.GetOrCreateAsync(cacheKey, async _ =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var results = await dbContext
                .IgnoredRoles
                .AsNoTracking()
                .Where(reward => reward.GuildId == guildId)
                .Select(reward => reward.Id)
                .ToHashSetAsync(cancellationToken);
            return results.ToFrozenSet();
        }, this._cacheEntryOptions) ?? [];
    }

    public async Task AddIgnoredItems(
        ulong guildId,
        IReadOnlyList<ulong> ignoredMemberIds,
        IReadOnlyList<ulong> ignoredChannelIds,
        IReadOnlyList<ulong> ignoredRoleIds,
        CancellationToken cancellationToken = default)
    {
        if (ignoredMemberIds.Count == 0 && ignoredChannelIds.Count == 0 && ignoredRoleIds.Count == 0)
            return;
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (ignoredMemberIds.Count > 0)
        {
            var currentIgnoredMembers = await dbContext
                .IgnoredMembers
                .Where(x => x.GuildId == guildId)
                .ToListAsync(cancellationToken);

            var newIgnoredMembers = AddIgnoredItems(currentIgnoredMembers, ignoredMemberIds, guildId);
            await dbContext.IgnoredMembers.AddRangeAsync(newIgnoredMembers, cancellationToken);

            await dbContext.IgnoredMembers.AddRangeAsync(newIgnoredMembers, cancellationToken);
            this._memoryCache.Remove(string.Format(IgnoredMembersCacheKeyPrefix, guildId));
        }

        if (ignoredChannelIds.Count > 0)
        {
            var currentIgnoredChannels = await dbContext
                .IgnoredChannels
                .Where(x => x.GuildId == guildId)
                .ToListAsync(cancellationToken);

            var newIgnoredChannels = AddIgnoredItems(currentIgnoredChannels, ignoredChannelIds, guildId);
            await dbContext.IgnoredChannels.AddRangeAsync(newIgnoredChannels, cancellationToken);
            this._memoryCache.Remove(string.Format(IgnoredChannelsCacheKeyPrefix, guildId));
        }
        if (ignoredRoleIds.Count > 0)
        {
            var currentIgnoredRoles = await dbContext
                .IgnoredRoles
                .Where(x => x.GuildId == guildId)
                .ToListAsync(cancellationToken);

            var newIgnoredRoles = AddIgnoredItems(currentIgnoredRoles, ignoredRoleIds, guildId);
            await dbContext.IgnoredRoles.AddRangeAsync(newIgnoredRoles, cancellationToken);
            this._memoryCache.Remove(string.Format(IgnoredRolesCacheKeyPrefix, guildId));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }


    private static T[] AddIgnoredItems<T>(IList<T> currentIgnoredItems, IReadOnlyList<ulong> ignoredIds,
        ulong guildId) where T : IIgnored, new()
    {
        if (ignoredIds.Count > 0)
            return [];
        var existingIgnoredMemberIds = currentIgnoredItems
            .Select(x => x.Id)
            .ToHashSet();

        var newUsersToIgnore = ignoredIds
            .Where(x => !existingIgnoredMemberIds.Contains(x))
            .Select(x => new T { Id = x, GuildId = guildId })
            .ToArray();

        foreach (var newIgnoredMember in newUsersToIgnore)
            currentIgnoredItems.Add(newIgnoredMember);
        return newUsersToIgnore;
    }

    public async Task RemoveIgnoredItems(
        ulong guildId,
        IReadOnlyList<ulong> ignoredMemberIds,
        IReadOnlyList<ulong> ignoredChannelIds,
        IReadOnlyList<ulong> ignoredRoleIds,
        CancellationToken cancellationToken = default)
    {
        if (ignoredMemberIds.Count == 0 && ignoredChannelIds.Count == 0 && ignoredRoleIds.Count == 0)
            return;
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (ignoredMemberIds.Count > 0)
        {
            var currentIgnoredMembers = await dbContext
                .IgnoredMembers
                .Where(x => x.GuildId == guildId)
                .ToListAsync(cancellationToken);

            var newIgnoredMembers = RemoveIgnoredItems(currentIgnoredMembers, ignoredMemberIds);
            dbContext.IgnoredMembers.RemoveRange(newIgnoredMembers);
            this._memoryCache.Remove(string.Format(IgnoredMembersCacheKeyPrefix, guildId));
        }

        if (ignoredChannelIds.Count > 0)
        {
            var currentIgnoredChannels = await dbContext
                .IgnoredChannels
                .Where(x => x.GuildId == guildId)
                .ToListAsync(cancellationToken);

            var newIgnoredChannels = RemoveIgnoredItems(currentIgnoredChannels, ignoredChannelIds);
            dbContext.IgnoredChannels.RemoveRange(newIgnoredChannels);
            this._memoryCache.Remove(string.Format(IgnoredChannelsCacheKeyPrefix, guildId));
        }
        if (ignoredRoleIds.Count > 0)
        {
            var currentIgnoredRoles = await dbContext
                .IgnoredRoles
                .Where(x => x.GuildId == guildId)
                .ToListAsync(cancellationToken);

            var newIgnoredRoles = RemoveIgnoredItems(currentIgnoredRoles, ignoredRoleIds);
            dbContext.IgnoredRoles.RemoveRange(newIgnoredRoles);
            this._memoryCache.Remove(string.Format(IgnoredRolesCacheKeyPrefix, guildId));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static T[] RemoveIgnoredItems<T>(ICollection<T> currentIgnoredItems, IReadOnlyList<ulong> ignoredIds) where T : IIgnored, new()
    {
        if (ignoredIds.Count == 0)
            return [];
        var itemsRequestedToRemoveIgnore = ignoredIds
            .ToHashSet();

        var itemsToNoLongerIgnore = currentIgnoredItems
            .Where(x => itemsRequestedToRemoveIgnore.Contains(x.Id))
            .ToArray();

        foreach (var itemToNoLongerIgnore in itemsToNoLongerIgnore)
            currentIgnoredItems.Remove(itemToNoLongerIgnore);
        return itemsToNoLongerIgnore;
    }

    public async Task<AllIgnoredItems> GetAllIgnoredItems(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        var ignoredMembersTask = this.GetAllIgnoredMembers(guildId, cancellationToken);

        var ignoredChannelsTask = this.GetAllIgnoredChannels(guildId, cancellationToken);

        var ignoredRolesTask = this.GetAllIgnoredRoles(guildId, cancellationToken);

        await Task.WhenAll(ignoredMembersTask, ignoredChannelsTask, ignoredRolesTask);

        return new AllIgnoredItems(
            guildId,
            await ignoredMembersTask,
            await ignoredChannelsTask,
            await ignoredRolesTask);
    }

    public record AllIgnoredItems(ulong GuildId, IReadOnlySet<ulong> IgnoredMemberIds, IReadOnlySet<ulong> IgnoredChannelIds, IReadOnlySet<ulong> IgnoredRoleIds);
}
