// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Commands.LockCommands.UnlockChannelCommand
{
    public class UnlockChannelCommandHandler : ICommandHandler<UnlockChannelCommand, UnlockChannelCommandResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public UnlockChannelCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<UnlockChannelCommandResponse> Handle(UnlockChannelCommand command, CancellationToken cancellationToken)
        {
            var result = await this._grimoireDbContext.Locks
                .Where(x => x.ChannelId == command.ChannelId && x.GuildId == command.GuildId)
                .Select(x => new
                {
                    Lock = x,
                    ModerationLogId = x.Guild.ModChannelLog
                }).FirstOrDefaultAsync(cancellationToken);
            if (result is null || result.Lock is null)
                throw new AnticipatedException("Could not find a lock entry for that channel.");

            this._grimoireDbContext.Locks.Remove(result.Lock);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            return new UnlockChannelCommandResponse
            {
                LogChannelId = command.ChannelId,
                PreviouslyAllowed = result.Lock.PreviouslyAllowed,
                PreviouslyDenied = result.Lock.PreviouslyDenied
            };
        }
    }
}
