// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

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
        var trackerEndTime = DateTimeOffset.UtcNow + command.Duration;

        var result = await this._grimoireDbContext.Guilds
            .Where(x => x.Id == command.GuildId)
            .Select(x =>
            new
            {
                Tracker = x.Trackers.FirstOrDefault(y => y.UserId == command.UserId),
                x.ModChannelLog,
                MemberExist = x.Members.Any(y => y.UserId == command.UserId)
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
        if (result?.Tracker is null)
        {
            var local = this._grimoireDbContext.Trackers.Local
                .FirstOrDefault(x => x.UserId == command.UserId
                    && x.GuildId == command.GuildId);
            if (local is not null)
                this._grimoireDbContext.Entry(local).State = EntityState.Detached;
            if (result?.MemberExist is null || !result.MemberExist)
            {
                if (!await this._grimoireDbContext.Users.WhereIdIs(command.UserId).AnyAsync(cancellationToken: cancellationToken))
                    await this._grimoireDbContext.Users.AddAsync(new User
                    {
                        Id = command.UserId,
                    }, cancellationToken);
                await this._grimoireDbContext.Members.AddAsync(new Member
                {

                    UserId = command.UserId,
                    GuildId = command.GuildId,
                    XpHistory = new List<XpHistory>
                    {
                        new XpHistory
                        {
                            UserId = command.UserId,
                            GuildId = command.GuildId,
                            Xp = 0,
                            Type = XpHistoryType.Created,
                            TimeOut = DateTime.UtcNow
                        }
                    },
                }, cancellationToken);
            }

            await this._grimoireDbContext.Trackers.AddAsync(new Tracker
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                EndTime = trackerEndTime,
                LogChannelId = command.ChannelId,
                ModeratorId = command.ModeratorId
            }, cancellationToken);
        }
        else
        {
            result.Tracker.LogChannelId = command.ChannelId;
            result.Tracker.EndTime = trackerEndTime;
            result.Tracker.ModeratorId = command.ModeratorId;
            this._grimoireDbContext.Trackers.Update(result.Tracker);
        }
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return new AddTrackerCommandResponse
        {
            ModerationLogId = result?.ModChannelLog
        };
    }
}
