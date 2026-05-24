// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Values;

namespace Grimoire.Settings.Domain;

public abstract record Reward(RoleId RoleId, GuildId GuildId, ModeratorId SetBy, DateTimeOffset SetAt);

public sealed record RewardAdded(
    RoleId RoleId,
    GuildId GuildId,
    ModeratorId SetBy,
    DateTimeOffset SetAt,
    int RewardLevel,
    RewardMessage? RewardMessage)
    : Reward(RoleId, GuildId, SetBy, SetAt)
{
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
        if (roleId.Value == 0)
            return Validation<RewardAdded>.Fail(
                new Error("reward.role-id.invalid", "RoleId must be specified."));
        if (setBy.Value == 0)
            return Validation<RewardAdded>.Fail(
                new Error("reward.moderator-id.invalid", "ModeratorId must be specified."));
        if (guildId.Value == 0)
            return Validation<RewardAdded>.Fail(
                new Error("reward.guild-id.invalid", "GuildId must be specified."));
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

    public static Validation<RewardRemoved> Create(
        RoleId roleId,
        GuildId guildId,
        ModeratorId setBy,
        DateTimeOffset setAt)
    {
        if (roleId.Value == 0)
            return Validation<RewardRemoved>.Fail(
                new Error("reward-removed.role-id.invalid", "RoleId must be specified."));
        if (setBy.Value == 0)
            return Validation<RewardRemoved>.Fail(
                new Error("reward-removed.set-by.invalid", "ModeratorId must be specified."));
        if (guildId.Value == 0)
            return Validation<RewardRemoved>.Fail(
                new Error("reward-removed.guild-id.invalid", "GuildId must be specified."));
        return Validation<RewardRemoved>.Succeed(
            new RewardRemoved(roleId, guildId, setBy, setAt));
    }
}
