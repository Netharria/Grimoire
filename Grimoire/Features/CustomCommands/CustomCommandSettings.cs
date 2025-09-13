// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;

namespace Grimoire.Features.CustomCommands;

[Command("Commands")]
[RequireGuild]
[RequireModuleEnabled(Module.Commands)]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
public sealed partial class CustomCommandSettings(IMediator mediator, GuildLog guildLog)
{
    private readonly IMediator _mediator = mediator;
    private readonly GuildLog _guildLog = guildLog;
}
