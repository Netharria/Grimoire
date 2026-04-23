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
}

public sealed record ChannelUnlocked(
    ModeratorId ModeratorId,
    ChannelId ChannelId,
    GuildId GuildId,
    DateTimeOffset SetAt,
    ModerationReason? Reason = null)
    : ChannelLock(ModeratorId, ChannelId, GuildId, SetAt, Reason);

public readonly record struct PreviouslyAllowedPermissions(long Permissions);

public readonly record struct PreviouslyDeniedPermissions(long Permissions);
