// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

/// <summary>
///     A stored channel lock: one row per lock, which moves through <see cref="ItemState" /> (TD-1, spec 2.2). The
///     permissions are the @everyone overwrite's from before the lock, which the unlock restores. Its key is the
///     channel and the time it was set.
/// </summary>
public sealed record ChannelLock(
    GuildId GuildId,
    ChannelId ChannelId,
    ModerationReason? Reason,
    ModeratorId ModeratorId,
    DateTimeOffset SetAt,
    DateTimeOffset EndTime,
    PreviouslyAllowedPermissions PreviouslyAllowed,
    PreviouslyDeniedPermissions PreviouslyDenied,
    ItemState State)
{
    public bool InForce => this.State.InForce;

    public Fin<ChannelLock> Activate(DateTimeOffset at)
        => this.State.Activate(at).Map(state => this with { State = state });

    public Fin<ChannelLock> Expire(DateTimeOffset at)
        => this.State.Expire(at).Map(state => this with { State = state });

    public Fin<ChannelLock> Remove(ModeratorId by, ModerationReason? reason, DateTimeOffset at)
        => this.State.Remove(by, reason, at).Map(state => this with { State = state });
}

/// <summary>A channel lock to record: checked, with no id yet.</summary>
public sealed record NewChannelLock
{
    private NewChannelLock(ModeratorId moderatorId, ModerationReason? reason, ChannelId channelId, GuildId guildId,
        DateTimeOffset setAt, PreviouslyAllowedPermissions previouslyAllowed,
        PreviouslyDeniedPermissions previouslyDenied, DateTimeOffset endTime)
    {
        this.ModeratorId = moderatorId;
        this.Reason = reason;
        this.ChannelId = channelId;
        this.GuildId = guildId;
        this.SetAt = setAt;
        this.PreviouslyAllowed = previouslyAllowed;
        this.PreviouslyDenied = previouslyDenied;
        this.EndTime = endTime;
    }

    public ModeratorId ModeratorId { get; }
    public ModerationReason? Reason { get; }
    public ChannelId ChannelId { get; }
    public GuildId GuildId { get; }
    public DateTimeOffset SetAt { get; }
    public PreviouslyAllowedPermissions PreviouslyAllowed { get; }
    public PreviouslyDeniedPermissions PreviouslyDenied { get; }
    public DateTimeOffset EndTime { get; }

    public static Validation<Error, NewChannelLock> Create(
        ModeratorId moderatorId,
        ModerationReason? reason,
        ChannelId channelId,
        GuildId guildId,
        DateTimeOffset setAt,
        PreviouslyAllowedPermissions previouslyAllowed,
        PreviouslyDeniedPermissions previouslyDenied,
        DateTimeOffset endTime)
        => endTime <= setAt
            ? new Invalid("End time must be after the lock's set time.")
            : Success<Error, NewChannelLock>(new NewChannelLock(moderatorId, reason, channelId, guildId,
                ItemState.Stored(setAt),
                previouslyAllowed, previouslyDenied, endTime));
}

public readonly record struct PreviouslyAllowedPermissions(long Permissions);

public readonly record struct PreviouslyDeniedPermissions(long Permissions);
