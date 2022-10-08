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
            var memberExists = await this._cybermancyDbContext.Members.AnyAsync(x => x.UserId == request.UserId && x.GuildId == request.GuildId, cancellationToken);

            if (!userExists)
                await this._cybermancyDbContext.Users.AddAsync(new User
                    {
                        Id = request.UserId,
                        UsernameHistories = new List<UsernameHistory> {
                            new UsernameHistory {
                                Username = request.UserName,
                                UserId = request.UserId
                            }
                        }
                    }, cancellationToken);

            if (!memberExists)
            {
                var member = new Member
                {
                    UserId = request.UserId,
                    GuildId = request.GuildId,
                    XpHistory = new List<XpHistory>
                    {
                        new XpHistory {
                            UserId = request.UserId,
                            GuildId = request.GuildId,
                            Type = XpHistoryType.Created,
                            Xp = 0,
                            TimeOut = DateTime.UtcNow
                        }
                    }
                    
                };
                if (!string.IsNullOrWhiteSpace(request.Nickname))
                {
                    member.NicknamesHistory.Add(
                    new NicknameHistory
                    {
                        UserId = request.UserId,
                        GuildId = request.GuildId,
                        Nickname = request.Nickname
                    });
                }
                    
                await this._cybermancyDbContext.Members.AddAsync(member, cancellationToken);

            }
            if(!userExists || !memberExists)
                await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}
