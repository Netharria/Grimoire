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
    public ulong? ModeratorId { get; init; }

    public required string Reason { get; set; }

    public DateTimeOffset SinOn { get; } = DateTimeOffset.UtcNow;

    public required SinType SinType { get; init; }

    public Pardon? Pardon { get; set; }

    public ICollection<PublishedMessage> PublishMessages { get; init; } = [];


    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    public long Id { get; private set; }

    public required ulong UserId { get; init; }

    public required ulong GuildId { get; init; }
}
