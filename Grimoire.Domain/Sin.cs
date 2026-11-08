// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

public enum SinType
{
    Warn,
    Mute,
    Ban,
    Kick
}

[UsedImplicitly]
public sealed record Sin
{
    // EF Core sets this via field access after INSERT for the PostgreSQL bigserial identity.
    public SinId Id { get; init; }

    public required ModerationActor Actor { get; init; }
    public required DateTimeOffset SinOn { get; init; }
    public required SinType SinType { get; init; }
    public required UserId UserId { get; init; }
    public required GuildId GuildId { get; init; }

    public ICollection<Pardon> Pardons { get; init; } = [];
    public ICollection<PublishedMessage> PublishMessages { get; init; } = [];
    public ICollection<SinReasonHistory> ReasonHistory { get; init; } = [];

    public static Validation<Sin> ForWarn(
        ModerationActor actor, UserId userId, GuildId guildId, DateTimeOffset occurredAt, ModerationReason? reason = null)
        => Create(SinType.Warn, actor, userId, guildId, occurredAt, reason);

    public static Validation<Sin> ForMute(
        ModerationActor actor, UserId userId, GuildId guildId, DateTimeOffset occurredAt, ModerationReason? reason = null)
        => Create(SinType.Mute, actor, userId, guildId, occurredAt, reason);

    public static Validation<Sin> ForBan(
        ModerationActor actor, UserId userId, GuildId guildId, DateTimeOffset occurredAt, ModerationReason? reason = null)
        => Create(SinType.Ban, actor, userId, guildId, occurredAt, reason);

    public static Validation<Sin> ForKick(
        ModerationActor actor, UserId userId, GuildId guildId, DateTimeOffset occurredAt, ModerationReason? reason = null)
        => Create(SinType.Kick, actor, userId, guildId, occurredAt, reason);

    /// <param name="reason">The reason it starts with, if any, recorded as given by <paramref name="actor" />.</param>
    private static Validation<Sin> Create(
        SinType type, ModerationActor actor, UserId userId, GuildId guildId, DateTimeOffset occurredAt,
        ModerationReason? reason)
    {
        if (userId.Value == 0)
            return Validation<Sin>.Fail(
                new Error("sin.user-id.invalid", "UserId must be specified."));
        if (guildId.Value == 0)
            return Validation<Sin>.Fail(
                new Error("sin.guild-id.invalid", "GuildId must be specified."));
        if (actor is ModerationActor.Moderator m && m.Id.Value == 0)
            return Validation<Sin>.Fail(
                new Error("sin.moderator-id.invalid", "ModeratorId must be specified."));
        return Validation<Sin>.Succeed(new Sin
        {
            Actor = actor,
            SinType = type,
            UserId = userId,
            GuildId = guildId,
            SinOn = occurredAt,
            // The sin's id is assigned on save; EF sets the history's from the navigation.
            ReasonHistory = reason is { } given
                ? [new SinReasonHistory { SinId = default, Reason = given, Actor = actor, SetAt = occurredAt }]
                : []
        });
    }
}
