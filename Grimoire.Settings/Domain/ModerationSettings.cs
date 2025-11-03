// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain;
using Grimoire.Settings.Domain.Shared;

namespace Grimoire.Settings.Domain;

public sealed class ModerationSettings : IModule
{
    public ChannelId? PublicBanLog { get; set; }
    public TimeSpan AutoPardonAfter { get; set; }
    public RoleId? MuteRole { get; set; }
    public bool AntiSpamEnabled { get; init; }
    public GuildId GuildId { get; init; }
    public bool ModuleEnabled { get; set; }
}
