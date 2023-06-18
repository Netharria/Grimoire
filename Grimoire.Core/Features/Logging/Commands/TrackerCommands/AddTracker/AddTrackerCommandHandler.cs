// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.TrackerCommands.AddTracker;

public class AddTrackerCommandHandler : ICommandHandler<AddTrackerCommand, AddTrackerCommandResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public AddTrackerCommandHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<AddTrackerCommandResponse> Handle(AddTrackerCommand command, CancellationToken cancellationToken)
    {
        var trackerEndTime = command.DurationType.GetDateTimeOffset(command.DurationAmount);

        var result = await this._grimoireDbContext.Trackers
            .Where(x => x.UserId == command.UserId && x.GuildId == command.GuildId)
            .Select(x => new { Tracker = x, x.Guild.ModChannelLog })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
        if (result?.Tracker is null)
            await this._grimoireDbContext.Trackers.AddAsync(new Tracker
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
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return new AddTrackerCommandResponse
        {
            ModerationLogId = result?.ModChannelLog
        };
    }
}
