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

            var joinLog = await this._settingsModule.GetLogChannelSetting(GuildLogType.UserJoined, guild.GetGuildId());
            var leaveLog = await this._settingsModule.GetLogChannelSetting(GuildLogType.UserLeft, guild.GetGuildId());
            var usernameUpdated =
                await this._settingsModule.GetLogChannelSetting(GuildLogType.UsernameUpdated, guild.GetGuildId());
            var nicknameUpdated =
                await this._settingsModule.GetLogChannelSetting(GuildLogType.NicknameUpdated, guild.GetGuildId());
            var avatarUpdated =
                await this._settingsModule.GetLogChannelSetting(GuildLogType.AvatarUpdated, guild.GetGuildId());

            var joinChannelLog =
                joinLog is null
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(joinLog))?.Mention ?? "Deleted Channel";
            var leaveChannelLog =
                leaveLog is null
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(leaveLog))?.Mention ?? "Deleted Channel";
            var usernameChannelLog =
                usernameUpdated is null
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(usernameUpdated))?.Mention ?? "Deleted Channel";
            var nicknameChannelLog =
                nicknameUpdated is null
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(nicknameUpdated))?.Mention ?? "Deleted Channel";
            var avatarChannelLog =
                avatarUpdated is null
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(avatarUpdated))?.Mention ?? "Deleted Channel";
            await ctx.EditReplyAsync(
                title: "Current Logging System Settings",
                message:
                $"**Module Enabled:** {await this._settingsModule.IsModuleEnabled(Module.UserLog, guild.GetGuildId())}\n" +
                $"**Join Log:** {joinChannelLog}\n" +
                $"**Leave Log:** {leaveChannelLog}\n" +
                $"**Username Log:** {usernameChannelLog}\n" +
                $"**Nickname Log:** {nicknameChannelLog}\n" +
                $"**Avatar Log:** {avatarChannelLog}\n");
        }
    }
}
