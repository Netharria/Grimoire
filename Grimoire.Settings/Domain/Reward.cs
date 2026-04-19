// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Values;

namespace Grimoire.Settings.Domain;

public sealed record Reward
{
    private Reward(
        RoleId roleId,
        GuildId guildId,
        int rewardLevel,
        RewardMessage? rewardMessage,
        ModeratorId setBy,
        DateTimeOffset setAt,
        bool enabled)
    {
        RoleId = roleId;
        GuildId = guildId;
        RewardLevel = rewardLevel;
        RewardMessage = rewardMessage;
        SetBy = setBy;
        SetAt = setAt;
        Enabled = enabled;
    }

    public RoleId RoleId { get; }
    public GuildId GuildId { get; }
    public int RewardLevel { get; }
    public RewardMessage? RewardMessage { get; }
    public ModeratorId SetBy { get; }
    public DateTimeOffset SetAt { get; }
    public bool Enabled { get; }

    public static Validation<Reward> Create(
        RoleId roleId,
        GuildId guildId,
        int rewardLevel,
        RewardMessage? rewardMessage,
        ModeratorId setBy,
        DateTimeOffset setAt,
        bool enabled
    )
    {
        if (rewardLevel < 0)
            return Validation<Reward>.Fail(
                new Error("reward.reward-level.invalid", "Reward Level must be greater than or equal to zero."));
        if (roleId.Value == 0)
            return Validation<Reward>.Fail(
                new Error("reward.role-id.invalid", "RoleId must be specified."));
        if (setBy.Value == 0)
            return Validation<Reward>.Fail(
                new Error("mute.moderator-id.invalid", "ModeratorId must be specified."));
        return Validation<Reward>.Succeed(
            new Reward(roleId, guildId, rewardLevel, rewardMessage, setBy, setAt, enabled));
    }
}
