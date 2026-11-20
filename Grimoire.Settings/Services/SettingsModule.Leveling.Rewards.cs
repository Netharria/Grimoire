// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Documents;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public Task<Fin<IReadOnlySet<RewardEntry>>> GetLevelingRewardsAsync(
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => ReadDocument<LevelRewards, IReadOnlySet<RewardEntry>>(guildId,
            rewards => rewards.Entries,
            cancellationToken);

    /// <summary>
    ///     Adds the reward, or replaces the role's reward, and says which (spec 2.5, UX change 1). Unchanged when the
    ///     role already has exactly this reward.
    /// </summary>
    public Task<Fin<Write<RewardChange>>> SetRewardAsync(
        GuildId guildId,
        ModeratorId setBy,
        RewardEntry reward,
        CancellationToken cancellationToken = default)
        => UpdateDocument<LevelRewards, RewardChange>(guildId, setBy,
            rewards => rewards.Set(reward),
            "That reward is already set.", cancellationToken,
            "Could not set reward");

    /// <summary>Removes the rewards of these roles. Saved holds the roles that had one; Unchanged when none did.</summary>
    public Task<Fin<Write<Set<RoleId>>>> RemoveRewardsAsync(
        GuildId guildId,
        ModeratorId setBy,
        IEnumerable<RoleId> roles,
        CancellationToken cancellationToken = default)
        => UpdateDocument<LevelRewards, Set<RoleId>>(guildId, setBy,
            rewards => rewards.Remove(roles),
            "None of those roles are rewards.", cancellationToken,
            "Could not set reward");
}
