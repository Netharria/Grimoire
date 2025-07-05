// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.ContextChecks;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

[Command("ModSettings")]
[Description("Change the settings of the Moderation Module.")]
[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
internal sealed partial class ModSettings(IMediator mediator)
{
    private readonly IMediator _mediator = mediator;
}
