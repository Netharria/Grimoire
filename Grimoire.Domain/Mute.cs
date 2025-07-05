// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;
using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public class Mute : IMember
{
    public long? SinId { get; init; }

    public virtual Sin? Sin { get; init; }

    public virtual Member Member { get; init; } = null!;

    public virtual Guild Guild { get; init; } = null!;

    public DateTimeOffset EndTime { get; init; }

    public ulong UserId { get; set; }

    public ulong GuildId { get; set; }
}
