// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public abstract record Mute(
    UserId UserId,
    GuildId GuildId,
    ModeratorId ModeratorId,
    DateTimeOffset SetAt);

public sealed record MuteAdded : Mute
{
    private MuteAdded(
        UserId userId,
        GuildId guildId,
        ModeratorId moderatorId,
        SinId sinId,
        DateTimeOffset setAt,
        DateTimeOffset endTime)
        : base(userId, guildId, moderatorId, setAt)
    {
        SinId = sinId;
        EndTime = endTime;
    }

    public SinId SinId { get; }
    public DateTimeOffset EndTime { get; }

    public static Validation<MuteAdded> Create(
        UserId userId,
        GuildId guildId,
        ModeratorId moderatorId,
        SinId sinId,
        DateTimeOffset setAt,
        DateTimeOffset endTime)
    {
        if (endTime <= setAt)
            return Validation<MuteAdded>.Fail(
                new Error("mute.end-time.invalid", "End time must be after the mute's set time."));
        if (userId.Value == 0)
            return Validation<MuteAdded>.Fail(
                new Error("mute.user-id.invalid", "UserId must be specified."));
        if (moderatorId.Value == 0)
            return Validation<MuteAdded>.Fail(
                new Error("mute.moderator-id.invalid", "ModeratorId must be specified."));
        if (guildId.Value == 0)
            return Validation<MuteAdded>.Fail(
                new Error("mute.guild-id.invalid", "GuildId must be specified."));
        if (moderatorId.Value == userId.Value)
            return Validation<MuteAdded>.Fail(
                new Error("mute.self-mute.invalid", "A moderator cannot mute themselves."));
        if (sinId.Value <= 0)
            return Validation<MuteAdded>.Fail(
                new Error("mute.sin-id.invalid", "Sin Id must be a positive number."));
        return Validation<MuteAdded>.Succeed(
            new MuteAdded(userId, guildId, moderatorId, sinId, setAt, endTime));
    }
}

public sealed record MuteRemoved : Mute
{
    private MuteRemoved(
        UserId userId,
        GuildId guildId,
        ModeratorId moderatorId,
        DateTimeOffset setAt)
        : base(userId, guildId, moderatorId, setAt)
    {
    }

    public static Validation<MuteRemoved> Create(
        UserId userId,
        GuildId guildId,
        ModeratorId moderatorId,
        DateTimeOffset setAt)
    {
        if (userId.Value == 0)
            return Validation<MuteRemoved>.Fail(
                new Error("mute-removed.user-id.invalid", "UserId must be specified."));
        if (moderatorId.Value == 0)
            return Validation<MuteRemoved>.Fail(
                new Error("mute-removed.moderator-id.invalid", "ModeratorId must be specified."));
        if (guildId.Value == 0)
            return Validation<MuteRemoved>.Fail(
                new Error("mute-removed.guild-id.invalid", "GuildId must be specified."));
        return Validation<MuteRemoved>.Succeed(
            new MuteRemoved(userId, guildId, moderatorId, setAt));
    }
}
