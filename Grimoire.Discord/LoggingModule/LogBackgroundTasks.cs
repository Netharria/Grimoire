// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Core.Features.Logging.Commands.DeleteOldLogMessages;
using Grimoire.Core.Features.Logging.Commands.MessageLoggingCommands.DeleteOldMessages;
using Grimoire.Core.Features.Logging.Queries.GetOldLogMessages;

namespace Grimoire.Discord.LoggingModule;

public class LogBackgroundTasks : INotificationHandler<TimedNotification>
{
    private readonly IMediator _mediator;
    private readonly IDiscordClientService _discordClientService;

    public LogBackgroundTasks(IMediator mediator, IDiscordClientService discordClientService)
    {
        this._mediator = mediator;
        this._discordClientService = discordClientService;
    }

    public async ValueTask Handle(TimedNotification notification, CancellationToken cancellationToken)
    {
        if (notification.Time.Second % 60 != 0)
            return;

        var oldLogMessages = await this._mediator.Send(new GetOldLogMessagesQuery(), cancellationToken);


         var resultTask = oldLogMessages
            .Select(channel =>
                new
                {
                    DiscordChannel = this.GetChannelAsync(channel.GuildId, channel.ChannelId),
                    DatabaseChannel = channel
                })
            .SelectMany(channelInfo =>
                channelInfo.DatabaseChannel.MessageIds
                .Select(messageId => DeleteMessageAsync(channelInfo.DiscordChannel, messageId))
                ).ToArray();

        var result = await Task.WhenAll(resultTask);

        await this._mediator.Send(new DeleteOldMessagesCommand(), cancellationToken);
        if (result is not null)
            await this._mediator.Send(new DeleteOldLogMessagesCommand { DeletedOldLogMessageIds = result }, cancellationToken);
    }

    private static async Task<DeleteMessageResult> DeleteMessageAsync(DiscordChannel? channel, ulong messageId)
    {
        try
        {
            if (channel is null)
                return new DeleteMessageResult
                {
                    WasSuccessful = false,
                    MessageId = messageId
                };
            var message =  await channel.GetMessageAsync(messageId);
            await message.DeleteAsync();
            return new DeleteMessageResult
            {
                WasSuccessful = true,
                MessageId = messageId
            };
        }
        catch (NotFoundException)
        {
            return new DeleteMessageResult
            {
                WasSuccessful = false,
                MessageId = messageId
            };
        }
        catch (Exception)
        {
            return new DeleteMessageResult
            {
                WasSuccessful = false,
                MessageId = messageId
            };
        }
    }

    private DiscordChannel? GetChannelAsync(ulong guildId, ulong channelId)
    {
        try
        {
            return this._discordClientService.Client.Guilds.GetValueOrDefault(guildId)?.Channels.GetValueOrDefault(channelId);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
