// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Queries;

namespace Grimoire.Features.Shared.Commands;

[SlashCommandGroup("GeneralSettings", "View or set general settings.")]
[RequireGuild]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
internal sealed partial class GeneralSettingsCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;
}
