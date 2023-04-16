// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Extensions;

namespace Cybermancy.Core.Features.Leveling.Commands.ManageXpCommands.ReclaimUserXp
{
    public class ReclaimUserXpCommandHandler : ICommandHandler<ReclaimUserXpCommand, Unit>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public ReclaimUserXpCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<Unit> Handle(ReclaimUserXpCommand command, CancellationToken cancellationToken)
        {
            var member = await this._cybermancyDbContext.Members
                .WhereMemberHasId(command.UserId, command.GuildId)
                .Select(x => new { Xp = x.XpHistory.Sum(x => x.Xp )})
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (member is null)
                throw new AnticipatedException($"{UserExtensions.Mention(command.UserId)} was not found. Have they been on the server before?");

            long xpToTake;
            if (command.XpToTake.Equals("All", StringComparison.CurrentCultureIgnoreCase))
                xpToTake = member.Xp;
            else if (command.XpToTake.Trim().StartsWith('-'))
                throw new AnticipatedException("Xp needs to be a positive value.");
            else if (!long.TryParse(command.XpToTake.Trim(), out xpToTake))
                throw new AnticipatedException("Xp needs to be a valid number.");
            if(member.Xp < xpToTake)
                xpToTake = member.Xp;
            await this._cybermancyDbContext.XpHistory.AddAsync(new XpHistory
                {
                    UserId = command.UserId,
                    GuildId = command.GuildId,
                    Xp = -xpToTake,
                    Type = XpHistoryType.Reclaimed,
                    AwarderId = command.ReclaimerId,
                    TimeOut = DateTimeOffset.UtcNow
                }, cancellationToken);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return new Unit();
        }
    }
}
