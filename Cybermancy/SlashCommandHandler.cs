// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Cybermancy.Enums;
using Cybermancy.Extensions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using DSharpPlus.SlashCommands.EventArgs;
using Microsoft.Extensions.Logging;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting.Attributes;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting.Events;

namespace Cybermancy
{
    [DiscordSlashCommandsEventsSubscriber]
    public class SlashCommandHandler : IDiscordSlashCommandsEventsSubscriber
    {
        private readonly ILogger<SlashCommandHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SlashCommandHandler"/> class.
        /// </summary>
        /// <param name="logger"></param>
        public SlashCommandHandler(ILogger<SlashCommandHandler> logger)
        {
            this._logger = logger;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="commandOptions"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public static async Task<StringBuilder> BuildSlashCommandLogAsync(StringBuilder builder, IEnumerable<DiscordInteractionDataOption> commandOptions)
        {
            foreach (var option in commandOptions)
            {
                builder.Append(option.Name).Append(' ');
                if (option.Options is not null)
                    await BuildSlashCommandLogAsync(builder, option.Options);
                if (option.Value is not null)
                    builder.Append('\'').Append(option.Value).Append("' ");
            }

            return builder;
        }

        public Task SlashCommandsOnContextMenuErrored(SlashCommandsExtension sender, ContextMenuErrorEventArgs args) => Task.CompletedTask;

        public Task SlashCommandsOnContextMenuExecuted(SlashCommandsExtension sender, ContextMenuExecutedEventArgs args) => Task.CompletedTask;

        public async Task SlashCommandsOnSlashCommandErrored(SlashCommandsExtension sender, SlashCommandErrorEventArgs args)
        {
            if (args.Exception is SlashExecutionChecksFailedException ex)
                foreach (var check in ex.FailedChecks)
                {
                    if (check is SlashRequireGuildAttribute)
                        await args.Context.ReplyAsync(color: CybermancyColor.Green, message: "You need to be in a server to use this command.");

                    if (check is SlashRequirePermissionsAttribute requirePermissions)
                    {
                        var value = Enum.ToObject(typeof(Permissions), requirePermissions.Permissions).ToString();
                        await args.Context.ReplyAsync(color: CybermancyColor.Green, message: $"You and {args.Context.Guild.CurrentMember.DisplayName} need {value} permissions to use this command.");
                    }

                    if (check is SlashRequireUserPermissionsAttribute requireUserPermissions)
                    {
                        var value = Enum.ToObject(typeof(Permissions), requireUserPermissions.Permissions).ToString();
                        await args.Context.ReplyAsync(color: CybermancyColor.Green, message: $"You need {value} permissions to use this command.");
                    }

                    if (check is SlashRequireBotPermissionsAttribute requireBotPermissions)
                    {
                        var value = Enum.ToObject(typeof(Permissions), requireBotPermissions.Permissions).ToString();
                        await args.Context.ReplyAsync(color: CybermancyColor.Green, message: $"{args.Context.Guild.CurrentMember.DisplayName} needs {value} permissions to use this command.");
                    }

                    if (check is SlashRequireOwnerAttribute)
                        await args.Context.ReplyAsync(color: CybermancyColor.Green, message: $"You need to be {args.Context.Guild.CurrentMember.DisplayName}'s owner to use this command");

                    if (check is SlashRequireDirectMessageAttribute)
                        await args.Context.ReplyAsync(color: CybermancyColor.Green, message: $"You need to DM {args.Context.Guild.CurrentMember.DisplayName} to use this command.");
                }
            else if (args.Exception is not null)
            {
                var commandOptions = args.Context.Interaction.Data.Options;
                var log = new StringBuilder();
                log.Append("Error on Slash Command: ").Append(args.Context.Interaction.Data.Name).Append(' ');
                if (commandOptions is not null)
                    await BuildSlashCommandLogAsync(log, commandOptions);
                log.Append('\n').Append(args.Exception.Message).Append('\n').Append(args.Exception.StackTrace);
                this._logger.LogInformation(log.ToString());
            }
        }

        public async Task SlashCommandsOnSlashCommandExecuted(SlashCommandsExtension sender, SlashCommandExecutedEventArgs args)
        {
            var commandOptions = args.Context.Interaction.Data.Options;
            var log = new StringBuilder();
            log.Append("Slash Command Invoked: ").Append(args.Context.Interaction.Data.Name).Append(' ');
            if (commandOptions is not null)
                await BuildSlashCommandLogAsync(log, commandOptions);
            this._logger.LogInformation(log.ToString());
        }
    }
}
