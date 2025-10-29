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
using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    private const string RewardsCacheKeyPrefix = "LevelingRewards_{0}";

    public async Task<IReadOnlySet<RewardEntry>> GetLevelingRewardsAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return FrozenSet<RewardEntry>.Empty;
        var cacheKey = string.Format(RewardsCacheKeyPrefix, guildId);
        return await this._memoryCache.GetOrCreateAsync(cacheKey, async _ =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var results = await dbContext.Rewards
                .AsNoTracking()
                .Where(reward => reward.GuildId == guildId)
                .Select(reward => new RewardEntry
                {
                    RoleId = reward.RoleId, RewardLevel = reward.RewardLevel, RewardMessage = reward.RewardMessage
                })
                .ToHashSetAsync(cancellationToken);
            return results.ToFrozenSet();
        }, this._cacheEntryOptions) ?? [];
    }

    public async Task AddOrUpdateRewardAsync(
        ulong roleId,
        ulong guildId,
        int level,
        string? rewardMessage,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var reward = await dbContext.Rewards
            .Where(reward => reward.RoleId == roleId && reward.GuildId == guildId)
            .FirstOrDefaultAsync(cancellationToken) ?? new Reward { RoleId = roleId, GuildId = guildId };

        reward.RewardLevel = level;
        reward.RewardMessage = rewardMessage;

        dbContext.Rewards.Add(reward);
        await dbContext.SaveChangesAsync(cancellationToken);

        var cacheKey = string.Format(RewardsCacheKeyPrefix, reward.GuildId);
        this._memoryCache.Remove(cacheKey);
    }

    public async Task RemoveRewardAsync(
        ulong roleId,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var reward = await dbContext.Rewards
            .FirstOrDefaultAsync(r => r.RoleId == roleId && r.GuildId == guildId, cancellationToken);

        if (reward is null)
            return;

        dbContext.Rewards.Remove(reward);
        await dbContext.SaveChangesAsync(cancellationToken);
        var cacheKey = string.Format(RewardsCacheKeyPrefix, guildId);
        this._memoryCache.Remove(cacheKey);
    }


    public sealed record RewardEntry
    {
        public ulong RoleId { get; init; }
        public int RewardLevel { get; init; }
        public string? RewardMessage { get; init; }
    }
}
