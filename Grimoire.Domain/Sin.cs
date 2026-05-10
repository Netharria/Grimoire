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
    public ModeratorId? ModeratorId { get; init; }

    public DateTimeOffset SinOn { get; } = DateTimeOffset.UtcNow;

    public required SinType SinType { get; init; }

    public ICollection<Pardon> Pardons { get; init; } = [];

    public ICollection<PublishedMessage> PublishMessages { get; init; } = [];

    public ICollection<SinReasonHistory> ReasonHistory { get; init; } = [];

    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    public SinId Id { get; private set; }

    public required UserId UserId { get; init; }

    public required GuildId GuildId { get; init; }
}
