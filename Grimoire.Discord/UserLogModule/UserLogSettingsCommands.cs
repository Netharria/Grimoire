// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Logging.Commands.SetLogSettings;
using Grimoire.Core.Features.Logging.Queries.GetLogSettings;

namespace Grimoire.Discord.LoggingModule
{
    [SlashCommandGroup("LogSettings", "Changes the settings of the Logging Module")]
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.UserLog)]
    [SlashRequirePermissions(Permissions.ManageGuild)]
    public class UserLogSettingsCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public UserLogSettingsCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("View", "View the current settings for the logging module.")]
        public async Task ViewAsync(InteractionContext ctx)
        {
            var response = await this._mediator.Send(new GetLoggingSettingsQuery{ GuildId = ctx.Guild.Id });
            var JoinChannelLog =
                    response.JoinChannelLog is null ?
                    "None" :
                    ctx.Guild.GetChannel(response.JoinChannelLog.Value).Mention;
            var LeaveChannelLog  =
                    response.LeaveChannelLog  is null ?
                    "None" :
                    ctx.Guild.GetChannel(response.LeaveChannelLog.Value).Mention;
            var UsernameChannelLog =
                    response.UsernameChannelLog is null ?
                    "None" :
                    ctx.Guild.GetChannel(response.UsernameChannelLog.Value).Mention;
            var NicknameChannelLog =
                    response.NicknameChannelLog is null ?
                    "None" :
                    ctx.Guild.GetChannel(response.NicknameChannelLog.Value).Mention;
            var AvatarChannelLog =
                    response.AvatarChannelLog is null ?
                    "None" :
                    ctx.Guild.GetChannel(response.AvatarChannelLog.Value).Mention;
            await ctx.ReplyAsync(
                title: "Current Logging System Settings",
                message: $"**Module Enabled:** {response.IsLoggingEnabled}\n" +
                $"**Join Log:** {JoinChannelLog}\n" +
                $"**Leave Log:** {LeaveChannelLog}\n" +
                $"**Username Log:** {UsernameChannelLog}\n" +
                $"**Nickname Log:** {NicknameChannelLog}\n" +
                $"**Avatar Log:** {AvatarChannelLog}\n");
        }

        [SlashCommand("Set", "Set a logging setting.")]
        public async Task SetAsync(
            InteractionContext ctx,
            [Option("Setting", "The Setting to change.")] LoggingSetting loggingSetting,
            [Option("Value", "The value to change the setting to. 0 is off. Empty is current channel")] string? value = null)
        {
            (var success, var result) = await ctx.TryMatchStringToChannelOrDefaultAsync(value);
            if (!success) return;

            await this._mediator.Send(new SetLoggingSettingsCommand
            {
                GuildId = ctx.Guild.Id,
                LogSetting = loggingSetting,
                ChannelId = result == 0 ? null : result
            });

            await ctx.ReplyAsync(message: $"Updated {loggingSetting.GetName()} to {value}", ephemeral: false);
        }
    }
}
