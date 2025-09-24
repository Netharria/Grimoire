// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
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

    private static async Task<StringBuilder> BuildIgnoreListAsync(
        CommandContext ctx,
        IEnumerable<IgnoredChannel> ignoredChannels,
        IEnumerable<IgnoredRole> ignoredRoles,
        IEnumerable<IgnoredMember> ignoredMembers)
    {
        ArgumentNullException.ThrowIfNull(ctx.Guild);
        var stringBuilder = new StringBuilder();
        foreach (var ignorable in ignoredMembers)
        {
            var ignoredMember = await ctx.Client.GetUserOrDefaultAsync(ignorable.UserId);
            if (ignoredMember is not null)
                stringBuilder.Append(ignoredMember.Mention).Append(' ');
        }

        foreach (var ignorable in ignoredRoles)
        {
            var ignoredRole = await ctx.Guild.GetRoleOrDefaultAsync(ignorable.RoleId);
            if (ignoredRole is not null)
                stringBuilder.Append(ignoredRole.Mention).Append(' ');
        }

        foreach (var ignorable in ignoredChannels)
        {
            var ignoredChannel = await ctx.Guild.GetChannelOrDefaultAsync(ignorable.ChannelId);
            if (ignoredChannel is not null)
                stringBuilder.Append(ignoredChannel.Mention).Append(' ');
        }

        return stringBuilder;
    }
}
