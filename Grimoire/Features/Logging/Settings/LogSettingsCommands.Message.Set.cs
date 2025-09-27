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
    public partial class Message
    {
        public enum MessageLogSetting
        {
            [ChoiceDisplayName("Delete Message Log")] DeleteLog,
            [ChoiceDisplayName("Bulk Delete Message Log")] BulkDeleteLog,
            [ChoiceDisplayName("Edit Message Log")] EditLog
        }

        [Command("Set")]
        [Description("Set a Message Log setting.")]
        public async Task SetAsync(
            SlashCommandContext ctx,
            [Parameter("Setting")] [Description("The setting to change.")]
            MessageLogSetting logSetting,
            [Parameter("Option")]
            [Description("Select whether to turn log off, use the current channel, or specify a channel")]
            ChannelOption option,
            [Parameter("Value")] [Description("The channel to change the log setting to.")]
            DiscordChannel? channel = null)
        {
            await ctx.DeferResponseAsync();

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            channel = ctx.GetChannelOptionAsync(option, channel);
            if (channel is not null)
            {
                var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
                if (!permissions.HasPermission(DiscordPermission.SendMessages))
                    throw new AnticipatedException(
                        $"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
            }

            await this._settingsModule.SetLogChannelSetting(
                logSetting switch
                {
                    MessageLogSetting.DeleteLog => GuildLogType.MessageDeleted,
                    MessageLogSetting.BulkDeleteLog => GuildLogType.BulkMessageDeleted,
                    MessageLogSetting.EditLog => GuildLogType.MessageEdited,
                    _ => throw new ArgumentOutOfRangeException(nameof(logSetting), logSetting, null)
                },
                ctx.Guild.Id,
                option is ChannelOption.Off ? null : channel?.Id);

            await ctx.EditReplyAsync(message: option is ChannelOption.Off
                ? $"Disabled {logSetting}"
                : $"Updated {logSetting} to {channel?.Mention}");

            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Description = option is ChannelOption.Off
                    ? $"{ctx.User.Mention} disabled {logSetting}."
                    : $"{ctx.User.Mention} updated {logSetting} to {channel?.Mention}.",
                Color = GrimoireColor.Purple
            });
        }
    }
}
