// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Logging.Commands.SetMessageLogSettings;
using Grimoire.Core.Features.Logging.Queries.GetMessageLogSettings;

namespace Grimoire.Discord.LoggingModule
{
    [SlashCommandGroup("LogSettings", "Changes the settings of the Logging Module")]
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.UserLog)]
    [SlashRequirePermissions(Permissions.ManageGuild)]
    public class MessageLogSettingsCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public MessageLogSettingsCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("View", "View the current settings for the logging module.")]
        public async Task ViewAsync(InteractionContext ctx)
        {
            var response = await this._mediator.Send(new GetMessageLogSettingsQuery{ GuildId = ctx.Guild.Id });
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
            await ctx.ReplyAsync(
                title: "Current Logging System Settings",
                message: $"**Module Enabled:** {response.IsLoggingEnabled}\n" +
                $"**Delete Log:** {DeleteChannelLog}\n" +
                $"**Bulk Delete Log:** {BulkDeleteChannelLog}\n" +
                $"**Edit Log:** {EditChannelLog}\n");
        }

        [SlashCommand("Set", "Set a logging setting.")]
        public async Task SetAsync(
            InteractionContext ctx,
            [Option("Setting", "The Setting to change.")] MessageLogSetting loggingSetting,
            [Option("Value", "The value to change the setting to. 0 is off. Empty is current channel")] string? value = null)
        {
            (var success, var result) = await ctx.TryMatchStringToChannelOrDefaultAsync(value);
            if (!success) return;

            var response = await this._mediator.Send(new SetMessageLogSettingsCommand
            {
                GuildId = ctx.Guild.Id,
                MessageLogSetting = loggingSetting,
                ChannelId = result == 0 ? null : result
            });

            await ctx.ReplyAsync(message: $"Updated {loggingSetting.GetName()} to {value}", ephemeral: false);
        }
    }
}
