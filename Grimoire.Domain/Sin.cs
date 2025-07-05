// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;
using JetBrains.Annotations;

namespace Grimoire.Domain;

public enum SinType
{
    Warn,
    Mute,
    Ban
}

[UsedImplicitly]
public class Sin : IIdentifiable<long>, IMember
{
    public virtual Member Member { get; init; } = null!;

    public ulong? ModeratorId { get; init; }

    public virtual Member? Moderator { get; init; }

    public virtual Guild Guild { get; init; } = null!;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset SinOn { get; init; }

    public SinType SinType { get; init; }

    public virtual Mute? Mute { get; init; }

    public virtual Pardon? Pardon { get; set; }

    public virtual ICollection<PublishedMessage> PublishMessages { get; init; } = [];
    public long Id { get; set; }

    public ulong UserId { get; set; }

    public ulong GuildId { get; set; }
}
