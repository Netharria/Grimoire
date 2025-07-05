// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public class Pardon
{
    public long SinId { get; init; }

    public virtual Sin Sin { get; init; } = null!;

    public ulong? ModeratorId { get; init; }

    public virtual Member? Moderator { get; init; }

    public ulong GuildId { get; init; }
    public virtual Guild Guild { get; init; } = null!;

    public DateTimeOffset PardonDate { get; init; }

    public string Reason { get; set; } = string.Empty;
}
