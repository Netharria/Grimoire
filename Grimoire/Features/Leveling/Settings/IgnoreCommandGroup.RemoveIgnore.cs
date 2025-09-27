// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Leveling.Settings;

public partial class IgnoreCommandGroup
{
    [Command("Remove")]
    [Description("Removes a user, channel, or role from the ignored xp list.")]
    public async Task WatchAsync(CommandContext ctx,
        [Parameter("Item")] [Description("The user, channel or role to remove from the ignore xp list.")]
        params SnowflakeObject[] value)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");


        if (value.Length == 0)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "Could not parse any ids from the submitted values.");
            return;
        }

        var ignoredMemberIds = value.OfType<DiscordUser>().Select(x => x.Id).ToArray();
        var ignoredChannelIds = value.OfType<DiscordChannel>().Select(x => x.Id).ToArray();
        var ignoredRoleIds = value.OfType<DiscordRole>().Select(x => x.Id).ToArray();


        await this._settingsModule.RemoveIgnoredItems(
            ctx.Guild.Id,
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
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = message
        });
    }
}
