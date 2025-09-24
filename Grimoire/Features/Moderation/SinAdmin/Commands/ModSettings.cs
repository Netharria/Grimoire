// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

[Command("ModSettings")]
[Description("Change the settings of the Moderation Module.")]
[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
internal sealed partial class ModSettings(IMediator mediator, GuildLog guildLog)
{
    private readonly GuildLog _guildLog = guildLog;
    private readonly IMediator _mediator = mediator;
}
