// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Logging.Settings;

[SlashCommandGroup("Log", "View or change the settings of the Logging Modules.")]
[SlashRequireGuild]
[SlashRequireUserGuildPermissions(DiscordPermission.ManageGuild)]
public partial class LogSettingsCommands : ApplicationCommandModule
{
}
