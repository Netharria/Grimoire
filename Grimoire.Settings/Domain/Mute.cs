// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

/// <summary>
///     A stored mute: one row per mute, which moves through <see cref="ItemState" /> (TD-1, spec 2.2). Its key is its
///     sin: every mute comes from exactly one.
/// </summary>
/// <param name="MuteRoleId">
///     The role given to the member, so it is the one taken off again even if the server's mute role has changed since.
///     Empty when it wasn't known: the expiry then uses the server's current role.
/// </param>
public sealed record Mute(
    GuildId GuildId,
    UserId UserId,
    SinId SinId,
    RoleId? MuteRoleId,
    ModeratorId ModeratorId,
    DateTimeOffset SetAt,
    DateTimeOffset EndTime,
    ItemState State)
{
    public bool InForce => this.State.InForce;

    public Fin<Mute> Activate(DateTimeOffset at) => this.State.Activate(at).Map(state => this with { State = state });

    public Fin<Mute> Expire(DateTimeOffset at) => this.State.Expire(at).Map(state => this with { State = state });

    public Fin<Mute> Remove(ModeratorId by, ModerationReason? reason, DateTimeOffset at)
        => this.State.Remove(by, reason, at).Map(state => this with { State = state });
}

/// <summary>A mute to record: checked, with no id yet.</summary>
public sealed record NewMute
{
    private NewMute(UserId userId, GuildId guildId, ModeratorId moderatorId, SinId sinId, DateTimeOffset setAt,
        DateTimeOffset endTime, RoleId? muteRoleId)
    {
        this.UserId = userId;
        this.GuildId = guildId;
        this.ModeratorId = moderatorId;
        this.SinId = sinId;
        this.SetAt = setAt;
        this.EndTime = endTime;
        this.MuteRoleId = muteRoleId;
    }

    public UserId UserId { get; }
    public GuildId GuildId { get; }
    public ModeratorId ModeratorId { get; }
    public SinId SinId { get; }
    public DateTimeOffset SetAt { get; }
    public DateTimeOffset EndTime { get; }
    public RoleId? MuteRoleId { get; }

    public static Validation<Error, NewMute> Create(
        UserId userId,
        GuildId guildId,
        ModeratorId moderatorId,
        SinId sinId,
        DateTimeOffset setAt,
        DateTimeOffset endTime,
        RoleId? muteRoleId = null)
    {
        if (endTime <= setAt)
            return new Invalid("End time must be after the mute's set time.");
        if (moderatorId.Value == userId.Value)
            return new Invalid("A moderator cannot mute themselves.");
        if (sinId.Value <= 0)
            return new Invalid("Sin Id must be a positive number.");
        return Success<Error, NewMute>(
            new NewMute(userId, guildId, moderatorId, sinId, ItemState.Stored(setAt), endTime, muteRoleId));
    }
}
