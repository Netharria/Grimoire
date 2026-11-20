// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Text.Json.Serialization;
using Grimoire.Settings.Domain.Values;

namespace Grimoire.Settings.Domain.Documents;

/// <summary>A guild's level rewards, at most one per role (C14, spec 2.5). History is the document's versions.</summary>
public sealed record LevelRewards : ISettingsDocument<LevelRewards>
{
    public Map<RoleId, Reward> Roles { get; init; }

    public static SettingsDocument Kind => SettingsDocument.Rewards;
    public static LevelRewards Empty { get; } = new();

    [JsonIgnore]
    public IReadOnlySet<RewardEntry> Entries
        => this.Roles.Pairs.Select(pair => new RewardEntry(pair.Key, pair.Value.Level, pair.Value.Message)).ToFrozenSet();

    /// <summary>Adds the reward, or replaces the role's reward, and says which it was.</summary>
    public (LevelRewards Document, RewardChange Change) Set(RewardEntry entry)
    {
        var reward = new Reward(entry.RewardLevel, entry.RewardMessage);
        var change = this.Roles.Find(entry.RoleId).Match<RewardChange>(
            None: () => new RewardChange.Added(),
            Some: current => current == reward ? new RewardChange.Unchanged()
                : current.Level != reward.Level ? new RewardChange.Moved(current.Level)
                : new RewardChange.MessageChanged());
        return (this with { Roles = this.Roles.AddOrUpdate(entry.RoleId, reward) }, change);
    }

    /// <summary>Removes the rewards of these roles. Returns only the roles that had one.</summary>
    public (LevelRewards Document, Set<RoleId> Removed) Remove(IEnumerable<RoleId> roles)
    {
        var removed = toSet(roles.Where(this.Roles.ContainsKey));
        return (this with { Roles = this.Roles.RemoveRange(removed) }, removed);
    }
}

/// <summary>A role's reward: the level it's given at, and the message sent with it.</summary>
public sealed record Reward(int Level, RewardMessage? Message = null);

/// <summary>
///     What <see cref="LevelRewards.Set" /> did, decided against the version it replaced, so a reply can't be wrong
///     after a race (spec 2.5, UX change 1).
/// </summary>
public abstract record RewardChange
{
    private RewardChange() { }

    public sealed record Added : RewardChange;

    /// <summary>The level changed. The message may have changed too.</summary>
    public sealed record Moved(int FromLevel) : RewardChange;

    /// <summary>The same level, a different message.</summary>
    public sealed record MessageChanged : RewardChange;

    /// <summary>Exactly the reward already set: nothing is stored.</summary>
    public sealed record Unchanged : RewardChange;
}
