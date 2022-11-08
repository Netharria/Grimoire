// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Enums;
using Cybermancy.Core.Features.Logging.Commands.SetLogSettings;
using Cybermancy.Core.Features.Logging.Queries.GetLogSettings;
using Cybermancy.Discord.Attributes;
using Cybermancy.Discord.Extensions;
using Cybermancy.Discord.Structs;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using Mediator;

namespace Cybermancy.Discord.LoggingModule
{
    [SlashCommandGroup("LogSettings", "Changes the settings of the Logging Module")]
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Logging)]
    [SlashRequirePermissions(Permissions.ManageGuild)]
    public class LogSettingsCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public LogSettingsCommands(IMediator mediator)
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
            var DeleteChannelLog =
                    response.DeleteChannelLog is null ?
                    "None" :
                    ctx.Guild.GetChannel(response.DeleteChannelLog.Value).Mention;
            var BulkDeleteChannelLog =
                    response.BulkDeleteChannelLog is null ?
                    "None" :
                    ctx.Guild.GetChannel(response.BulkDeleteChannelLog.Value).Mention;
            var EditChannelLog =
                    response.EditChannelLog is null ?
                    "None" :
                    ctx.Guild.GetChannel(response.EditChannelLog.Value).Mention;
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
                $"**Delete Log:** {DeleteChannelLog}\n" +
                $"**Bulk Delete Log:** {BulkDeleteChannelLog}\n" +
                $"**Edit Log:** {EditChannelLog}\n" +
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

            var response = await this._mediator.Send(new SetLoggingSettingsCommand
            {
                GuildId = ctx.Guild.Id,
                LogSetting = loggingSetting,
                ChannelId = result == 0 ? null : result
            });

            if (!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }

            await ctx.ReplyAsync(message: $"Updated {loggingSetting.GetName()} to {value}", ephemeral: false);
        }
    }
}
