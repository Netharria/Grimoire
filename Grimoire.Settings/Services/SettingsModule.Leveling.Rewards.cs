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
    public async Task<Result<IReadOnlySet<RewardEntry>>> GetLevelingRewardsAsync(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        if (!(await IsModuleEnabled(Module.Leveling, guildId, cancellationToken)).OrElse(false))
            return Result<IReadOnlySet<RewardEntry>>.Ok(FrozenSet<RewardEntry>.Empty);
        var cacheKey = CacheKey.LevelingRewards(guildId);
        return Result<IReadOnlySet<RewardEntry>>.Ok(
            await this._cache.GetOrCreateAsync(cacheKey,
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
                        if (reward is RewardAdded added)
                            entries.Add(new RewardEntry(added.RoleId, added.RewardLevel, added.RewardMessage?.Value));
                    return entries.ToFrozenSet();
                }, this._cacheEntryOptions,
                cancellationToken: cancellationToken));
    }

    public async Task<Result<T>> SetRewardAsync<T>(
        T reward,
        CancellationToken cancellationToken = default) where T : Reward
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.Rewards.Add(reward);
        await dbContext.SaveChangesAsync(cancellationToken);

        var cacheKey = CacheKey.LevelingRewards(reward.GuildId);
        await this._cache.RemoveAsync(cacheKey, cancellationToken);
        return Result<T>.Ok(reward);
    }

    public sealed record RewardEntry(RoleId RoleId, int RewardLevel, string? RewardMessage);
}
