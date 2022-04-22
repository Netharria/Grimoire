// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Commands.TrackerCommands.RemoveTracker
{
    internal class RemoveTrackerCommandHandler : IRequestHandler<RemoveTrackerCommand, RemoveTrackerCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public RemoveTrackerCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<RemoveTrackerCommandResponse> Handle(RemoveTrackerCommand request, CancellationToken cancellationToken)
        {
            var result = await _cybermancyDbContext.Trackers
                .Where(x => x.UserId == request.UserId && x.GuildId == request.GuildId)
                .Select(x => new
                {
                    Tracker = x,
                    ModerationLogId = x.Guild.ModChannelLog,
                    TrackerChannelId = x.LogChannelId
                }).FirstOrDefaultAsync(cancellationToken);
            if (result is null)
                return new RemoveTrackerCommandResponse { Success = false, Message = "Could not find a tracker for that user." };
            if (result.Tracker is not null)
                _cybermancyDbContext.Trackers.Remove(result.Tracker);
            await _cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return new RemoveTrackerCommandResponse
            {
                Success = true,
                ModerationLogId = result.ModerationLogId,
                TrackerChannelId = result.TrackerChannelId
            };
        }
    }
}
