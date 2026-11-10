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

    /// <summary>
    ///     When this lock stopped being in force: the time of the next event for the same thread, an end or a newer
    ///     lock. Null while it is in force. Only the settings module sets it, in the same save as that event, so the
    ///     expiry polls can read open rows without searching the whole history (review S-13).
    /// </summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    internal void Close(DateTimeOffset closedAt) => this.ClosedAt = closedAt;

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

    public static ThreadUnlocked Create(
        ModeratorId moderatorId,
        ChannelId channelId,
        GuildId guildId,
        DateTimeOffset setAt,
        ModerationReason? reason = null)
        => new ThreadUnlocked(moderatorId, channelId, guildId, setAt, reason);
}
