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

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    private static string GetIgnoredMembersCacheKey(GuildId guildId) =>
        $"IgnoredMembers_{guildId}";

    private static string GetIgnoredChannelsCacheKey(GuildId guildId) =>
        $"IgnoredChannels_{guildId}";

    private static string GetIgnoredRolesCacheKey(GuildId guildId) =>
        $"IgnoredRoles_{guildId}";

    public async Task<bool> IsMessageIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        ChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        if (await IsMemberIgnored(guildId, userId, cancellationToken))
            return true;
        if (await IsChannelIgnored(guildId, channelId, cancellationToken))
            return true;
        return await AreRolesIgnored(guildId, userRoleIds, cancellationToken);
    }

    public async Task<bool> IsMemberIgnored(
        GuildId guildId,
        UserId userId,
        IReadOnlySet<RoleId> userRoleIds,
        CancellationToken cancellationToken = default)
    {
        if (await IsMemberIgnored(guildId, userId, cancellationToken))
            return true;
        return await AreRolesIgnored(guildId, userRoleIds, cancellationToken);
    }

    private async Task<bool> IsMemberIgnored(
        GuildId guildId,
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return true;

        var ignoredMembers = await GetAllIgnoredMembers(guildId, cancellationToken);

        return ignoredMembers.Contains(userId);
    }

    private async Task<IReadOnlySet<UserId>> GetAllIgnoredMembers(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return FrozenSet<UserId>.Empty;

        var ignoredMembers = await this._cache.GetOrCreateAsync(
            GetIgnoredMembersCacheKey(guildId),
            guildId,
            async (guildIdState, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var results = await dbContext
                    .IgnoredMembers
                    .AsNoTracking()
                    .Where(reward => reward.GuildId == guildIdState)
                    .Select(reward => reward.UserId)
                    .ToHashSetAsync(ct);
                return results.ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);

        return ignoredMembers;
    }

    private async Task<bool> IsChannelIgnored(
        GuildId guildId,
        ChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return true;

        var ignoredChannels = await GetAllIgnoredChannels(guildId, cancellationToken);

        return ignoredChannels.Contains(channelId);
    }

    private async Task<IReadOnlySet<ChannelId>> GetAllIgnoredChannels(GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return FrozenSet<ChannelId>.Empty;
        return await this._cache.GetOrCreateAsync(
            GetIgnoredChannelsCacheKey(guildId),
            guildId,
            async (guildIdState, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var results = await dbContext
                    .IgnoredChannels
                    .AsNoTracking()
                    .Where(reward => reward.GuildId == guildIdState)
                    .Select(reward => reward.ChannelId)
                    .ToHashSetAsync(ct);
                return results.ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> AreRolesIgnored(
        GuildId guildId,
        IReadOnlySet<RoleId> roleIds,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return true;

        var ignoredRoles = await GetAllIgnoredRoles(guildId, cancellationToken);

        return ignoredRoles.Overlaps(roleIds);
    }

    private async Task<IReadOnlySet<RoleId>> GetAllIgnoredRoles(GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return FrozenSet<RoleId>.Empty;
        return await this._cache.GetOrCreateAsync(
            GetIgnoredRolesCacheKey(guildId),
            guildId,
            async (guildIdState, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var results = await dbContext
                    .IgnoredRoles
                    .AsNoTracking()
                    .Where(reward => reward.GuildId == guildIdState)
                    .Select(reward => reward.RoleId)
                    .ToHashSetAsync(ct);
                return results.ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);
    }

    public async Task AddIgnoredItems(
        GuildId guildId,
        IReadOnlySet<UserId> ignoredMemberIds,
        IReadOnlySet<ChannelId> ignoredChannelIds,
        IReadOnlySet<RoleId> ignoredRoleIds,
        CancellationToken cancellationToken = default)
    {
        if (ignoredMemberIds.Count == 0 && ignoredChannelIds.Count == 0 && ignoredRoleIds.Count == 0)
            return;
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        FrozenSet<UserId>? ignoredMembers = null;
        FrozenSet<ChannelId>? ignoredChannels = null;
        FrozenSet<RoleId>? ignoredRoles = null;

        if (ignoredMemberIds.Count > 0)
        {
            var currentIgnoredMembers = await dbContext
                .IgnoredMembers
                .Where(x => x.GuildId == guildId)
                .ToListAsync(cancellationToken);

            var newIgnoredMembers = AddIgnoredItems(currentIgnoredMembers,
                ignoredMemberIds.Select(x => x.Value).ToHashSet(), guildId);
            await dbContext.IgnoredMembers.AddRangeAsync(newIgnoredMembers, cancellationToken);
            ignoredMembers = currentIgnoredMembers
                .Select(x => x.UserId)
                .ToFrozenSet();
        }

        if (ignoredChannelIds.Count > 0)
        {
            var currentIgnoredChannels = await dbContext
                .IgnoredChannels
                .Where(x => x.GuildId == guildId)
                .ToListAsync(cancellationToken);

            var newIgnoredChannels = AddIgnoredItems(currentIgnoredChannels,
                ignoredChannelIds.Select(x => x.Value).ToHashSet(), guildId);
            await dbContext.IgnoredChannels.AddRangeAsync(newIgnoredChannels, cancellationToken);
            ignoredChannels = currentIgnoredChannels
                .Select(x => x.ChannelId)
                .ToFrozenSet();
        }

        if (ignoredRoleIds.Count > 0)
        {
            var currentIgnoredRoles = await dbContext
                .IgnoredRoles
                .Where(x => x.GuildId == guildId)
                .ToListAsync(cancellationToken);

            var newIgnoredRoles = AddIgnoredItems(currentIgnoredRoles, ignoredRoleIds.Select(x => x.Value).ToHashSet(),
                guildId);
            await dbContext.IgnoredRoles.AddRangeAsync(newIgnoredRoles, cancellationToken);
            ignoredRoles = currentIgnoredRoles
                .Select(x => x.RoleId)
                .ToFrozenSet();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (ignoredMembers is not null)
            await this._cache.SetAsync(
                GetIgnoredMembersCacheKey(guildId),
                ignoredMembers,
                this._cacheEntryOptions,
                cancellationToken: cancellationToken);
        if (ignoredChannels is not null)
            await this._cache.SetAsync(
                GetIgnoredChannelsCacheKey(guildId),
                ignoredChannels,
                this._cacheEntryOptions,
                cancellationToken: cancellationToken);
        if (ignoredRoles is not null)
            await this._cache.SetAsync(
                GetIgnoredRolesCacheKey(guildId),
                ignoredRoles,
                this._cacheEntryOptions,
                cancellationToken: cancellationToken);
    }


    private static T[] AddIgnoredItems<T>(
        IList<T> currentIgnoredItems,
        IReadOnlySet<ulong> ignoredIds,
        GuildId guildId) where T : IIgnored, new()
    {
        if (ignoredIds.Count == 0)
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
        GuildId guildId,
        IReadOnlySet<UserId> ignoredMemberIds,
        IReadOnlySet<ChannelId> ignoredChannelIds,
        IReadOnlySet<RoleId> ignoredRoleIds,
        CancellationToken cancellationToken = default)
    {
        if (ignoredMemberIds.Count == 0 && ignoredChannelIds.Count == 0 && ignoredRoleIds.Count == 0)
            return;
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        FrozenSet<UserId>? ignoredMembers = null;
        FrozenSet<ChannelId>? ignoredChannels = null;
        FrozenSet<RoleId>? ignoredRoles = null;

        if (ignoredMemberIds.Count > 0)
        {
            var currentIgnoredMembers = await dbContext
                .IgnoredMembers
                .Where(x => x.GuildId == guildId)
                .ToListAsync(cancellationToken);

            var newIgnoredMembers =
                RemoveIgnoredItems(currentIgnoredMembers, ignoredMemberIds.Select(x => x.Value).ToHashSet());
            dbContext.IgnoredMembers.RemoveRange(newIgnoredMembers);
            ignoredMembers = currentIgnoredMembers
                .Select(x => x.UserId)
                .ToFrozenSet();
        }

        if (ignoredChannelIds.Count > 0)
        {
            var currentIgnoredChannels = await dbContext
                .IgnoredChannels
                .Where(x => x.GuildId == guildId)
                .ToListAsync(cancellationToken);

            var newIgnoredChannels =
                RemoveIgnoredItems(currentIgnoredChannels, ignoredChannelIds.Select(x => x.Value).ToHashSet());
            dbContext.IgnoredChannels.RemoveRange(newIgnoredChannels);
            ignoredChannels = currentIgnoredChannels
                .Select(x => x.ChannelId)
                .ToFrozenSet();
        }

        if (ignoredRoleIds.Count > 0)
        {
            var currentIgnoredRoles = await dbContext
                .IgnoredRoles
                .Where(x => x.GuildId == guildId)
                .ToListAsync(cancellationToken);

            var newIgnoredRoles =
                RemoveIgnoredItems(currentIgnoredRoles, ignoredRoleIds.Select(x => x.Value).ToHashSet());
            dbContext.IgnoredRoles.RemoveRange(newIgnoredRoles);
            ignoredRoles = currentIgnoredRoles
                .Select(x => x.RoleId)
                .ToFrozenSet();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (ignoredMembers is not null)
            await this._cache.SetAsync(
                GetIgnoredMembersCacheKey(guildId),
                ignoredMembers,
                this._cacheEntryOptions,
                cancellationToken: cancellationToken);
        if (ignoredChannels is not null)
            await this._cache.SetAsync(
                GetIgnoredChannelsCacheKey(guildId),
                ignoredChannels,
                this._cacheEntryOptions,
                cancellationToken: cancellationToken);
        if (ignoredRoles is not null)
            await this._cache.SetAsync(
                GetIgnoredRolesCacheKey(guildId),
                ignoredRoles,
                this._cacheEntryOptions,
                cancellationToken: cancellationToken);
    }

    private static T[] RemoveIgnoredItems<T>(ICollection<T> currentIgnoredItems, IReadOnlySet<ulong> ignoredIds)
        where T : IIgnored, new()
    {
        if (ignoredIds.Count == 0)
            return [];

        var itemsToNoLongerIgnore = currentIgnoredItems
            .Where(x => ignoredIds.Contains(x.Id))
            .ToArray();

        foreach (var itemToNoLongerIgnore in itemsToNoLongerIgnore)
            currentIgnoredItems.Remove(itemToNoLongerIgnore);
        return itemsToNoLongerIgnore;
    }

    public async Task<AllIgnoredItems> GetAllIgnoredItems(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var ignoredMembersTask = GetAllIgnoredMembers(guildId, cancellationToken);

        var ignoredChannelsTask = GetAllIgnoredChannels(guildId, cancellationToken);

        var ignoredRolesTask = GetAllIgnoredRoles(guildId, cancellationToken);

        await Task.WhenAll(ignoredMembersTask, ignoredChannelsTask, ignoredRolesTask);

        return new AllIgnoredItems(
            guildId,
            await ignoredMembersTask,
            await ignoredChannelsTask,
            await ignoredRolesTask);
    }

    public record AllIgnoredItems(
        GuildId GuildId,
        IReadOnlySet<UserId> IgnoredMemberIds,
        IReadOnlySet<ChannelId> IgnoredChannelIds,
        IReadOnlySet<RoleId> IgnoredRoleIds);
}
