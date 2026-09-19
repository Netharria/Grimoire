// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using DSharpPlus.Commands;
using DSharpPlus.Commands.EventArgs;
using DSharpPlus.Commands.Exceptions;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Trees;
using Grimoire.Features.Shared.Alerts;
using DSharpPlus.Exceptions;
using EntityFramework.Exceptions.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared;

//todo: convert these to static methods if DI is not possible
public sealed partial class CommandHandler(IServiceProvider services, ILogger<CommandHandler> logger)
    : IClientErrorHandler
{
    // Resolved lazily: IAlertSender depends on DiscordClient, which depends on this handler.
    public ValueTask HandleEventHandlerError(string name, Exception exception, Delegate invokedDelegate,
        object sender,
        object args)
    {
        LogEventHandlerError(logger, exception, name);
        SendErrorAlert(services, name, exception);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleGatewayError(Exception exception)
    {
        LogGatewayError(logger, exception);
        services.GetRequiredService<IAlertSender>().Send(new Alert
        {
            Severity = AlertSeverity.Warning,
            Type = "GatewayError",
            Message = exception.Message,
            Exception = exception
        });
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(LogLevel.Error, "Unhandled exception in event handler {HandlerName}")]
    private static partial void LogEventHandlerError(ILogger logger, Exception exception, string handlerName);

    [LoggerMessage(LogLevel.Error, "Gateway error")]
    private static partial void LogGatewayError(ILogger logger, Exception exception);

    private static void BuildCommandLogAsync(StringBuilder builder,
        IReadOnlyDictionary<CommandParameter, object?> commandParameters)
    {
        foreach (var commandParameter in commandParameters)
            builder.Append(commandParameter.Key.Name).Append(' ')
                .Append('\'').Append(commandParameter.Value).Append("' ");
    }

    private static void SendErrorAlert(IServiceProvider services, string action, Exception exception,
        string? errorId = null, GuildId? guildId = null)
        => services.GetRequiredService<IAlertSender>().Send(new Alert
        {
            // Known noisy exception types are still recorded, but only in the daily report.
            Severity = exception is NullReferenceException or UniqueConstraintException
                ? AlertSeverity.Warning
                : AlertSeverity.Urgent,
            Type = "UnhandledException",
            Discriminator = action,
            Message = string.IsNullOrWhiteSpace(errorId)
                ? $"Encountered exception while executing {action}"
                : $"Encountered exception while executing {action} [Id `{errorId}`]",
            Exception = exception,
            GuildId = guildId
        });

    private static void SendWarning(DiscordClient client, string type, string command, string message, GuildId? guildId)
        => client.ServiceProvider.GetRequiredService<IAlertSender>().Send(new Alert
        {
            Severity = AlertSeverity.Warning,
            Type = type,
            Discriminator = command,
            Message = message,
            GuildId = guildId
        });

    public static async Task HandleEventAsync(DiscordClient sender, CommandErroredEventArgs args)
    {
        switch (args.Exception)
        {
            case UnauthorizedException:
                LogCommandOutcome(sender, args.Context, CommandOutcome.BotMissingPermissions);
                await SendOrEditMessageAsync(args, new DiscordEmbedBuilder()
                    .WithColor(GrimoireColor.Yellow)
                    .WithDescription(
                        $"{args.Context.Client.CurrentUser.Mention} does not have the permissions needed to complete this request."));
                return;
            case ChecksFailedException checksFailedException:
                LogCommandOutcome(sender, args.Context, CommandOutcome.CheckFailed);
                await SendOrEditMessageAsync(args, new DiscordEmbedBuilder()
                    .WithColor(GrimoireColor.Yellow)
                    .WithDescription(string.Join('\n',
                        checksFailedException
                            .Errors
                            .Select(error => error.ErrorMessage)
                            .Distinct()
                            .ToArray())));
                return;
            case ArgumentParseException argumentParseException:
                LogCommandOutcome(sender, args.Context, CommandOutcome.ParseError);
                await SendOrEditMessageAsync(args, new DiscordEmbedBuilder()
                    .WithColor(GrimoireColor.Yellow)
                    .WithDescription(argumentParseException.Message));
                return;
        }

        var errorHexString = RandomNumberGenerator.GetHexString(10);
        var commandOptions = args.Context.Arguments;
        var log = new StringBuilder();
        BuildCommandLogAsync(log.Append(' '), commandOptions);
        LogCommandError(sender.Logger,
            args.Exception,
            errorHexString,
            args.Context.Command.FullName,
            log.ToString(),
            args.Context.Guild?.Id,
            GetElapsedMilliseconds(args.Context));

        await SendOrEditMessageAsync(args, new DiscordEmbedBuilder()
            .WithColor(GrimoireColor.Yellow)
            .WithDescription(
                $"Encountered exception while executing {args.Context.Command.FullName} [ID {errorHexString}]"));
        SendErrorAlert(sender.ServiceProvider, args.Context.Command.FullName, args.Exception, errorHexString,
            args.Context.Guild is { } guild ? new GuildId(guild.Id) : null);
    }

    private static async Task SendOrEditMessageAsync(CommandErroredEventArgs args, DiscordEmbedBuilder embed)
    {
        if (args.Context.FollowupMessages.Count > 0)
            await args.Context.EditResponseAsync(embed);
        else if (args.Context is SlashCommandContext slashContext)
            await slashContext.RespondAsync(embed, true);
        else
            await args.Context.RespondAsync(embed);
    }

    [LoggerMessage(LogLevel.Error, "Error on Command: [ID {ErrorId}] {InteractionName}{InteractionOptions}")]
    static partial void LogCommandError(ILogger logger, Exception ex, string errorId,
        string interactionName, string interactionOptions, ulong? guildId, long durationMs);

    public static Task HandleEventAsync(DiscordClient sender, CommandExecutedEventArgs args)
    {
        LogCommandOutcome(sender, args.Context, CommandOutcome.Success);
        return Task.CompletedTask;
    }

    private static void LogCommandOutcome(DiscordClient sender, CommandContext context, CommandOutcome outcome)
    {
        var options = new StringBuilder();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (context.Arguments.Count > 0)
            BuildCommandLogAsync(options.Append(' '), context.Arguments);

        var durationMs = GetElapsedMilliseconds(context);
        var guildId = context.Guild?.Id;
        var command = context.Command.FullName;

        LogCommandFinished(sender.Logger, command, outcome, durationMs, guildId, options.ToString());
        if (durationMs > SlowCommandThresholdMs)
        {
            LogSlowCommand(sender.Logger, command, durationMs, guildId);
            SendWarning(sender, "SlowCommand", command, $"{command} took {durationMs}ms", guildId is { } id ? new GuildId(id) : null);
        }

        if (outcome is CommandOutcome.BotMissingPermissions)
            SendWarning(sender, "BotMissingPermissions", command, $"Bot lacks the permissions needed for {command}",
                guildId is { } guild ? new GuildId(guild) : null);
    }

    // Elapsed time since Discord created the interaction/message. Includes gateway delay, which is what the user waits for.
    private static long GetElapsedMilliseconds(CommandContext context)
    {
        var createdAt = context switch
        {
            SlashCommandContext slash => slash.Interaction.CreationTimestamp,
            TextCommandContext text => text.Message.CreationTimestamp,
            _ => (DateTimeOffset?)null
        };
        return createdAt is { } value ? (long)(DateTimeOffset.UtcNow - value).TotalMilliseconds : -1;
    }

    // Discord requires an initial interaction response within 3 seconds.
    private const long SlowCommandThresholdMs = 2000;

    [LoggerMessage(LogLevel.Information,
        "Command {Command} finished with {Outcome} in {DurationMs}ms (Guild {GuildId}){CommandOptions}")]
    static partial void LogCommandFinished(ILogger logger, string command, CommandOutcome outcome, long durationMs,
        ulong? guildId, string commandOptions);

    [LoggerMessage(LogLevel.Warning, "Slow command {Command} took {DurationMs}ms (Guild {GuildId})")]
    static partial void LogSlowCommand(ILogger logger, string command, long durationMs, ulong? guildId);
}

public enum CommandOutcome
{
    Success,
    CheckFailed,
    ParseError,
    BotMissingPermissions,
    Exception
}
