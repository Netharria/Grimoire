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
    public Task<Result<IReadOnlySet<RewardEntry>>> GetLevelingRewardsAsync(
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
                Result<IReadOnlySet<RewardEntry>>.Ok(
                    await this._cache.GetOrCreateAsync(CacheKey.LevelingRewards(guildId),
                        guildId,
                        async (guildIdState, innerCt) =>
                        {
                            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(innerCt);
                            return (await dbContext.Rewards
                                    .AsNoTracking()
                                    .Where(reward => reward.GuildId == guildIdState)
                                    .GroupBy(reward => reward.RoleId)
                                    .Select(group => group.OrderByDescending(reward => reward.SetAt).First())
                                    .ToListAsync(innerCt))
                                .OfType<RewardAdded>()
                                .Select(added =>
                                    new RewardEntry(added.RoleId, added.RewardLevel, added.RewardMessage))
                                .ToFrozenSet();
                        }, this._cacheEntryOptions,
                        cancellationToken: ct)),
            new Error("reward.lookup-failed", "Could not retrieve rewards."), cancellationToken);

    public async Task<Result<T>> SetRewardAsync<T>(
        T reward,
        CancellationToken cancellationToken = default) where T : Reward
    {
        try
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

            dbContext.Rewards.Add(reward);
            await dbContext.SaveChangesAsync(cancellationToken);

            var cacheKey = CacheKey.LevelingRewards(reward.GuildId);
            await this._cache.RemoveAsync(cacheKey, cancellationToken);
            return Result<T>.Ok(reward);
        }
        catch (Exception ex)
        {
            LogOperationFailure(this._logger, ex.Message, ex);
            return Result<T>.Fail(new Error("set-reward.failed", "Could not set reward"));
        }
    }
}
