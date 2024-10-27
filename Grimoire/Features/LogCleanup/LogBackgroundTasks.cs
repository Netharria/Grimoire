// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.LogCleanup.Commands;
using Grimoire.Features.LogCleanup.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.LogCleanup;

internal sealed class LogBackgroundTasks(IServiceProvider serviceProvider, ILogger<LogBackgroundTasks> logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromMinutes(1))
{
    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var discordClient = serviceProvider.GetRequiredService<DiscordClient>();
        var oldLogMessages = await mediator.CreateStream(new GetOldLogMessages.Query(), stoppingToken)
            .Select(channel =>
                new
                {
                    DiscordChannel = GetChannel(discordClient, channel.GuildId, channel.ChannelId),
                    DatabaseChannel = channel
                })
            .SelectMany(channelInfo =>
                channelInfo.DatabaseChannel.MessageIds
                    .ToAsyncEnumerable()
                    .SelectAwait(async messageId =>
                        await DeleteMessageAsync(channelInfo.DiscordChannel, messageId, stoppingToken))
            ).ToArrayAsync(stoppingToken);

        await mediator.Send(new DeleteOldLogsCommand(), stoppingToken);
        if (oldLogMessages is not null)
            await mediator.Send(new DeleteOldLogMessages.Command { DeletedOldLogMessageIds = oldLogMessages },
                stoppingToken);
    }

    private static async Task<DeleteMessageResult> DeleteMessageAsync(DiscordChannel? channel, ulong messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (channel is null)
                return new DeleteMessageResult { WasSuccessful = false, MessageId = messageId };
            var message = await channel.GetMessageAsync(messageId).WaitAsync(cancellationToken);
            await message.DeleteAsync().WaitAsync(cancellationToken);
            return new DeleteMessageResult { WasSuccessful = true, MessageId = messageId };
        }
        catch (Exception)
        {
            return new DeleteMessageResult { WasSuccessful = false, MessageId = messageId };
        }
    }

    private static DiscordChannel? GetChannel(DiscordClient discordClient, ulong guildId, ulong channelId)
    {
        try
        {
            return discordClient.Guilds.GetValueOrDefault(guildId)?.Channels.GetValueOrDefault(channelId);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
