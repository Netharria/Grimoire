// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Cybermancy.Discord.Extensions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using DSharpPlus.SlashCommands.EventArgs;
using Microsoft.Extensions.Logging;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting.Attributes;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting.Events;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Configuration;
using Cybermancy.Discord.Structs;
using Cybermancy.Discord.Attributes;
using Cybermancy.Core.Exceptions;

namespace Cybermancy.Discord
{
    [DiscordSlashCommandsEventsSubscriber]
    [DiscordClientErroredEventSubscriber]
    public class SlashCommandHandler : IDiscordSlashCommandsEventsSubscriber, IDiscordClientErroredEventSubscriber
    {
        private readonly ILogger<SlashCommandHandler> _logger;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="SlashCommandHandler"/> class.
        /// </summary>
        /// <param name="logger"></param>
        public SlashCommandHandler(ILogger<SlashCommandHandler> logger, IConfiguration configuration)
        {
            this._logger = logger;
            this._configuration = configuration;
        }

        private static async Task<StringBuilder> BuildSlashCommandLogAsync(StringBuilder builder, IEnumerable<DiscordInteractionDataOption> commandOptions)
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

        private async Task SendErrorLogToLogChannel(DiscordClient client, string action, Exception exception, string? errorId = "")
        {
            if (!ulong.TryParse(this._configuration.GetSection("channelId").Value, out var channelId))
                return;
            var channel = await client.GetChannelOrDefaultAsync(channelId);
            if (channel is not null)
            {
                var errorIdString = string.IsNullOrWhiteSpace(errorId) ? string.Empty : $"[Id `{errorId}`]";
                var shortStackTrace = string.Empty;
                if(exception.StackTrace is not null)
                    shortStackTrace = string.Join('\n', exception.StackTrace.Split('\n')
                        .Where(x => x.StartsWith("   at Cybermancy"))
                        .Select(x => x[(x.IndexOf(" in ") + 4)..])
                        .Select(x => '\"' + x.Replace(":line", "\" line")));
                await channel.SendMessageAsync($"Encountered exception while executing {action} {errorIdString}\n" +
                    $"```csharp\n{exception.Message}\n{shortStackTrace}\n```");
            }
                
        }

        public async Task DiscordOnClientErrored(DiscordClient sender, ClientErrorEventArgs args)
        {
            await this.SendErrorLogToLogChannel(sender, args.EventName, args.Exception);
            args.Handled = true;
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

                    if (check is SlashRequireModuleEnabledAttribute requireEnabledPermissions)
                        await args.Context.ReplyAsync(color: CybermancyColor.Green, message: $"The {requireEnabledPermissions.Module} module is not enabled.");
                }
            else if (args.Exception is AnticipatedException)
            {
                await args.Context.ReplyAsync(color: CybermancyColor.Orange, message: args.Exception.Message);
                args.Handled = true;
            }
            else if (args.Exception is not null)
            {
                var errorUlong = args.Context.User.Id + args.Context.InteractionId;
                var errorBytes = BitConverter.GetBytes(errorUlong);
                var errorByteString = Convert.ToHexString(errorBytes, 0, 5);

                var commandOptions = args.Context.Interaction.Data.Options;
                var log = new StringBuilder();
                if (commandOptions is not null)
                    await BuildSlashCommandLogAsync(log.Append(' '), commandOptions);
                this._logger.LogError("Error on SlashCommand: [ID {ErrorId}] {InteractionName}{InteractionOptions}\n{Message}\n{StackTrace}",
                    errorByteString,
                    args.Context.Interaction.Data.Name,
                    log.ToString(),
                    args.Exception.Message,
                    args.Exception.StackTrace);

                
                await args.Context.ReplyAsync(color: CybermancyColor.Orange,
                    message: $"Encountered exception while executing {args.Context.Interaction.Data.Name} [ID {errorByteString}]");
                await this.SendErrorLogToLogChannel(sender.Client, args.Context.Interaction.Data.Name, args.Exception, errorByteString);
            }
            args.Handled = true;
        }

        public async Task SlashCommandsOnSlashCommandExecuted(SlashCommandsExtension sender, SlashCommandExecutedEventArgs args)
        {
            var commandOptions = args.Context.Interaction.Data.Options;
            var log = new StringBuilder();
            if (commandOptions is not null)
                await BuildSlashCommandLogAsync(log.Append(' '), commandOptions);
            this._logger.LogInformation("Slash Command Invoked: {InteractionName}{InteractionOptions}",
                args.Context.Interaction.Data.Name,
                log.ToString());
        }
    }
}
