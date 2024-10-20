// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.CustomCommands;

[SlashCommandGroup("Commands", "Manage custom commands.")]
[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Commands)]
[SlashRequireUserGuildPermissions(DiscordPermissions.ManageGuild)]
public partial class CustomCommandSettings(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;
}
