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
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.CustomCommands;

public sealed partial class TextCustomCommandProcessor(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildIssueReporter issueReporter,
    CommandsExtension commands,
    ILogger<TextCustomCommandProcessor> logger)
    : IEventHandler<MessageCreatedEventArgs>
{
    /// <summary>Discord's "Cannot send an empty message" error.</summary>
    private const int EmptyMessageCode = 50006;

    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs eventArgs) =>
        await Success<Error, MessageCreatedEventArgs>(eventArgs)
            .Bind(ev => ev switch
            {
                {
                        Message.MessageType: DiscordMessageType.Default or DiscordMessageType.Reply,
                        Author: DiscordMember { IsBot: false } member
                    } =>
                    Success<Error, (DiscordMember Member, string Content)>((member, ev.Message.Content)),
                _ => new Invalid("Not a valid command event")
            })
            .Bind(x => !string.IsNullOrWhiteSpace(x.Content) && x.Content.StartsWith('!')
                                                             && x.Content[1..].Split(' ',
                                                                     StringSplitOptions.RemoveEmptyEntries) is
                                                                 { Length: > 0 } parsedArgs
                ? Success<Error, (DiscordMember Member, string[] Args)>((x.Member, parsedArgs))
                : new Invalid("Message is not a command"))
            .Bind(x => CustomCommandName.Create(x.Args[0]).Map(name => (x.Member, Name: name, x.Args)))
            // The bot's own command of that name runs instead, as a text command. Learn refuses these names, but a
            // command saved before that check, or before the bot command existed, must not answer as well (KI-85).
            .Bind(x => commands.IsBuiltInCommandName(x.Name.Value)
                ? new Invalid("A bot command has this name")
                : Success<Error, (DiscordMember Member, CustomCommandName Name, string[] Args)>(x))
            .ToFin()
            .BindAsync(x => FetchCommandAsync(x.Member, x.Name, x.Args))
            // A use counts once the reply is out, so a refused or empty reply isn't counted, and a failed count
            // can't stop the reply.
            .TapAsync(async cmd =>
            {
                if (await this.SendCommandResponseAsync(cmd, sender, eventArgs))
                    await dbContextFactory.RecordCommandUsageAsync(cmd.Name, cmd.Member.GetGuildId(), cmd.Member.GetUserId());
            })
            // Most messages are not commands and fail before the lookup; the lookups worth knowing about are logged
            // where they fail, in FetchCommandAsync.
            .Match(
                _ => Task.CompletedTask,
                _ => Task.CompletedTask);

    [LoggerMessage(LogLevel.Information,
        "Custom command {CommandName} not found or user not authorized (Guild {GuildId})")]
    private static partial void LogCommandNotFound(ILogger logger, string commandName, ulong guildId);

    [LoggerMessage(LogLevel.Debug, "Ignored text command because the Commands module is disabled (Guild {GuildId})")]
    private static partial void LogModuleDisabled(ILogger logger, ulong guildId);

    [LoggerMessage(LogLevel.Warning,
        "Could not send custom command response {CommandName} (Guild {GuildId}, Channel {ChannelId}): missing permissions")]
    private static partial void LogSendForbidden(ILogger logger, string commandName, ulong guildId, ulong channelId);

    [LoggerMessage(LogLevel.Warning,
        "Could not send embed custom command {CommandName} (Guild {GuildId}, Channel {ChannelId}): missing Embed Links")]
    private static partial void LogEmbedForbidden(ILogger logger, string commandName, ulong guildId, ulong channelId);

    private async Task<Fin<ParsedCommand>> FetchCommandAsync(DiscordMember member, CustomCommandName name,
        string[] args)
    {
        if (!await settingsModule.IsModuleEnabled(Module.Commands, member.GetGuildId()).OrDefaultDuringMigration(false))
        {
            LogModuleDisabled(logger, member.Guild.Id);
            return new Invalid("The Commands module is disabled");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var response = await dbContext.CustomCommands
            .GetCustomCommandQuery(member.GetGuildId(), name)
            .FirstOrDefaultAsync();
        if (response is null || !GetCustomCommand.IsUserAuthorized(member, response))
        {
            LogCommandNotFound(logger, name.Value, member.Guild.Id);
            return new NotFound("Command not found or not authorized");
        }

        return new ParsedCommand(response, member, name, args);
    }

    /// <returns>Whether the reply was sent.</returns>
    private async Task<bool> SendCommandResponseAsync(
        ParsedCommand cmd, DiscordClient sender, MessageCreatedEventArgs eventArgs)
    {
        var snowflake = cmd.Response.Content.Value.Contains("%Mention", StringComparison.OrdinalIgnoreCase)
                        && cmd.Args.Length > 1
            ? await ResolveSnowflake(cmd.Args[1], sender, eventArgs.Guild)
            : null;
        // The first word after the name is the mention only if it resolved to one; otherwise it's part of the message.
        var message = string.Join(' ', cmd.Args.Skip(snowflake is not null ? 2 : 1));
        if (GetCustomCommand.RenderContent(cmd.Response, snowflake, message, eventArgs.Guild.Id) is not { } content)
            return false;
        try
        {
            await eventArgs.Channel.SendMessageAsync(
                GetCustomCommand.BuildResponse(new DiscordMessageBuilder(), cmd.Response, content, snowflake,
                    eventArgs.Guild.Id));
            return true;
        }
        catch (UnauthorizedException)
        {
            // Expected when the bot cannot post in that channel: tell the moderators rather than raising an error.
            LogSendForbidden(logger, cmd.Name.Value, eventArgs.Guild.Id, eventArgs.Channel.Id);
            await issueReporter.ReportAsync(GuildIssueCatalog.CannotSendInChannel(
                new GuildId(eventArgs.Guild.Id), new ChannelId(eventArgs.Channel.Id)));
            return false;
        }
        catch (BadRequestException exception) when (exception.Code == EmptyMessageCode
                                                    && cmd.Response is EmbedCustomCommand)
        {
            // Without Embed Links, Discord drops the embed and refuses the then-empty message (KI-84).
            LogEmbedForbidden(logger, cmd.Name.Value, eventArgs.Guild.Id, eventArgs.Channel.Id);
            await issueReporter.ReportAsync(GuildIssueCatalog.CannotEmbedInChannel(
                new GuildId(eventArgs.Guild.Id), new ChannelId(eventArgs.Channel.Id)));
            return false;
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
