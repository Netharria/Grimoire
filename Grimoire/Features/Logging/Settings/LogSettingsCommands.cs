// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Logging.Settings;

[Command("Log")]
[Description("View or change the settings of the Logging Modules.")]
[RequireGuild]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
public partial class LogSettingsCommands
{
    [Command("Message")]
    [Description("View or change the Message Log Module Settings.")]
    [RequireModuleEnabled(Module.MessageLog)]
    public partial class Message(SettingsModule settingsModule, GuildLog guildLog)
    {
        private readonly GuildLog _guildLog = guildLog;
        private readonly SettingsModule _settingsModule = settingsModule;
    }

    [RequireModuleEnabled(Module.UserLog)]
    [Command("User")]
    [Description("View or change the User Log Module Settings.")]
    public partial class User(SettingsModule settingsModule, GuildLog guildLog)
    {
        private readonly GuildLog _guildLog = guildLog;
        private readonly SettingsModule _settingsModule = settingsModule;
    }
}
