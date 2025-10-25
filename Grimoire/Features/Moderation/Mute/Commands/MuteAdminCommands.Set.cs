// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.Mute.Commands;

public partial class MuteAdminCommands
{
    [Command("Set")]
    [Description("Sets the role that is used for muting users.")]
    public async Task SetMuteRoleAsync(
        SlashCommandContext ctx,
        [Parameter("Role")] [Description("The role to use for muting users.")]
        DiscordRole role)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        await this._settingsModule.SetMuteRole(role.Id, ctx.Guild.Id);

        await ctx.EditReplyAsync(message: $"Will now use role {role.Mention} for muting users.");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} updated the mute role to {role.Mention}"
        });
    }
}
