// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Core.Features.Logging.Commands;
using Grimoire.Core.Features.Logging.Commands.MessageLoggingCommands;
using Grimoire.Core.Features.Logging.Queries;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Grimoire.Discord.LoggingModule;

public class LogBackgroundTasks(IServiceProvider serviceProvider, ILogger logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromMinutes(1))
{
    protected override async Task RunTask(CancellationToken stoppingToken)
    {
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var discordClientService = _serviceProvider.GetRequiredService<IDiscordClientService>();
        var oldLogMessages = await mediator.Send(new GetOldLogMessagesQuery(), stoppingToken);

        var result = await oldLogMessages
            .ToAsyncEnumerable()
            .Select(channel =>
                new
                {
                    DiscordChannel = GetChannel(discordClientService, channel.GuildId, channel.ChannelId),
                    DatabaseChannel = channel
                })
            .SelectMany(channelInfo =>
                channelInfo.DatabaseChannel.MessageIds
                .ToAsyncEnumerable()
                .SelectAwait(async messageId => await DeleteMessageAsync(channelInfo.DiscordChannel, messageId))
                ).ToArrayAsync(stoppingToken);

        await mediator.Send(new DeleteOldMessagesCommand(), stoppingToken);
        if (result is not null)
            await mediator.Send(new DeleteOldLogMessagesCommand { DeletedOldLogMessageIds = result }, stoppingToken);
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

    private static DiscordChannel? GetChannel(IDiscordClientService discordClientService, ulong guildId, ulong channelId)
    {
        try
        {
            return discordClientService.Client.Guilds.GetValueOrDefault(guildId)?.Channels.GetValueOrDefault(channelId);
        }
        catch (Exception)
        {
            return null;
        }
    }

}
