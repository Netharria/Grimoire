// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

/// <summary>
///     A stored thread lock: one row per lock, which moves through <see cref="ItemState" /> (TD-1, spec 2.2). Its key
///     is the thread and the time it was set.
/// </summary>
public sealed record ThreadLock(
    GuildId GuildId,
    ChannelId ChannelId,
    ModerationReason? Reason,
    ModeratorId ModeratorId,
    DateTimeOffset SetAt,
    DateTimeOffset EndTime,
    ItemState State)
{
    public bool InForce => this.State.InForce;

    public Fin<ThreadLock> Activate(DateTimeOffset at)
        => this.State.Activate(at).Map(state => this with { State = state });

    public Fin<ThreadLock> Expire(DateTimeOffset at)
        => this.State.Expire(at).Map(state => this with { State = state });

    public Fin<ThreadLock> Remove(ModeratorId by, ModerationReason? reason, DateTimeOffset at)
        => this.State.Remove(by, reason, at).Map(state => this with { State = state });
}

/// <summary>A thread lock to record: checked, with no id yet.</summary>
public sealed record NewThreadLock
{
    private NewThreadLock(ModeratorId moderatorId, ModerationReason? reason, ChannelId channelId, GuildId guildId,
        DateTimeOffset setAt, DateTimeOffset endTime)
    {
        this.ModeratorId = moderatorId;
        this.Reason = reason;
        this.ChannelId = channelId;
        this.GuildId = guildId;
        this.SetAt = setAt;
        this.EndTime = endTime;
    }

    public ModeratorId ModeratorId { get; }
    public ModerationReason? Reason { get; }
    public ChannelId ChannelId { get; }
    public GuildId GuildId { get; }
    public DateTimeOffset SetAt { get; }
    public DateTimeOffset EndTime { get; }

    public static Validation<Error, NewThreadLock> Create(
        ModeratorId moderatorId,
        ModerationReason? reason,
        ChannelId channelId,
        GuildId guildId,
        DateTimeOffset setAt,
        DateTimeOffset endTime)
        => endTime <= setAt
            ? new Invalid("End time must be after the lock's set time.")
            : Success<Error, NewThreadLock>(new NewThreadLock(moderatorId, reason, channelId, guildId,
                ItemState.Stored(setAt), endTime));
}
