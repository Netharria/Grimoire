// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain;

public class Mute : IMember
{
    public long SinId { get; set; }

    public virtual Sin Sin { get; set; } = null!;

    public ulong UserId { get; set; }

    public virtual Member Member { get; set; } = null!;

    public ulong GuildId { get; set; }

    public virtual Guild Guild { get; set; } = null!;

    public DateTimeOffset EndTime { get; set; }
}
