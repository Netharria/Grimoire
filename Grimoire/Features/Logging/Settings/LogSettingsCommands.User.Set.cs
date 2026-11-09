// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Features.Shared.Commands;
using Grimoire.Settings.Enums;

// ReSharper disable once CheckNamespace
namespace Grimoire.Features.Logging.Settings;

public partial class LogSettingsCommands
{
    public partial class User
    {
        public enum UserLogSetting
        {
            [ChoiceDisplayName("Joined Server Log")]
            JoinLog,
            [ChoiceDisplayName("Left Server Log")] LeaveLog,

            [ChoiceDisplayName("Username Change Log")]
            UsernameLog,

            [ChoiceDisplayName("Nickname Change Log")]
            NicknameLog,

            [ChoiceDisplayName("Avatar Change Log")]
            AvatarLog
        }

        [Command("Set")]
        [Description("Set a User Log setting.")]
        public async Task SetAsync(
            CommandContext ctx,
            [Parameter("setting")] [Description("The setting to change.")]
            UserLogSetting logSetting,
            [Parameter("option")]
            [Description("Select whether to turn log off, use the current channel, or specify a channel")]
            ChannelOption option,
            [Parameter("value")] [Description("The channel to change the log setting to.")]
            DiscordChannel? channel = null)
        {
            await ctx.DeferResponseAsync();
            var logType = logSetting switch
            {
                UserLogSetting.JoinLog => GuildLogType.UserJoined,
                UserLogSetting.LeaveLog => GuildLogType.UserLeft,
                UserLogSetting.UsernameLog => GuildLogType.UsernameUpdated,
                UserLogSetting.NicknameLog => GuildLogType.NicknameUpdated,
                UserLogSetting.AvatarLog => GuildLogType.AvatarUpdated,
                _ => throw new InvariantViolationException("Invalid user log setting.")
            };
            await ChannelSettingCommand.SetAsync(ctx, this._guildLog, option, channel, logSetting.ToString(),
                channelId => this._settingsModule.SetLogChannelSetting(logType, ctx.RequiredGuild.GetGuildId(),
                    ctx.GetModeratorId(), channelId),
                logType is GuildLogType.AvatarUpdated
                    ? ChannelSettingCommand.LogWithFilesPermissions
                    : ChannelSettingCommand.LogPermissions);
        }
    }
}
