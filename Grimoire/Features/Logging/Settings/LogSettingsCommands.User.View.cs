// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace

using Grimoire.Settings.Enums;

namespace Grimoire.Features.Logging.Settings;

public partial class LogSettingsCommands
{
    public partial class User
    {
        [Command("View")]
        [Description("View the current settings for the User Log module.")]
        public async Task ViewAsync(CommandContext ctx)
        {
            if (ctx is SlashCommandContext slashContext)
                await slashContext.DeferResponseAsync(true);
            else
                await ctx.DeferResponseAsync();

            var guild = ctx.Guild!;

            Task<string> Describe(GuildLogType logType) => DescribeLogChannelAsync(this._settingsModule, guild, logType);

            await ctx.ReplyAsync(
                title: "Current Logging System Settings",
                message:
                $"**Module Enabled:** {await this._settingsModule.IsModuleEnabled(Module.UserLog, guild.GetGuildId()).GetOrElse(() => false)}\n" +
                $"**Join Log:** {await Describe(GuildLogType.UserJoined)}\n" +
                $"**Leave Log:** {await Describe(GuildLogType.UserLeft)}\n" +
                $"**Username Log:** {await Describe(GuildLogType.UsernameUpdated)}\n" +
                $"**Nickname Log:** {await Describe(GuildLogType.NicknameUpdated)}\n" +
                $"**Avatar Log:** {await Describe(GuildLogType.AvatarUpdated)}\n");
        }
    }
}
