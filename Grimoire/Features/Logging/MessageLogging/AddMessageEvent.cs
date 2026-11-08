// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using EntityFramework.Exceptions.Common;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Logging.MessageLogging;

public sealed partial class AddMessageEvent(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    ILogger<AddMessageEvent> logger) : IEventHandler<MessageCreatedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<AddMessageEvent> _logger = logger;
    private readonly SettingsModule _settingsModule = settingsModule;

    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs args)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        // The bot's own posts are mostly logs. Saving them would log their purge as a delete, and that log would be
        // purged and logged in turn, forever.
        if (args.Guild is null
            || args.Author.Id == sender.CurrentUser.Id
            || args.Message.MessageType is not DiscordMessageType.Default and not DiscordMessageType.Reply)
            return;

        if (!await this._settingsModule.ShouldLogMessage(
                args.GetChannelId(),
                args.Guild.GetGuildId(),
                args.Channel.BuildChannelTree().ToDictionary()).GetOrElse(() => false))
            return;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        var message = new Message
        {
            Id = args.GetMessageId(),
            UserId = args.GetAuthorUserId(),
            CreatedTimestamp = args.Message.CreationTimestamp,
            Attachments =
            [
                .. args.Message.Attachments
                    .Where(x => !string.IsNullOrWhiteSpace(x.Url))
                    .Select(x => new Attachment
                    {
                        Id = new AttachmentId(x.Id),
                        MessageId = new MessageId(args.Message.Id),
                        // The full signed URL: the delete log downloads the file from it and re-uploads it.
                        AttachmentUrl = AttachmentUrl.Create(x.Url!)
                    })
            ],
            ChannelId = args.GetChannelId(),
            ReferencedMessageId = args.Message.ReferencedMessage?.GetMessageId(),
            GuildId = args.Guild.GetGuildId(),
            MessageHistory =
            [
                new MessageCreatedEntry
                {
                    MessageId = args.GetMessageId(),
                    GuildId = args.Guild.GetGuildId(),
                    Content = args.Message.GetMessageContent(),
                    Timestamp = args.Message.CreationTimestamp
                }
            ]
        };

        await dbContext.Messages.AddAsync(message);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        // A message already saved, as when the gateway replays events after a resume. Nothing is lost.
        catch (UniqueConstraintException)
        {
            LogNonUniqueMessage(this._logger);
        }
        // Rethrown, so the event handler raises the alert a lost message deserves.
        catch (Exception ex)
        {
            LogOriginalMessageForDebugging(this._logger, args.Message.Content, ex);
            throw;
        }
    }

    [LoggerMessage(LogLevel.Debug, "Message was already saved.")]
    static partial void LogNonUniqueMessage(ILogger logger);

    [LoggerMessage(LogLevel.Error,
        "Database threw exception on message creation. This was the original message {message}")]
    static partial void LogOriginalMessageForDebugging(ILogger logger, string message, Exception ex);
}
