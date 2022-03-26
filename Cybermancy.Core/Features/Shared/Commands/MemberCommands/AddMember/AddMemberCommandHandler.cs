// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Shared.Commands.MemberCommands.AddMember
{
    public class AddMemberCommandHandler : IRequestHandler<AddMemberCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public AddMemberCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<Unit> Handle(AddMemberCommand request, CancellationToken cancellationToken)
        {
            var userExists = await this._cybermancyDbContext.Users.AnyAsync(x => x.Id == request.UserId, cancellationToken);
            var guildUserExists = await this._cybermancyDbContext.GuildUsers.AnyAsync(x => x.UserId == request.UserId && x.GuildId == request.GuildId, cancellationToken);

            if (!userExists)
                await this._cybermancyDbContext.Users.AddAsync(new User
                    {
                        Id = request.UserId,
                        UsernameHistories = new List<UsernameHistory> {
                            new UsernameHistory {
                                NewUsername = request.UserName,
                                UserId = request.UserId
                            }
                        }
                    }, cancellationToken);

            if (!guildUserExists)
            {
                var guildUser = new GuildUser
                {
                    UserId = request.UserId,
                    GuildId = request.GuildId
                };
                if (!string.IsNullOrWhiteSpace(request.Nickname))
                {
                    guildUser.NicknamesHistory.Add(
                    new NicknameHistory
                    {
                        UserId = request.UserId,
                        GuildId = request.GuildId,
                        Nickname = request.Nickname
                    });
                }
                    
                await this._cybermancyDbContext.GuildUsers.AddAsync(guildUser, cancellationToken);

            }
            if(!userExists || !guildUserExists)
                await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}
