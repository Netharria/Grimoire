// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain;

public class GuildLevelSettings : IModule
{
    public ulong GuildId { get; set; }

    public virtual Guild Guild { get; set; } = null!;

    public TimeSpan TextTime { get; set; }

    public int Base { get; set; }

    public int Modifier { get; set; }

    public int Amount { get; set; }

    public ulong? LevelChannelLogId { get; set; }

    public virtual Channel? LevelChannelLog { get; set; }

    public bool ModuleEnabled { get; set; }
}
