// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Leveling.Settings;

public partial class IgnoreCommandGroup
{
    [RequireGuild]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [RequireModuleEnabled(Module.Leveling)]
    [Command("Remove")]
    [Description("Removes a user, channel, or role from the ignored xp list.")]
    public async Task WatchAsync(CommandContext ctx,
        [Parameter("Item")] [Description("The user, channel or role to remove from the ignore xp list.")]
        params SnowflakeObject[] value)
    {
        await ctx.DeferResponseAsync();
        var guild = ctx.Guild!;


        if (value.Length == 0)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "Could not parse any ids from the submitted values.");
            return;
        }

        var ignoredMemberIds = value.OfType<DiscordUser>().Select(x => x.Id).ToArray();
        var ignoredChannelIds = value.OfType<DiscordChannel>().Select(x => x.Id).ToArray();
        var ignoredRoleIds = value.OfType<DiscordRole>().Select(x => x.Id).ToArray();


        await this._settingsModule.RemoveIgnoredItems(
            guild.Id,
            ignoredMemberIds,
            ignoredChannelIds,
            ignoredRoleIds);

        var message = BuildIgnoreListAsync(
                          ignoredMemberIds,
                          ignoredChannelIds,
                          ignoredRoleIds)
                      + " are no longer ignored for xp gain.";

        await ctx.EditReplyAsync(GrimoireColor.Green,
            message);
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = message
        });
    }
}
