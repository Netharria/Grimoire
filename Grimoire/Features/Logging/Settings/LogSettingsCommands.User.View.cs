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

            var joinLog =
                await this._settingsModule.GetConfiguredLogChannelSetting(GuildLogType.UserJoined, guild.GetGuildId());
            var leaveLog =
                await this._settingsModule.GetConfiguredLogChannelSetting(GuildLogType.UserLeft, guild.GetGuildId());
            var usernameUpdated =
                await this._settingsModule.GetConfiguredLogChannelSetting(GuildLogType.UsernameUpdated,
                    guild.GetGuildId());
            var nicknameUpdated =
                await this._settingsModule.GetConfiguredLogChannelSetting(GuildLogType.NicknameUpdated,
                    guild.GetGuildId());
            var avatarUpdated =
                await this._settingsModule.GetConfiguredLogChannelSetting(GuildLogType.AvatarUpdated,
                    guild.GetGuildId());

            var joinChannelLog =
                joinLog.GetOrElse(() => null) is not { } join
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(join))?.Mention ?? "Deleted Channel";
            var leaveChannelLog =
                leaveLog.GetOrElse(() => null) is not { } leave
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(leave))?.Mention ?? "Deleted Channel";
            var usernameChannelLog =
                usernameUpdated.GetOrElse(() => null) is not { } username
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(username))?.Mention ?? "Deleted Channel";
            var nicknameChannelLog =
                nicknameUpdated.GetOrElse(() => null) is not { } nickname
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(nickname))?.Mention ?? "Deleted Channel";
            var avatarChannelLog =
                avatarUpdated.GetOrElse(() => null) is not { } avatar
                    ? "None"
                    : (await guild.GetChannelOrDefaultAsync(avatar))?.Mention ?? "Deleted Channel";
            await ctx.ReplyAsync(
                title: "Current Logging System Settings",
                message:
                $"**Module Enabled:** {await this._settingsModule.IsModuleEnabled(Module.UserLog, guild.GetGuildId()).GetOrElse(() => false)}\n" +
                $"**Join Log:** {joinChannelLog}\n" +
                $"**Leave Log:** {leaveChannelLog}\n" +
                $"**Username Log:** {usernameChannelLog}\n" +
                $"**Nickname Log:** {nicknameChannelLog}\n" +
                $"**Avatar Log:** {avatarChannelLog}\n");
        }
    }
}
