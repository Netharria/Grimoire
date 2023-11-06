// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Commands.TrackerCommands.RemoveExpiredTrackers;

public class RemoveExpiredTrackersCommandHandler : ICommandHandler<RemoveExpiredTrackersCommand, IEnumerable<RemoveExpiredTrackersCommandResponse>>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public RemoveExpiredTrackersCommandHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<IEnumerable<RemoveExpiredTrackersCommandResponse>> Handle(RemoveExpiredTrackersCommand command, CancellationToken cancellationToken)
    {
        var results = await this._grimoireDbContext.Trackers
            .Where(x => x.EndTime < DateTimeOffset.UtcNow)
            .Select(x => new
            {
                Tracker = x,
                ModerationLogId = x.Guild.ModChannelLog
            }).ToArrayAsync(cancellationToken: cancellationToken);
        if (results.Any())
        {
            this._grimoireDbContext.Trackers.RemoveRange(results.Select(x => x.Tracker));
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        }

        return results.Select(x => new RemoveExpiredTrackersCommandResponse
        {
            UserId = x.Tracker.UserId,
            GuildId = x.Tracker.GuildId,
            LogChannelId = x.ModerationLogId,
            TrackerChannelId = x.Tracker.LogChannelId
        });
    }
}
