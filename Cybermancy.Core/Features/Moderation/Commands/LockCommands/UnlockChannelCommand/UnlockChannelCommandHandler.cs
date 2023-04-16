// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Core.Features.Moderation.Commands.LockCommands.UnlockChannelCommand
{
    public class UnlockChannelCommandHandler : ICommandHandler<UnlockChannelCommand, UnlockChannelCommandResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public UnlockChannelCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<UnlockChannelCommandResponse> Handle(UnlockChannelCommand command, CancellationToken cancellationToken)
        {
            var result = await _cybermancyDbContext.Locks
                .Where(x => x.ChannelId == command.ChannelId && x.GuildId == command.GuildId)
                .Select(x => new
                {
                    Lock = x,
                    ModerationLogId = x.Guild.ModChannelLog
                }).FirstOrDefaultAsync(cancellationToken);
            if (result is null || result.Lock is null)
                throw new AnticipatedException("Could not find a lock entry for that channel.");

            this._cybermancyDbContext.Locks.Remove(result.Lock);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return new UnlockChannelCommandResponse
            {
                LogChannelId = command.ChannelId,
                PreviouslyAllowed = result.Lock.PreviouslyAllowed,
                PreviouslyDenied = result.Lock.PreviouslyDenied
            };
        }
    }
}
