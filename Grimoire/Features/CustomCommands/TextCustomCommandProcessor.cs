// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.CustomCommands;

public sealed partial class TextCustomCommandProcessor(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildIssueReporter issueReporter,
    ILogger<TextCustomCommandProcessor> logger)
    : IEventHandler<MessageCreatedEventArgs>
{
    private const string NotFoundCode = "text-command.not-found";
    private const string ModuleDisabledCode = "text-command.module-disabled";

    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs eventArgs) =>
        await Validation<MessageCreatedEventArgs>.Succeed(eventArgs)
            .Bind(ev => ev switch
            {
                {
                        Message.MessageType: DiscordMessageType.Default or DiscordMessageType.Reply,
                        Author: DiscordMember { IsBot: false } member
                    } =>
                    Validation<(DiscordMember Member, string Content)>.Succeed((member, ev.Message.Content)),
                _ => Validation<(DiscordMember Member, string Content)>.Fail(
                    new Error("text-command.invalid-event", "Not a valid command event"))
            })
            .Bind(x => !string.IsNullOrWhiteSpace(x.Content) && x.Content.StartsWith('!')
                                                             && x.Content[1..].Split(' ',
                                                                     StringSplitOptions.RemoveEmptyEntries) is
                                                                 { Length: > 0 } parsedArgs
                ? Validation<(DiscordMember Member, string[] Args)>.Succeed((x.Member, parsedArgs))
                : Validation<(DiscordMember Member, string[] Args)>.Fail(
                    new Error("text-command.not-a-command", "Message is not a command")))
            .Bind(x => CustomCommandName.Create(x.Args[0]).Map(name => (x.Member, Name: name, x.Args)))
            .ToResult()
            .BindAsync(x => FetchCommandAsync(x.Member, x.Name, x.Args))
            .TapAsync(cmd =>
                dbContextFactory.RecordCommandUsageAsync(cmd.Name, cmd.Member.GetGuildId(), cmd.Member.GetUserId()))
            .TapAsync(cmd => this.SendCommandResponseAsync(cmd, sender, eventArgs))
            .Match(
                _ => Task.CompletedTask,
                error => this.LogFailure(error, eventArgs));

    // Only lookups worth knowing about are logged. Most messages are not commands and fail earlier.
    private Task LogFailure(Error error, MessageCreatedEventArgs eventArgs)
    {
        switch (error.Code)
        {
            case NotFoundCode:
                LogCommandNotFound(logger, error.Target, eventArgs.Guild?.Id);
                break;
            case ModuleDisabledCode:
                LogModuleDisabled(logger, eventArgs.Guild?.Id);
                break;
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information,
        "Custom command {CommandName} not found or user not authorized (Guild {GuildId})")]
    private static partial void LogCommandNotFound(ILogger logger, string? commandName, ulong? guildId);

    [LoggerMessage(LogLevel.Debug, "Ignored text command because the Commands module is disabled (Guild {GuildId})")]
    private static partial void LogModuleDisabled(ILogger logger, ulong? guildId);

    [LoggerMessage(LogLevel.Warning,
        "Could not send custom command response {CommandName} (Guild {GuildId}, Channel {ChannelId}): missing permissions")]
    private static partial void LogSendForbidden(ILogger logger, string commandName, ulong guildId, ulong channelId);

    private async Task<Result<ParsedCommand>> FetchCommandAsync(DiscordMember member, CustomCommandName name,
        string[] args)
    {
        if (!await settingsModule.IsModuleEnabled(Module.Commands, member.GetGuildId()).GetOrElse(() => false))
            return Result<ParsedCommand>.Fail(new Error(ModuleDisabledCode, "The Commands module is disabled"));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var response = await dbContext.CustomCommands
            .AsNoTracking()
            .GetCustomCommandQuery(member.GetGuildId(), name)
            .FirstOrDefaultAsync();
        return response is null || !GetCustomCommand.IsUserAuthorized(member, response)
            ? Result<ParsedCommand>.Fail(new Error(NotFoundCode, "Command not found or not authorized", name.Value))
            : Result<ParsedCommand>.Ok(new ParsedCommand(response, member, name, args));
    }

    private static DiscordMessageBuilder BuildResponse(
        CustomCommand response,
        string[] args, SnowflakeObject? snowflake, ulong guildId)
    {
        var contentValue = response.Content.Value;
        var hasMention = contentValue.Contains("%Mention", StringComparison.OrdinalIgnoreCase);
        var rawMessage = string.Join(' ', args.Skip(hasMention ? 2 : 1));
        CommandOutputFormat outputFormat = response is EmbedCustomCommand embed
            ? new CommandOutputFormat.Embedded(embed.EmbedColor)
            : new CommandOutputFormat.Text();
        var content = GetCustomCommand.TruncateForDiscord(
            GetCustomCommand.ApplyMessage(
                GetCustomCommand.ApplyMention(contentValue, snowflake, guildId),
                rawMessage, guildId),
            outputFormat is CommandOutputFormat.Embedded
                ? GetCustomCommand.MaxEmbedDescriptionLength
                : GetCustomCommand.MaxMessageLength);
        return GetCustomCommand.BuildMessageResponse(content, outputFormat);
    }

    private async Task SendCommandResponseAsync(
        ParsedCommand cmd, DiscordClient sender, MessageCreatedEventArgs eventArgs)
    {
        var snowflake = cmd.Response.Content.Value.Contains("%Mention", StringComparison.OrdinalIgnoreCase)
                        && cmd.Args.Length > 1
            ? await ResolveSnowflake(cmd.Args[1], sender, eventArgs.Guild)
            : null;
        try
        {
            await eventArgs.Channel.SendMessageAsync(
                BuildResponse(cmd.Response, cmd.Args, snowflake, eventArgs.Guild.Id));
        }
        catch (UnauthorizedException)
        {
            // Expected when the bot cannot post in that channel: tell the moderators rather than raising an error.
            LogSendForbidden(logger, cmd.Name.Value, eventArgs.Guild.Id, eventArgs.Channel.Id);
            await issueReporter.ReportAsync(GuildIssueCatalog.CannotSendInChannel(
                new GuildId(eventArgs.Guild.Id), new ChannelId(eventArgs.Channel.Id)));
        }
    }

    private static async Task<SnowflakeObject?> ResolveSnowflake(string arg, DiscordClient sender, DiscordGuild guild)
    {
        var userIds = DiscordRegex.GetUserMentions(arg).ToArray();
        if (userIds.Length > 0) return await sender.GetUserOrDefaultAsync(new UserId(userIds[0]));
        var roleIds = DiscordRegex.GetRoleMentions(arg).ToArray();
        if (roleIds.Length > 0) return await guild.GetRoleOrDefaultAsync(new RoleId(roleIds[0]));
        return null;
    }

    private sealed record ParsedCommand(
        CustomCommand Response,
        DiscordMember Member,
        CustomCommandName Name,
        string[] Args);
}
