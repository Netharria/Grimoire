// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Commands.TrackerCommands.RemoveExpiredTrackers
{
    public class RemoveExpiredTrackersCommandHandler : ICommandHandler<RemoveExpiredTrackersCommand, RemoveExpiredTrackersCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public RemoveExpiredTrackersCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<RemoveExpiredTrackersCommandResponse> Handle(RemoveExpiredTrackersCommand request, CancellationToken cancellationToken)
        {
            var results = await this._cybermancyDbContext.Trackers.Where(x => x.EndTime < DateTime.UtcNow)
                .Select(x => new
                {
                    Tracker = x,
                    ModerationLogId = x.Guild.ModChannelLog,
                    TrackerChannelId = x.LogChannelId
                }).ToArrayAsync(cancellationToken: cancellationToken);
            if (results.Any())
            {
                this._cybermancyDbContext.Trackers.RemoveRange(results.Select(x => x.Tracker));
                await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            }
                
            return new RemoveExpiredTrackersCommandResponse
            {
                Success = true,
                ExpiredTrackers = results.Select(x => new ExpiredTracker
                {
                    UserId = x.Tracker.UserId,
                    GuildId = x.Tracker.GuildId,
                    ModerationLogId = x.ModerationLogId,
                    TrackerChannelId = x.TrackerChannelId
                }).ToArray()
            };
        }
    }
}
