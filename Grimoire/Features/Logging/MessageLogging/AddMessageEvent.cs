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
        if (args.Guild is null
            || args.Message.MessageType is not DiscordMessageType.Default and not DiscordMessageType.Reply)
            return;

        var guild = args.Guild!;

        if (!await this._settingsModule.IsModuleEnabled(Module.MessageLog, guild.GetGuildId()))
            return;

        if (!await this._settingsModule.ShouldLogMessage(
                args.GetChannelId(),
                guild.GetGuildId(),
                args.Channel.BuildChannelTree().ToDictionary()))
            return;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();


        var message = new Message
        {
            Id = args.GetMessageId(),
            UserId = args.GetAuthorUserId(),
            Attachments = args.Message.Attachments
                .Where(x => !string.IsNullOrWhiteSpace(x.FileName))
                .Select(x =>
                    new Attachment { Id = new AttachmentId(x.Id), MessageId = new MessageId(args.Message.Id), FileName = x.FileName ?? string.Empty })
                .ToArray(),
            ChannelId = args.GetChannelId(),
            ReferencedMessageId = args.Message.ReferencedMessage?.GetMessageId(),
            GuildId = guild.GetGuildId(),
            MessageHistory =
            [
                new MessageHistory
                {
                    MessageId = args.GetMessageId(),
                    MessageContent = args.Message.GetMessageContent(),
                    GuildId = guild.GetGuildId(),
                    Action = MessageAction.Created
                }
            ]
        };

        await dbContext.Messages.AddAsync(message);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (UniqueConstraintException)
        {
            LogNonUniqueMessage(this._logger);
        }
        catch (Exception ex)
        {
            LogOriginalMessageForDebugging(this._logger, args.Message.Content, ex);
        }
    }

    [LoggerMessage(LogLevel.Error,
        "Was not able to save Message due to violating a unique constraint.")]
    static partial void LogNonUniqueMessage(ILogger logger);

    [LoggerMessage(LogLevel.Error,
        "Database threw exception on message creation. This was the original message {message}")]
    static partial void LogOriginalMessageForDebugging(ILogger logger, string message, Exception ex);
}
