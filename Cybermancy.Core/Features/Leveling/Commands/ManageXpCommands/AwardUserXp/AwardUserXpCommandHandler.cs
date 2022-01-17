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

namespace Cybermancy.Core.Features.Leveling.Commands.AwardUserXp
{
    public class AwardUserXpCommandHandler : IRequestHandler<AwardUserXpCommand, BaseResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public AwardUserXpCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<BaseResponse> Handle(AwardUserXpCommand request, CancellationToken cancellationToken)
        {
            if (request.XpToAward < 0)
                return new BaseResponse { Success = false, Message = "Xp needs to be a positive value." };

            var guildUser = await this._cybermancyDbContext.GuildUsers
                .SingleOrDefaultAsync(x => x.UserId == request.UserId && x.GuildId == request.GuildId, cancellationToken: cancellationToken);

            if (guildUser is null)
                return new BaseResponse { Success = false,
                    Message = $"{UserExtensions.Mention(request.UserId)} was not found. Have they been on the server before?" };

            guildUser.GrantXp(this._cybermancyDbContext, (int)request.XpToAward);

            this._cybermancyDbContext.GuildUsers.Update(guildUser);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { Success = true };
        }
    }
}
