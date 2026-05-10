// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public abstract record ThreadLock(
    ModeratorId ModeratorId,
    ChannelId ChannelId,
    GuildId GuildId,
    DateTimeOffset SetAt,
    ModerationReason? Reason);

public sealed record ThreadLocked : ThreadLock
{
    private ThreadLocked(
        ModeratorId moderatorId,
        ChannelId channelId,
        GuildId guildId,
        DateTimeOffset setAt,
        ModerationReason? reason,
        DateTimeOffset endTime)
        : base(moderatorId, channelId, guildId, setAt, reason)
    {
        EndTime = endTime;
    }

    public DateTimeOffset EndTime { get; }

    public static Validation<ThreadLocked> Create(
        ModeratorId moderatorId,
        ModerationReason? reason,
        ChannelId channelId,
        GuildId guildId,
        DateTimeOffset setAt,
        DateTimeOffset endTime)
    {
        if (endTime <= setAt)
            return Validation<ThreadLocked>.Fail(
                new Error("thread-lock.end-time.invalid", "End time must be after the lock's set time."));
        return Validation<ThreadLocked>.Succeed(
            new ThreadLocked(moderatorId, channelId, guildId, setAt, reason, endTime));
    }
}

public sealed record ThreadUnlocked : ThreadLock
{
    private ThreadUnlocked(
        ModeratorId moderatorId,
        ChannelId channelId,
        GuildId guildId,
        DateTimeOffset setAt,
        ModerationReason? reason)
        : base(moderatorId, channelId, guildId, setAt, reason)
    {
    }

    public static Validation<ThreadUnlocked> Create(
        ModeratorId moderatorId,
        ChannelId channelId,
        GuildId guildId,
        DateTimeOffset setAt,
        ModerationReason? reason = null)
    {
        if (moderatorId.Value == 0)
            return Validation<ThreadUnlocked>.Fail(
                new Error("thread-unlock.moderator-id.invalid", "ModeratorId must be specified."));
        if (channelId.Value == 0)
            return Validation<ThreadUnlocked>.Fail(
                new Error("thread-unlock.channel-id.invalid", "ChannelId must be specified."));
        if (guildId.Value == 0)
            return Validation<ThreadUnlocked>.Fail(
                new Error("thread-unlock.guild-id.invalid", "GuildId must be specified."));
        return Validation<ThreadUnlocked>.Succeed(
            new ThreadUnlocked(moderatorId, channelId, guildId, setAt, reason));
    }
}
