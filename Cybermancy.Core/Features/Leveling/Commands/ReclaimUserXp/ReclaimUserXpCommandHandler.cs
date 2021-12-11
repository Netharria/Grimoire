// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Extensions;
using Cybermancy.Core.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Leveling.Commands.ReclaimUserXp
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
            var guildUser = await this._cybermancyDbContext.GuildUsers
                .SingleOrDefaultAsync(x => x.UserId == request.UserId && x.GuildId == request.GuildId, cancellationToken: cancellationToken);
            if (guildUser is null)
                return new BaseResponse
                {
                    Success = false,
                    Message = $"{UserExtensions.Mention(request.UserId)} was not found. Have they been on the server before?"
                };

            int xpToTake;
            if (request.XpToTake.Equals("All", StringComparison.CurrentCultureIgnoreCase))
                xpToTake = guildUser.Xp;
            else if (!int.TryParse(request.XpToTake, out xpToTake))
                return new BaseResponse
                {
                    Success = false,
                    Message = "XP needs to be a valid number."
                };

            if (xpToTake < 0)
                return new BaseResponse
                {
                    Success = false,
                    Message = "XP needs to be a positive value."
                };
            guildUser.Xp -= xpToTake;
            this._cybermancyDbContext.GuildUsers.Update(guildUser);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return new BaseResponse
            {
                Success = true
            };
        }
    }
}
