// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Exceptions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Commands.TrackerCommands.RemoveTracker
{
    public class RemoveTrackerCommandHandler : ICommandHandler<RemoveTrackerCommand, RemoveTrackerCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public RemoveTrackerCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<RemoveTrackerCommandResponse> Handle(RemoveTrackerCommand command, CancellationToken cancellationToken)
        {
            var result = await this._cybermancyDbContext.Trackers
                .Where(x => x.UserId == command.UserId && x.GuildId == command.GuildId)
                .Select(x => new
                {
                    Tracker = x,
                    ModerationLogId = x.Guild.ModChannelLog,
                    TrackerChannelId = x.LogChannelId
                }).FirstOrDefaultAsync(cancellationToken);
            if (result is null)
                throw new AnticipatedException("Could not find a tracker for that user.");
            if (result.Tracker is not null)
            {
                this._cybermancyDbContext.Trackers.Remove(result.Tracker);
                await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            }
            return new RemoveTrackerCommandResponse
            {
                ModerationLogId = result.ModerationLogId,
                TrackerChannelId = result.TrackerChannelId
            };
        }
    }
}
