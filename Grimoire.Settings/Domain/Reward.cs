// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Values;

namespace Grimoire.Settings.Domain;

public abstract record Reward(RoleId RoleId, GuildId GuildId, ModeratorId SetBy, DateTimeOffset SetAt);

public sealed record RewardAdded : Reward
{
    // Private so a reward can only be made through Create, which checks the level. EF binds to it by parameter name.
    private RewardAdded(
        RoleId roleId,
        GuildId guildId,
        ModeratorId setBy,
        DateTimeOffset setAt,
        int rewardLevel,
        RewardMessage? rewardMessage)
        : base(roleId, guildId, setBy, setAt)
    {
        RewardLevel = rewardLevel;
        RewardMessage = rewardMessage;
    }

    public int RewardLevel { get; }
    public RewardMessage? RewardMessage { get; }

    public static Validation<RewardAdded> Create(
        RoleId roleId,
        GuildId guildId,
        int rewardLevel,
        RewardMessage? rewardMessage,
        ModeratorId setBy,
        DateTimeOffset setAt)
    {
        if (rewardLevel < 0)
            return Validation<RewardAdded>.Fail(
                new Error("reward.reward-level.invalid", "Reward Level must be greater than or equal to zero."));
        return Validation<RewardAdded>.Succeed(
            new RewardAdded(roleId, guildId, setBy, setAt, rewardLevel, rewardMessage));
    }
}

public sealed record RewardRemoved : Reward
{
    private RewardRemoved(
        RoleId roleId,
        GuildId guildId,
        ModeratorId setBy,
        DateTimeOffset setAt)
        : base(roleId, guildId, setBy, setAt)
    {
    }

    public static RewardRemoved Create(
        RoleId roleId,
        GuildId guildId,
        ModeratorId setBy,
        DateTimeOffset setAt)
        => new RewardRemoved(roleId, guildId, setBy, setAt);
}
