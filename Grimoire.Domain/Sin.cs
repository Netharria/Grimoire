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
    Ban
}

[UsedImplicitly]
public sealed class Sin
{
    public Member? Member { get; init; }

    public ulong? ModeratorId { get; init; }

    public Member? Moderator { get; init; }

    public Guild? Guild { get; init; }

    public required string Reason { get; set; }

    public DateTimeOffset SinOn { get; private init; }

    public required SinType SinType { get; init; }

    public Pardon? Pardon { get; set; }

    public ICollection<PublishedMessage> PublishMessages { get; init; } = [];
    public required long Id { get; set; }

    public required ulong UserId { get; set; }

    public required ulong GuildId { get; set; }
}
