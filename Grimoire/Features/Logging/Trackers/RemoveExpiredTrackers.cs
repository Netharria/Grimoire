// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Logging.Trackers;

public sealed class RemoveExpiredTrackers
{
    internal sealed class BackgroundTask(IServiceProvider serviceProvider, ILogger<BackgroundTask> logger)
        : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromSeconds(5))
    {
        protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken stoppingToken)
        {
            var mediator = serviceProvider.GetRequiredService<IMediator>();
            var discord = serviceProvider.GetRequiredService<DiscordClient>();
            var response = await mediator.Send(new Request(), stoppingToken);
            foreach (var expiredTracker in response)
            {
                var embed = new DiscordEmbedBuilder()
                    .WithDescription($"Tracker on {UserExtensions.Mention(expiredTracker.UserId)} has expired.");
                await discord.SendMessageToLoggingChannel(expiredTracker.TrackerChannelId, builder =>
                {
                    builder.AddEmbed(embed);
                });

                await discord.SendMessageToLoggingChannel(expiredTracker.LogChannelId, builder =>
                {
                    builder.AddEmbed(embed);
                });
            }
        }
    }

    public sealed record Request : IRequest<IEnumerable<Response>>;

    [UsedImplicitly]
    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, IEnumerable<Response>>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<IEnumerable<Response>> Handle(Request command,
            CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var results = await dbContext.Trackers
                .Where(x => x.EndTime < DateTimeOffset.UtcNow)
                .Select(x => new { Tracker = x, ModerationLogId = x.Guild.ModChannelLog })
                .ToArrayAsync(cancellationToken);

            var response = results.Select(x => new Response
            {
                UserId = x.Tracker.UserId,
                LogChannelId = x.ModerationLogId,
                TrackerChannelId = x.Tracker.LogChannelId
            });
            if (results.Length == 0)
                return response;

            dbContext.Trackers.RemoveRange(results.Select(x => x.Tracker));
            await dbContext.SaveChangesAsync(cancellationToken);

            return response;
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong UserId { get; init; }
        public ulong TrackerChannelId { get; init; }
    }
}
