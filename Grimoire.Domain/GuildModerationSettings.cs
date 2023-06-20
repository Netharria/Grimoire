// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain;

public class GuildModerationSettings : IModule
{
    public ulong GuildId { get; set; }

    public virtual Guild Guild { get; set; } = null!;

    public ulong? PublicBanLog { get; set; }

    public TimeSpan AutoPardonAfter { get; set; }

    public ulong? MuteRole { get; set; }
    public bool ModuleEnabled { get; set; }
}
