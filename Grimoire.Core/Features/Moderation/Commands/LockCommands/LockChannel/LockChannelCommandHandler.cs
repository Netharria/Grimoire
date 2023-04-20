// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Commands.LockCommands.LockChannel
{
    public class LockChannelCommandHandler : ICommandHandler<LockChannelCommand, BaseResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public LockChannelCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<BaseResponse> Handle(LockChannelCommand command, CancellationToken cancellationToken)
        {
            var lockEndTime = command.DurationType.GetDateTimeOffset(command.DurationAmount);

            var result = await this._grimoireDbContext.Channels
                .Where(x => x.GuildId == command.GuildId)
                .Include(x => x.Lock)
                .Include(x => x.Guild.ModChannelLog)
                .FirstAsync(x => x.Id == command.ChannelId, cancellationToken);
            if (result is null)
                throw new AnticipatedException("Could not find that channel");

            if (result.Lock is not null)
            {
                result.Lock.ModeratorId = command.ModeratorId;
                result.Lock.EndTime = lockEndTime;
                result.Lock.Reason = command.Reason;
                this._grimoireDbContext.Channels.Update(result);
            }
            else
            {
                await this._grimoireDbContext.Locks.AddAsync(new Lock
                {
                    ChannelId = command.ChannelId,
                    GuildId = command.GuildId,
                    Reason = command.Reason,
                    EndTime = lockEndTime,
                    ModeratorId = command.GuildId,
                    PreviouslyAllowed = command.PreviouslyAllowed,
                    PreviouslyDenied = command.PreviouslyDenied
                }, cancellationToken);
            }
            return new BaseResponse
            {
                LogChannelId = result.Guild.ModChannelLog
            };
        }

    }
}
