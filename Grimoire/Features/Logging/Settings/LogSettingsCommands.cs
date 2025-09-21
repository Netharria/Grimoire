// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;

namespace Grimoire.Features.Logging.Settings;

[Command("Log")]
[Description("View or change the settings of the Logging Modules.")]
[RequireGuild]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
public partial class LogSettingsCommands
{
}
