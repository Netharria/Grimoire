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
    public async Task<IReadOnlySet<RewardEntry>> GetLevelingRewardsAsync(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.Leveling, guildId, cancellationToken))
            return FrozenSet<RewardEntry>.Empty;
        var cacheKey = CacheKey.LevelingRewards(guildId);
        return await this._cache.GetOrCreateAsync(cacheKey,
            guildId,
            async (guildIdState, ct) =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var results = await dbContext.Rewards
                    .AsNoTracking()
                    .Where(reward => reward.GuildId == guildIdState)
                    .GroupBy(reward => reward.RoleId)
                    .Select(group => group.OrderByDescending(reward => reward.SetAt).First())
                    .Where(reward => reward.Enabled)
                    .Select(reward => new RewardEntry
                    {
                        RoleId = reward.RoleId,
                        RewardLevel = reward.RewardLevel,
                        RewardMessage = reward.RewardMessage
                    })
                    .ToHashSetAsync(ct);
                return results.ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);
    }

    public async Task<SettingsResult> SetRewardAsync(
        RoleId roleId,
        GuildId guildId,
        ModeratorId moderatorId,
        int level,
        string? rewardMessage,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var reward = new Reward
        {
            RoleId = roleId,
            GuildId = guildId,
            SetBy = moderatorId,
            SetAt = DateTimeOffset.UtcNow,
            RewardLevel = level,
            RewardMessage = rewardMessage,
            Enabled = enabled
        };

        dbContext.Rewards.Add(reward);
        await dbContext.SaveChangesAsync(cancellationToken);

        var cacheKey = CacheKey.LevelingRewards(reward.GuildId);
        await this._cache.RemoveAsync(cacheKey, cancellationToken);
        return SettingsResult.Written();
    }

    public sealed record RewardEntry
    {
        public RoleId RoleId { get; init; }
        public int RewardLevel { get; init; }
        public string? RewardMessage { get; init; }
    }
}
