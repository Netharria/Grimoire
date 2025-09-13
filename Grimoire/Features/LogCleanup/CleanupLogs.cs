// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Grimoire.Features.LogCleanup.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.LogCleanup;

public sealed class CleanupLogs
{
    internal sealed class BackgroundTask(IServiceProvider serviceProvider, ILogger<BackgroundTask> logger)
        : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromMinutes(1))
    {
        protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken stoppingToken)
        {
            var mediator = serviceProvider.GetRequiredService<IMediator>();
            var discordClient = serviceProvider.GetRequiredService<DiscordClient>();
            var oldLogMessages = await mediator.CreateStream(new Query(), stoppingToken)
                .SelectAwait(async channel =>
                    new
                    {
                        DiscordChannel = await discordClient.GetChannelOrDefaultAsync(channel.ChannelId),
                        DatabaseChannel = channel
                    })
                .SelectMany(channelInfo =>
                    channelInfo.DatabaseChannel.MessageIds
                        .ToAsyncEnumerable()
                        .SelectAwait(async messageId =>
                            await DeleteMessageAsync(channelInfo.DiscordChannel, messageId, stoppingToken))
                ).ToArrayAsync(stoppingToken);

            await mediator.Send(new DeleteOldLogsCommand(), stoppingToken);
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
    }

    public sealed record Query : IStreamRequest<Response>
    {
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IStreamRequestHandler<Query, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async IAsyncEnumerable<Response> Handle(Query request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var oldDate = DateTime.UtcNow - TimeSpan.FromDays(30);
            await foreach (var item in dbContext.OldLogMessages
                               .Where(oldLogMessage => oldLogMessage.CreatedAt < oldDate)
                               .GroupBy(oldLogMessage => new { oldLogMessage.ChannelId })
                               .Select(oldLogMessages => new Response
                               {
                                   ChannelId = oldLogMessages.Key.ChannelId,
                                   MessageIds = oldLogMessages.Select(x => x.Id).ToArray()
                               }).AsAsyncEnumerable().WithCancellation(cancellationToken))
                yield return item;
        }
    }

    public sealed record Response
    {
        public ulong ChannelId { get; init; }
        public ulong[] MessageIds { get; init; } = [];
    }
}
