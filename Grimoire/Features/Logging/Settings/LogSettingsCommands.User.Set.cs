// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;
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
            [Parameter("Setting")] [Description("The setting to change.")]
            UserLogSetting logSetting,
            [Parameter("Option")]
            [Description("Select whether to turn log off, use the current channel, or specify a channel")]
            ChannelOption option,
            [Parameter("Value")] [Description("The channel to change the log setting to.")]
            DiscordChannel? channel = null)
        {
            await ctx.DeferResponseAsync();

            var guild = ctx.Guild!;

            var channelOption = ctx.GetChannelOption(option, channel);

            if (channelOption.IsFail)
            {
                await ctx.EditReplyAsync(GrimoireColor.Yellow,
                    "Selected channel cannot be empty when ChannelOption is SelectChannel.");
                return;
            }

            channelOption.Match(
                success => channel = success,
                failure => { }); // Handled above

            if (channel is not null)
            {
                var permissions = channel.PermissionsFor(guild.CurrentMember);
                if (!permissions.HasPermission(DiscordPermission.SendMessages))
                {
                    await ctx.EditReplyAsync(GrimoireColor.Yellow,
                        $"{guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
                    return;
                }
            }

            await this._settingsModule.SetLogChannelSetting(
                logSetting switch
                {
                    UserLogSetting.JoinLog => GuildLogType.UserJoined,
                    UserLogSetting.LeaveLog => GuildLogType.UserLeft,
                    UserLogSetting.UsernameLog => GuildLogType.UsernameUpdated,
                    UserLogSetting.NicknameLog => GuildLogType.NicknameUpdated,
                    UserLogSetting.AvatarLog => GuildLogType.AvatarUpdated,
                    _ => throw new ArgumentOutOfRangeException(nameof(logSetting), logSetting, null)
                },
                guild.GetGuildId(),
                option is ChannelOption.Off ? null : channel?.GetChannelId());


            await ctx.EditReplyAsync(message: option is ChannelOption.Off
                ? $"Disabled {logSetting}"
                : $"Updated {logSetting} to {channel?.Mention}");
            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = guild.GetGuildId(),
                GuildLogType = GuildLogType.Moderation,
                Description = option is ChannelOption.Off
                    ? $"{ctx.User.Mention} disabled {logSetting}."
                    : $"{ctx.User.Mention} updated {logSetting} to {channel?.Mention}.",
                Color = GrimoireColor.Purple
            });
        }
    }
}
