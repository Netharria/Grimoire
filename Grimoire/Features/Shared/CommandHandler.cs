// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using DSharpPlus.Commands.EventArgs;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Exceptions;
using EntityFramework.Exceptions.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared;


//todo: convert these to static methods if DI is not possible
public sealed partial class CommandHandler : IClientErrorHandler
{

    private static void BuildCommandLogAsync(StringBuilder builder,
        IReadOnlyDictionary<CommandParameter, object?> commandParameters)
    {
        foreach (var commandParameter in commandParameters)
            builder.Append(commandParameter.Key.Name).Append(' ')
                .Append('\'').Append(commandParameter.Value).Append("' ");
    }

    private static async Task SendErrorLogToLogChannel(DiscordClient client, string action, Exception exception,
        string? errorId = "")
    {
        if (action.Equals("COMMAND_ERRORED") && exception is NullReferenceException)
            return;
        if (exception is UniqueConstraintException)
            return;
        var configuration = client.ServiceProvider.GetRequiredService<IConfiguration>();
        if (!ulong.TryParse(configuration.GetSection("channelId").Value, out var channelId))
            return;
        var channel = await client.GetChannelOrDefaultAsync(channelId);
        if (channel is not null)
        {
            var errorIdString = string.IsNullOrWhiteSpace(errorId) ? string.Empty : $"[Id `{errorId}`]";
            var shortStackTrace = string.Empty;
            if (exception.StackTrace is not null)
                shortStackTrace = string.Join('\n', exception.StackTrace.Split('\n')
                    .Where(x => x.StartsWith("   at Grimoire", StringComparison.OrdinalIgnoreCase))
                    .Select(x => x[(x.IndexOf(" in ", StringComparison.OrdinalIgnoreCase) + 4)..])
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
    public async ValueTask HandleEventHandlerError(string name, Exception exception, Delegate invokedDelegate, object sender,
        object args) => await SendErrorLogToLogChannel((DiscordClient) sender, name, exception);

    public ValueTask HandleGatewayError(Exception exception) => ValueTask.CompletedTask;

    public static async Task HandleEventAsync(DiscordClient sender, CommandErroredEventArgs args)
    {
        if (args.Exception is AnticipatedException)
            await SendOrEditMessageAsync(args, new DiscordEmbedBuilder()
                .WithColor(GrimoireColor.Yellow)
                .WithDescription(args.Exception.Message));
        else if (args.Exception is UnauthorizedException)
            await SendOrEditMessageAsync(args, new DiscordEmbedBuilder()
                .WithColor(GrimoireColor.Yellow)
                .WithDescription(
                    $"{args.Context.Client.CurrentUser.Mention} does not have the permissions needed to complete this request."));
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        else if (args.Exception is not null)
        {
            var errorHexString = RandomNumberGenerator.GetHexString(10);
            var commandOptions = args.Context.Arguments;
            var log = new StringBuilder();
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (commandOptions is not null)
                BuildCommandLogAsync(log.Append(' '), commandOptions);
            LogCommandError(sender.Logger,
                args.Exception,
                errorHexString,
                args.Context.Command.Name,
                log.ToString());

            await SendOrEditMessageAsync(args, new DiscordEmbedBuilder()
                .WithColor(GrimoireColor.Yellow)
                .WithDescription(
                    $"Encountered exception while executing {args.Context.Command.Name} [ID {errorHexString}]"));
            await SendErrorLogToLogChannel(sender, args.Context.Command.Name, args.Exception,
                errorHexString);
        }
    }

    private static async Task SendOrEditMessageAsync(CommandErroredEventArgs args, DiscordEmbedBuilder embed)
    {
        if(args.Context.FollowupMessages.Count > 0)
            await args.Context.EditResponseAsync(embed);
        else
            if(args.Context is SlashCommandContext slashContext)
                await slashContext.RespondAsync(embed, true);
            else
                await args.Context.RespondAsync(embed);

    }

    [LoggerMessage(LogLevel.Error, "Error on Command: [ID {ErrorId}] {InteractionName}{InteractionOptions}")]
    static partial void LogCommandError(ILogger logger, Exception ex, string errorId,
        string interactionName, string interactionOptions);

    public static Task HandleEventAsync(DiscordClient sender, CommandExecutedEventArgs args)
    {
        var commandOptions = args.Context.Arguments;
        var log = new StringBuilder();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (commandOptions.Count > 0)
            BuildCommandLogAsync(log.Append(' '), commandOptions);
        LogCommandInvoked(sender.Logger,
            args.Context.Command.Name,
            log.ToString());
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Slash Command Invoked: {InteractionName}{InteractionOptions}")]
    static partial void LogCommandInvoked(ILogger logger, string interactionName, string interactionOptions);

}
