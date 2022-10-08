// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Extensions;
using Cybermancy.Core.Responses;
using Cybermancy.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Leveling.Commands.ManageXpCommands.ReclaimUserXp
{
    public class ReclaimUserXpCommandHandler : IRequestHandler<ReclaimUserXpCommand, BaseResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public ReclaimUserXpCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<BaseResponse> Handle(ReclaimUserXpCommand request, CancellationToken cancellationToken)
        {
            var member = await this._cybermancyDbContext.Members
                .WhereMemberHasId(request.UserId, request.GuildId)
                .Select(x => new { Xp = x.XpHistory.Sum(x => x.Xp )})
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (member is null)
                return new BaseResponse
                {
                    Success = false,
                    Message = $"{UserExtensions.Mention(request.UserId)} was not found. Have they been on the server before?"
                };

            long xpToTake;
            if (request.XpToTake.Equals("All", StringComparison.CurrentCultureIgnoreCase))
                xpToTake = member.Xp;
            else if (request.XpToTake.Trim().StartsWith('-'))
                return new BaseResponse
                {
                    Success = false,
                    Message = "Xp needs to be a positive value."
                };
            else if (!long.TryParse(request.XpToTake.Trim(), out xpToTake))
                return new BaseResponse
                {
                    Success = false,
                    Message = "Xp needs to be a valid number."
                };
            if(member.Xp < xpToTake)
                xpToTake = member.Xp;
            await this._cybermancyDbContext.XpHistory.AddAsync(new XpHistory
                {
                    UserId = request.UserId,
                    GuildId = request.GuildId,
                    Xp = -xpToTake,
                    Type = XpHistoryType.Reclaimed,
                    AwarderId = request.ReclaimerId,
                    TimeOut = DateTimeOffset.UtcNow
                }, cancellationToken);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return new BaseResponse
            {
                Success = true
            };
        }
    }
}
