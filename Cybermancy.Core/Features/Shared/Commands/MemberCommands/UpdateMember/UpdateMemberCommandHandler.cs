// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Shared.Commands.MemberCommands.UpdateMember
{
    public class UpdateMemberCommandHandler : ICommandHandler<UpdateMemberCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public UpdateMemberCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<Unit> Handle(UpdateMemberCommand request, CancellationToken cancellationToken)
        {
            var currentNickname = await this._cybermancyDbContext.NicknameHistory
                .WhereMemberHasId(request.UserId, request.GuildId)
                .OrderByDescending(x => x.Timestamp)
                .Select(x => x.Nickname)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(request.Nickname)
                || string.IsNullOrWhiteSpace(currentNickname)
                || currentNickname.Equals(request.Nickname, StringComparison.Ordinal))
                return Unit.Value;
            await this._cybermancyDbContext.NicknameHistory.AddAsync(
                new NicknameHistory
                {
                    GuildId = request.GuildId,
                    UserId = request.UserId,
                    Nickname = request.Nickname
                }, cancellationToken);
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}
