// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.Channels.TrackerLog;
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
            var guildLog = serviceProvider.GetRequiredService<GuildLog>();
            var trackerLog = serviceProvider.GetRequiredService<TrackerLog>();
            var response = await mediator.Send(new Request(), stoppingToken);
            foreach (var expiredTracker in response)
            {
                var user = await discord.GetUserOrDefaultAsync(expiredTracker.UserId);

                await trackerLog.SendTrackerMessageAsync(
                    new TrackerMessage
                    {
                        GuildId = expiredTracker.GuildId,
                        TrackerId = expiredTracker.TrackerChannelId,
                        TrackerIdType = TrackerIdType.ChannelId,
                        Description = $"Tracker on {user?.Mention} has expired."
                    }, stoppingToken);

                await guildLog.SendLogMessageAsync(
                    new GuildLogMessage
                    {
                        GuildId = expiredTracker.GuildId,
                        GuildLogType = GuildLogType.Moderation,
                        Description = $"Tracker on {user?.Mention} has expired."
                    }, stoppingToken);
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
                .ToArrayAsync(cancellationToken);

            var response = results.Select(x => new Response
            {
                UserId = x.UserId, GuildId = x.GuildId, TrackerChannelId = x.LogChannelId
            });
            if (results.Length == 0)
                return response;

            dbContext.Trackers.RemoveRange(results);
            await dbContext.SaveChangesAsync(cancellationToken);

            return response;
        }
    }

    public sealed record Response
    {
        public ulong UserId { get; init; }
        public GuildId GuildId { get; init; }
        public ulong TrackerChannelId { get; init; }
    }
}
