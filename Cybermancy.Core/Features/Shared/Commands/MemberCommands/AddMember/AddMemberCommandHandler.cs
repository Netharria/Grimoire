// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Shared.Commands.MemberCommands.AddMember
{
    public class AddMemberCommandHandler : ICommandHandler<AddMemberCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public AddMemberCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<Unit> Handle(AddMemberCommand command, CancellationToken cancellationToken)
        {
            var userExists = await this._cybermancyDbContext.Users.AnyAsync(x => x.Id == command.UserId, cancellationToken);
            var memberExists = await this._cybermancyDbContext.Members.AnyAsync(x => x.UserId == command.UserId && x.GuildId == command.GuildId, cancellationToken);

            if (!userExists)
                await this._cybermancyDbContext.Users.AddAsync(new User
                    {
                        Id = command.UserId,
                        UsernameHistories = new List<UsernameHistory> {
                            new UsernameHistory {
                                Username = command.UserName,
                                UserId = command.UserId
                            }
                        }
                    }, cancellationToken);

            if (!memberExists)
            {
                var member = new Member
                {
                    UserId = command.UserId,
                    GuildId = command.GuildId,
                    XpHistory = new List<XpHistory>
                    {
                        new XpHistory {
                            UserId = command.UserId,
                            GuildId = command.GuildId,
                            Type = XpHistoryType.Created,
                            Xp = 0,
                            TimeOut = DateTime.UtcNow
                        }
                    }
                    
                };
                if (!string.IsNullOrWhiteSpace(command.Nickname))
                {
                    member.NicknamesHistory.Add(
                    new NicknameHistory
                    {
                        UserId = command.UserId,
                        GuildId = command.GuildId,
                        Nickname = command.Nickname
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
