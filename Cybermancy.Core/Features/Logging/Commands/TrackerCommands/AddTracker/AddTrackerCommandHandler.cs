// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Core.Features.Logging.Commands.TrackerCommands.AddTracker
{
    public class AddTrackerCommandHandler : ICommandHandler<AddTrackerCommand, AddTrackerCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public AddTrackerCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<AddTrackerCommandResponse> Handle(AddTrackerCommand command, CancellationToken cancellationToken)
        {
            var trackerEndTime = command.DurationType.GetDateTimeOffset(command.DurationAmount);

            var result = await this._cybermancyDbContext.Trackers
                .Where(x => x.UserId == command.UserId && x.GuildId == command.GuildId)
                .Select(x => new { Tracker = x, x.Guild.ModChannelLog })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (result is null)
                throw new AnticipatedException("Could not find that user.");
            if (result?.Tracker is null)
                await this._cybermancyDbContext.Trackers.AddAsync(new Tracker
                    {
                        UserId = command.UserId,
                        GuildId = command.GuildId,
                        EndTime = trackerEndTime,
                        LogChannelId = command.ChannelId,
                        ModeratorId = command.ModeratorId
                    }, cancellationToken);
            else
            {
                result.Tracker.LogChannelId = command.ChannelId;
                result.Tracker.EndTime = trackerEndTime;
                result.Tracker.ModeratorId = command.ModeratorId;
            }
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return new AddTrackerCommandResponse
            {
                ModerationLogId = result?.ModChannelLog
            };
        }
    }
}
