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
        public async Task ViewAsync(SlashCommandContext ctx)
        {
            await ctx.DeferResponseAsync(true);

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            var joinLog = await this._settingsModule.GetLogChannelSetting(GuildLogType.UserJoined, ctx.Guild.Id);
            var leaveLog = await this._settingsModule.GetLogChannelSetting(GuildLogType.UserLeft, ctx.Guild.Id);
            var usernameUpdated =
                await this._settingsModule.GetLogChannelSetting(GuildLogType.UsernameUpdated, ctx.Guild.Id);
            var nicknameUpdated =
                await this._settingsModule.GetLogChannelSetting(GuildLogType.NicknameUpdated, ctx.Guild.Id);
            var avatarUpdated =
                await this._settingsModule.GetLogChannelSetting(GuildLogType.AvatarUpdated, ctx.Guild.Id);

            var joinChannelLog =
                joinLog is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(joinLog.Value)).Mention;
            var leaveChannelLog =
                leaveLog is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(leaveLog.Value)).Mention;
            var usernameChannelLog =
                usernameUpdated is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(usernameUpdated.Value)).Mention;
            var nicknameChannelLog =
                nicknameUpdated is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(nicknameUpdated.Value)).Mention;
            var avatarChannelLog =
                avatarUpdated is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(avatarUpdated.Value)).Mention;
            await ctx.EditReplyAsync(
                title: "Current Logging System Settings",
                message:
                $"**Module Enabled:** {await this._settingsModule.IsModuleEnabled(Module.UserLog, ctx.Guild.Id)}\n" +
                $"**Join Log:** {joinChannelLog}\n" +
                $"**Leave Log:** {leaveChannelLog}\n" +
                $"**Username Log:** {usernameChannelLog}\n" +
                $"**Nickname Log:** {nicknameChannelLog}\n" +
                $"**Avatar Log:** {avatarChannelLog}\n");
        }
    }
}
