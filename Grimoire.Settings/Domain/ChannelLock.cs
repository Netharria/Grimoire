// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public abstract record ChannelLock(
    ModeratorId ModeratorId,
    ChannelId ChannelId,
    GuildId GuildId,
    DateTimeOffset SetAt,
    ModerationReason? Reason);

public sealed record ChannelLocked : ChannelLock
{
    private ChannelLocked(
        ModeratorId moderatorId,
        ChannelId channelId,
        GuildId guildId,
        DateTimeOffset setAt,
        ModerationReason? reason,
        PreviouslyAllowedPermissions previouslyAllowed,
        PreviouslyDeniedPermissions previouslyDenied,
        DateTimeOffset endTime)
        : base(moderatorId, channelId, guildId, setAt, reason)
    {
        PreviouslyAllowed = previouslyAllowed;
        PreviouslyDenied = previouslyDenied;
        EndTime = endTime;
    }

    public PreviouslyAllowedPermissions PreviouslyAllowed { get; }
    public PreviouslyDeniedPermissions PreviouslyDenied { get; }
    public DateTimeOffset EndTime { get; }

    public static Validation<ChannelLocked> Create(
        ModeratorId moderatorId,
        ModerationReason? reason,
        ChannelId channelId,
        GuildId guildId,
        DateTimeOffset setAt,
        PreviouslyAllowedPermissions previouslyAllowed,
        PreviouslyDeniedPermissions previouslyDenied,
        DateTimeOffset endTime)
    {
        if (endTime <= setAt)
            return Validation<ChannelLocked>.Fail(
                new Error("channel-lock.end-time.invalid", "End time must be after the lock's set time."));
        return Validation<ChannelLocked>.Succeed(
            new ChannelLocked(moderatorId, channelId, guildId, setAt, reason, previouslyAllowed, previouslyDenied,
                endTime));
    }

    public static Validation<ChannelLocked> Create(
        ChannelLocked lockAction,
        PreviouslyAllowedPermissions previouslyAllowed,
        PreviouslyDeniedPermissions previouslyDenied)
    {
        return Validation<ChannelLocked>.Succeed(new ChannelLocked(
            lockAction.ModeratorId,
            lockAction.ChannelId,
            lockAction.GuildId,
            lockAction.SetAt,
            lockAction.Reason,
            previouslyAllowed,
            previouslyDenied,
            lockAction.EndTime));
    }
}

public sealed record ChannelUnlocked : ChannelLock
{
    private ChannelUnlocked(
        ModeratorId moderatorId,
        ChannelId channelId,
        GuildId guildId,
        DateTimeOffset setAt,
        ModerationReason? reason)
        : base(moderatorId, channelId, guildId, setAt, reason) { }

    public static Validation<ChannelUnlocked> Create(
        ModeratorId moderatorId,
        ChannelId channelId,
        GuildId guildId,
        DateTimeOffset setAt,
        ModerationReason? reason = null)
    {
        if (moderatorId.Value == 0)
            return Validation<ChannelUnlocked>.Fail(
                new Error("channel-unlock.moderator-id.invalid", "ModeratorId must be specified."));
        if (channelId.Value == 0)
            return Validation<ChannelUnlocked>.Fail(
                new Error("channel-unlock.channel-id.invalid", "ChannelId must be specified."));
        if (guildId.Value == 0)
            return Validation<ChannelUnlocked>.Fail(
                new Error("channel-unlock.guild-id.invalid", "GuildId must be specified."));
        return Validation<ChannelUnlocked>.Succeed(
            new ChannelUnlocked(moderatorId, channelId, guildId, setAt, reason));
    }
}

public readonly record struct PreviouslyAllowedPermissions(long Permissions);

public readonly record struct PreviouslyDeniedPermissions(long Permissions);
