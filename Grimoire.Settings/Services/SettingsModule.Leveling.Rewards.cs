// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Values;
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
                var entries = new HashSet<RewardEntry>();
                await foreach (var reward in dbContext.Rewards
                                   .AsNoTracking()
                                   .Where(reward => reward.GuildId == guildIdState)
                                   .GroupBy(reward => reward.RoleId)
                                   .Select(group => group.OrderByDescending(reward => reward.SetAt).First())
                                   .AsAsyncEnumerable()
                                   .WithCancellation(ct))
                    if (reward.Enabled)
                        entries.Add(new RewardEntry(
                            reward.RoleId,
                            reward.RewardLevel,
                            reward.RewardMessage?.Value));
                return entries.ToFrozenSet();
            }, this._cacheEntryOptions,
            cancellationToken: cancellationToken);
    }

    public Task<Result<Reward>> SetRewardAsync(
        RoleId roleId,
        GuildId guildId,
        ModeratorId moderatorId,
        int rewardLevel,
        string? rewardMessage,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var msgValidation = rewardMessage is null
            ? Validation<RewardMessage?>.Succeed(null)
            : RewardMessage.Create(rewardMessage).Map(m => (RewardMessage?)m);
        return msgValidation
            .Bind(msg => Reward.Create(roleId, guildId, rewardLevel, msg, moderatorId, DateTimeOffset.UtcNow, enabled))
            .MatchAsync(
                reward => SetRewardAsync(reward, cancellationToken),
                errors => Result<Reward>.Fail(errors));
    }

    public async Task<Result<Reward>> SetRewardAsync(
        Reward reward,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.Rewards.Add(reward);
        await dbContext.SaveChangesAsync(cancellationToken);

        var cacheKey = CacheKey.LevelingRewards(reward.GuildId);
        await this._cache.RemoveAsync(cacheKey, cancellationToken);
        return Result<Reward>.Ok(reward);
    }

    public sealed record RewardEntry(RoleId RoleId, int RewardLevel, string? RewardMessage);
}
