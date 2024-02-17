// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.LogCleanup.Commands;
using Grimoire.Core.Features.LogCleanup.Queries;
using Grimoire.Core.Features.MessageLogging.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Discord.LoggingModule;

internal sealed class LogBackgroundTasks(IServiceProvider serviceProvider, ILogger<LogBackgroundTasks> logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromMinutes(1))
{
    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var discordClientService = serviceProvider.GetRequiredService<IDiscordClientService>();
        var oldLogMessages = await mediator.Send(new GetOldLogMessages.Query(), stoppingToken);

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
                .SelectAwait(async messageId => await DeleteMessageAsync(channelInfo.DiscordChannel, messageId, stoppingToken))
                ).ToArrayAsync(stoppingToken);

        await mediator.Send(new DeleteOldMessagesCommand(), stoppingToken);
        if (result is not null)
            await mediator.Send(new DeleteOldLogMessages.Command { DeletedOldLogMessageIds = result }, stoppingToken);
    }

    private static async Task<DeleteMessageResult> DeleteMessageAsync(DiscordChannel? channel, ulong messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (channel is null)
                return new DeleteMessageResult
                {
                    WasSuccessful = false,
                    MessageId = messageId
                };
            var message =  await channel.GetMessageAsync(messageId).WaitAsync(cancellationToken);
            await message.DeleteAsync().WaitAsync(cancellationToken);
            return new DeleteMessageResult
            {
                WasSuccessful = true,
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
