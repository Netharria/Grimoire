using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cybermancy.Core.Extensions;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.EventArgs;
using DSharpPlus.SlashCommands.Attributes;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting.Events;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting.Attributes;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;

namespace Cybermancy.Core
{
    [DiscordSlashCommandsEventsSubscriber]
    public class SlashCommandHandler : IDiscordSlashCommandsEventsSubscriber
    {
        private readonly ILogger<SlashCommandHandler> _logger;

        public SlashCommandHandler(ILogger<SlashCommandHandler> logger)
        {
            _logger = logger;
        }

        public Task SlashCommandsOnContextMenuErrored(SlashCommandsExtension sender, ContextMenuErrorEventArgs args)
        {
            return Task.CompletedTask;
        }

        public Task SlashCommandsOnContextMenuExecuted(SlashCommandsExtension sender, ContextMenuExecutedEventArgs args)
        {
            return Task.CompletedTask;
        }

        public async Task SlashCommandsOnSlashCommandErrored(SlashCommandsExtension sender, SlashCommandErrorEventArgs args)
        {
            if(args.Exception is SlashExecutionChecksFailedException ex)
            {
                foreach(var check in ex.FailedChecks)
                {
                    if (check is SlashRequireGuildAttribute)
                        await args.Context.Reply(color: Enums.CybermancyColor.Green, message: "You need to be in a server to use this command.");

                    if(check is SlashRequirePermissionsAttribute requirePermissions)
                    {
                        string value = Enum.ToObject(typeof(Permissions), requirePermissions.Permissions).ToString();
                        await args.Context.Reply(color: Enums.CybermancyColor.Green, message: $"You and {args.Context.Guild.CurrentMember.DisplayName} need {value} permissions to use this command.");
                    }
                        

                    if(check is SlashRequireUserPermissionsAttribute requireUserPermissions)
                    {
                        string value = Enum.ToObject(typeof(Permissions), requireUserPermissions.Permissions).ToString();
                        await args.Context.Reply(color: Enums.CybermancyColor.Green, message: $"You need {value} permissions to use this command.");
                    }
                        

                    if (check is SlashRequireBotPermissionsAttribute requireBotPermissions)
                    {
                        string value = Enum.ToObject(typeof(Permissions), requireBotPermissions.Permissions).ToString();
                        await args.Context.Reply(color: Enums.CybermancyColor.Green, message: $"{args.Context.Guild.CurrentMember.DisplayName} needs {value} permissions to use this command.");
                    }
                        

                    if(check is SlashRequireOwnerAttribute)
                        await args.Context.Reply(color: Enums.CybermancyColor.Green, message: $"You need to be {args.Context.Guild.CurrentMember.DisplayName}'s owner to use this command");

                    if(check is SlashRequireDirectMessageAttribute)
                        await args.Context.Reply(color: Enums.CybermancyColor.Green, message: $"You need to DM {args.Context.Guild.CurrentMember.DisplayName} to use this command.");
                }       
            }
            else if(args.Exception is not null)
            {
                var commandOptions = args.Context.Interaction.Data.Options;
                var log = new StringBuilder();
                log.Append($"Error on Slash Command: {args.Context.Interaction.Data.Name} ");
                if (commandOptions is not null)
                    await BuildSlashCommandLog(log, commandOptions);
                _logger.LogInformation(log.ToString());
            }
        }

        public async Task SlashCommandsOnSlashCommandExecuted(SlashCommandsExtension sender, SlashCommandExecutedEventArgs args)
        {
            var commandOptions = args.Context.Interaction.Data.Options;
            var log = new StringBuilder();
            log.Append($"Slash Command Invoked: {args.Context.Interaction.Data.Name} ");
            if (commandOptions is not null)
                await BuildSlashCommandLog(log, commandOptions);
            _logger.LogInformation(log.ToString());
        }

        public async static Task<StringBuilder> BuildSlashCommandLog(StringBuilder builder, IEnumerable<DiscordInteractionDataOption> commandOptions)
        {
            foreach (var option in commandOptions)
            {

                builder.Append($"{option.Name} ");
                if (option.Options is not null)
                    await BuildSlashCommandLog(builder, option.Options);
                if (option.Value is not null)
                    builder.Append($"'{option.Value}' ");
            }
            return builder;
        }
    }
}
