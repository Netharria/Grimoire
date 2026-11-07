// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Logging.Settings;

public partial class LogSettingsCommands
{
    public partial class Message
    {
        public enum MessageLogOverrideSetting
        {
            Always,
            Inherit,
            Never
        }

        [Command("Override")]
        [Description("Overrides the default message logging settings. Use this to control which channels are logged.")]
        public async Task Override(
            CommandContext ctx,
            [Parameter("option")] [Description("Override option to set the channel to")]
            MessageLogOverrideSetting overrideSetting,
            [Parameter("channel")]
            [Description("The channel to override the message log settings of. Leave empty for current channel.")]
            DiscordChannel? channel = null)
        {
            await ctx.DeferResponseAsync();
            channel ??= ctx.Channel;
            var guild = ctx.Guild!;

            var (option, message) = overrideSetting switch
            {
                MessageLogOverrideSetting.Always => (MessageLogOverrideOption.AlwaysLog,
                    $"Will now always log messages from {channel.Mention} and its sub channels/threads."),
                MessageLogOverrideSetting.Never => (MessageLogOverrideOption.NeverLog,
                    $"Will now never log messages from {channel.Mention} and its sub channels/threads."),
                MessageLogOverrideSetting.Inherit => (MessageLogOverrideOption.Inherit,
                    "Override was successfully removed from the channel."),
                _ => throw new UnreachableException("Invalid message log override option.")
            };

            await ctx.ReplyResultAsync(
                await this._settingsModule.SetChannelLogOverride(channel.GetChannelId(), guild.GetGuildId(),
                    ctx.GetModeratorId(), option),
                message,
                () => this._guildLog.SendLogMessageAsync(new GuildLogMessage
                {
                    GuildId = guild.GetGuildId(),
                    GuildLogType = GuildLogType.Moderation,
                    Title = $"{ctx.User.Mention} updated the channel overrides",
                    Description = message,
                    Color = GrimoireColor.Purple
                }));
        }
    }
}
