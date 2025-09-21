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
[Obsolete("Use Settings Module Instead.")]
public sealed class GuildModerationSettings : IModule
{
    public ulong GuildId { get; init; }
    public ulong? PublicBanLog { get; set; }
    public TimeSpan AutoPardonAfter { get; set; }
    public ulong? MuteRole { get; set; }
    public Role? MuteRoleNav { get; init; }
    public bool AntiSpamEnabled { get; init; }
    public Guild? Guild { get; set; }
    public bool ModuleEnabled { get; set; }
}
