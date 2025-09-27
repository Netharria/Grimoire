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

namespace Grimoire.Features.Leveling.Settings;

[RequireGuild]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
[RequireModuleEnabled(Module.Leveling)]
[Command("Ignore")]
[Description("Commands for updating and viewing the server ignore list.")]
public sealed partial class IgnoreCommandGroup(SettingsModule settingsModule, GuildLog guildLog)
{
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    private static string BuildIgnoreListAsync(
        IEnumerable<ulong> ignoredChannelIds,
        IEnumerable<ulong> ignoredRoleIds,
        IEnumerable<ulong> ignoredMemberIds)
    {
        return string.Join(' ',
                   ignoredMemberIds.Select(x => $"<@{x}>")) +
               string.Join(' ',
                   ignoredChannelIds.Select(x => $"<#{x}>")) +
               string.Join(' ',
                   ignoredRoleIds.Select(x => $"<@&{x}>"));
    }
}
