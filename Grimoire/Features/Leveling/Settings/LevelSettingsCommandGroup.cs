// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Leveling.Settings;

[SlashCommandGroup("LevelSettings", "Changes the settings of the Leveling Module.")]
[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Leveling)]
[SlashRequireUserGuildPermissions(DiscordPermissions.ManageGuild)]
public sealed partial class LevelSettingsCommandGroup(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;
}
