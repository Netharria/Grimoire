// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using DSharpPlus.Exceptions;
using DSharpPlus.SlashCommands.EventArgs;
using Grimoire.Core.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting.Attributes;
using Nefarius.DSharpPlus.SlashCommands.Extensions.Hosting.Events;

namespace Grimoire.Discord;

/// <summary>
/// Initializes a new instance of the <see cref="SlashCommandHandler"/> class.
/// </summary>
/// <param name="logger"></param>
[DiscordSlashCommandsEventsSubscriber]
[DiscordClientErroredEventSubscriber]
public sealed partial class SlashCommandHandler(ILogger<SlashCommandHandler> logger, IConfiguration configuration) : IDiscordSlashCommandsEventsSubscriber, IDiscordClientErroredEventSubscriber
{
    private readonly ILogger<SlashCommandHandler> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

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
        if (action.Equals("COMMAND_ERRORED") && exception is NullReferenceException)
            return;
        if (!ulong.TryParse(this._configuration.GetSection("channelId").Value, out var channelId))
            return;
        var channel = await client.GetChannelOrDefaultAsync(channelId);
        if (channel is not null)
        {
            var errorIdString = string.IsNullOrWhiteSpace(errorId) ? string.Empty : $"[Id `{errorId}`]";
            var shortStackTrace = string.Empty;
            if (exception.StackTrace is not null)
                shortStackTrace = string.Join('\n', exception.StackTrace.Split('\n')
                    .Where(x => x.StartsWith("   at Grimoire"))
                    .Select(x => x[(x.IndexOf(" in ") + 4)..])
                    .Select(x => '\"' + x.Replace(":line", "\" line")));
            var innerException = exception.InnerException;
            var exceptionMessage = new StringBuilder().AppendLine(exception.Message);
            while (innerException is not null)
            {
                exceptionMessage.AppendLine(innerException.Message);
                innerException = innerException.InnerException;
            }
            await channel.SendMessageAsync($"Encountered exception while executing {action} {errorIdString}\n" +
                $"```csharp\n{exceptionMessage}\n{shortStackTrace}\n```");
        }

    }

    public async Task DiscordOnClientErrored(DiscordClient sender, ClientErrorEventArgs args)
        => await this.SendErrorLogToLogChannel(sender, args.EventName, args.Exception);

    public Task SlashCommandsOnContextMenuErrored(SlashCommandsExtension sender, ContextMenuErrorEventArgs args) => Task.CompletedTask;

    public Task SlashCommandsOnContextMenuExecuted(SlashCommandsExtension sender, ContextMenuExecutedEventArgs args) => Task.CompletedTask;

    public async Task SlashCommandsOnSlashCommandErrored(SlashCommandsExtension sender, SlashCommandErrorEventArgs args)
    {
        if (args.Exception is SlashExecutionChecksFailedException ex)
        {

            foreach (var check in ex.FailedChecks)
            {
                if (check is SlashRequireGuildAttribute)
                    await args.Context.CreateResponseAsync(new DiscordEmbedBuilder()
                        .WithColor(GrimoireColor.DarkPurple)
                        .WithDescription("You need to be in a server to use this command."), true);

                if (check is SlashRequirePermissionsAttribute requirePermissions)
                {
                    var value = Enum.ToObject(typeof(Permissions), requirePermissions.Permissions).ToString();
                    await args.Context.CreateResponseAsync(new DiscordEmbedBuilder()
                        .WithColor(GrimoireColor.DarkPurple)
                        .WithDescription($"You and {args.Context.Guild.CurrentMember.DisplayName} need {value} permissions to use this command."), true);
                }

                if (check is SlashRequireUserPermissionsAttribute requireUserPermissions)
                {
                    var value = Enum.ToObject(typeof(Permissions), requireUserPermissions.Permissions).ToString();
                    await args.Context.CreateResponseAsync(new DiscordEmbedBuilder()
                        .WithColor(GrimoireColor.DarkPurple)
                        .WithDescription($"You need {value} permissions to use this command."), true);
                }
                if (check is SlashRequireUserGuildPermissionsAttribute requireUserGuildPermissions)
                {
                    var value = Enum.ToObject(typeof(Permissions), requireUserGuildPermissions.Permissions).ToString();
                    await args.Context.CreateResponseAsync(new DiscordEmbedBuilder()
                        .WithColor(GrimoireColor.DarkPurple)
                        .WithDescription($"You need {value} server permissions to use this command."), true);
                }
                if (check is SlashRequireBotPermissionsAttribute requireBotPermissions)
                {
                    var value = Enum.ToObject(typeof(Permissions), requireBotPermissions.Permissions).ToString();
                    await args.Context.CreateResponseAsync(new DiscordEmbedBuilder()
                        .WithColor(GrimoireColor.DarkPurple)
                        .WithDescription($"{args.Context.Guild.CurrentMember.DisplayName} needs {value} permissions to use this command."), true);
                }

                if (check is SlashRequireOwnerAttribute)
                    await args.Context.CreateResponseAsync(new DiscordEmbedBuilder()
                        .WithColor(GrimoireColor.DarkPurple)
                        .WithDescription($"You need to be {args.Context.Guild.CurrentMember.DisplayName}'s owner to use this command"), true);

                if (check is SlashRequireDirectMessageAttribute)
                    await args.Context.CreateResponseAsync(new DiscordEmbedBuilder()
                        .WithColor(GrimoireColor.DarkPurple)
                        .WithDescription($"You need to DM {args.Context.Guild.CurrentMember.DisplayName} to use this command."), true);

                if (check is SlashRequireModuleEnabledAttribute requireEnabledPermissions)
                    await args.Context.CreateResponseAsync(new DiscordEmbedBuilder()
                        .WithColor(GrimoireColor.DarkPurple)
                        .WithDescription($"The {requireEnabledPermissions.Module} module is not enabled."), true);
            }
        }
        else if (args.Exception is AnticipatedException)
        {
            await SendOrEditMessageAsync(args, new DiscordEmbedBuilder()
                .WithColor(GrimoireColor.Yellow)
                .WithDescription(args.Exception.Message));
        }
        else if (args.Exception is UnauthorizedException)
        {
            await SendOrEditMessageAsync(args, new DiscordEmbedBuilder()
                .WithColor(GrimoireColor.Yellow)
                .WithDescription($"{args.Context.Client.CurrentUser.Mention} does not have the permissions needed to complete this request."));
        }
        else if (args.Exception is not null)
        {
            var errorHexString = RandomNumberGenerator.GetHexString(10);
            var commandOptions = args.Context.Interaction.Data.Options;
            var log = new StringBuilder();
            if (commandOptions is not null)
                await BuildSlashCommandLogAsync(log.Append(' '), commandOptions);
            LogSlashCommandError(_logger,
                args.Exception,
                errorHexString,
                args.Context.Interaction.Data.Name,
                log.ToString());

            await SendOrEditMessageAsync(args, new DiscordEmbedBuilder()
                .WithColor(GrimoireColor.Yellow)
                .WithDescription($"Encountered exception while executing {args.Context.Interaction.Data.Name} [ID {errorHexString}]"));
            await this.SendErrorLogToLogChannel(sender.Client, args.Context.Interaction.Data.Name, args.Exception, errorHexString);
        }
    }

    public async static Task SendOrEditMessageAsync(SlashCommandErrorEventArgs args, DiscordEmbedBuilder embed)
    {
        try
        {
            await args.Context.CreateResponseAsync(embed, true);
        }
        catch (Exception)
        {
            await args.Context.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
        }
    }

    [LoggerMessage(LogLevel.Error, "Error on SlashCommand: [ID {ErrorId}] {InteractionName}{InteractionOptions}")]
    public static partial void LogSlashCommandError(ILogger logger, Exception ex, string ErrorId, string interactionName, string interactionOptions);

    public async Task SlashCommandsOnSlashCommandExecuted(SlashCommandsExtension sender, SlashCommandExecutedEventArgs args)
    {
        var commandOptions = args.Context.Interaction.Data.Options;
        var log = new StringBuilder();
        if (commandOptions is not null)
            await BuildSlashCommandLogAsync(log.Append(' '), commandOptions);
        LogSlashCommandInvoked(_logger,
            args.Context.Interaction.Data.Name,
            log.ToString());
    }

    [LoggerMessage(LogLevel.Information, "Slash Command Invoked: {InteractionName}{InteractionOptions}")]
    public static partial void LogSlashCommandInvoked(ILogger logger, string interactionName, string interactionOptions);
}
