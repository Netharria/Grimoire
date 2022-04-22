// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Commands.TrackerCommands.AddTracker
{
    public class AddTrackerCommandHandler : IRequestHandler<AddTrackerCommand, AddTrackerCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public AddTrackerCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<AddTrackerCommandResponse> Handle(AddTrackerCommand request, CancellationToken cancellationToken)
        {
            var trackerEndTime = request.DurationType switch
            {
                DurationType.Minutes => DateTime.UtcNow.AddMinutes(request.DurationAmount),
                DurationType.Hours => DateTime.UtcNow.AddHours(request.DurationAmount),
                DurationType.Days => DateTime.UtcNow.AddDays(request.DurationAmount),
                _ => throw new NotImplementedException(),
            };

            var result = await _cybermancyDbContext.Trackers
                .Where(x => x.UserId == request.UserId && x.GuildId == request.GuildId)
                .Select(x => new { Tracker = x, x.Guild.ModChannelLog })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (result is null)
                return new AddTrackerCommandResponse { Success = false, Message = "Could not find that user." };
            if (result?.Tracker is null)
                await this._cybermancyDbContext.Trackers.AddAsync(new Tracker
                    {
                        UserId = request.UserId,
                        GuildId = request.GuildId,
                        EndTime = trackerEndTime,
                        LogChannelId = request.ChannelId,
                        ModeratorId = request.ModeratorId
                    }, cancellationToken);
            else
            {
                result.Tracker.LogChannelId = request.ChannelId;
                result.Tracker.EndTime = trackerEndTime;
                result.Tracker.ModeratorId = request.ModeratorId;
            }
            await _cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return new AddTrackerCommandResponse
            {
                Success = true,
                ModerationLogId = result?.ModChannelLog
            };
        }
    }
}
