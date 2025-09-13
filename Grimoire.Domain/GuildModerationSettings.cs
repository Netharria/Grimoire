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
public class GuildModerationSettings : IModule
{
    public ulong GuildId { get; init; }
    [Obsolete("Temporary to help identify where configurations are changed.")]
    public ulong? PublicBanLog { get; set; }
    [Obsolete("Temporary to help identify where configurations are changed.")]
    public TimeSpan AutoPardonAfter { get; set; }
    [Obsolete("Temporary to help identify where configurations are changed.")]
    public ulong? MuteRole { get; set; }
    public virtual Role? MuteRoleNav { get; init; }
    [Obsolete("Temporary to help identify where configurations are changed.")]
    public bool AntiSpamEnabled { get; init; }

    public virtual Guild Guild { get; set; } = null!;
    [Obsolete("Temporary to help identify where configurations are changed.")]
    public bool ModuleEnabled { get; set; }
}
