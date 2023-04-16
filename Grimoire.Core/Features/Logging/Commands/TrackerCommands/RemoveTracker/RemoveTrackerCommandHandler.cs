// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.TrackerCommands.RemoveTracker
{
    public class RemoveTrackerCommandHandler : ICommandHandler<RemoveTrackerCommand, RemoveTrackerCommandResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public RemoveTrackerCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<RemoveTrackerCommandResponse> Handle(RemoveTrackerCommand command, CancellationToken cancellationToken)
        {
            var result = await this._grimoireDbContext.Trackers
                .Where(x => x.UserId == command.UserId && x.GuildId == command.GuildId)
                .Select(x => new
                {
                    Tracker = x,
                    ModerationLogId = x.Guild.ModChannelLog,
                    TrackerChannelId = x.LogChannelId
                }).FirstOrDefaultAsync(cancellationToken);
            if (result is null || result.Tracker is null)
                throw new AnticipatedException("Could not find a tracker for that user.");

            this._grimoireDbContext.Trackers.Remove(result.Tracker);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            return new RemoveTrackerCommandResponse
            {
                ModerationLogId = result.ModerationLogId,
                TrackerChannelId = result.TrackerChannelId
            };
        }
    }
}
