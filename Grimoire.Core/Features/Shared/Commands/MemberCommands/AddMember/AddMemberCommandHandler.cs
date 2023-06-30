// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Shared.Commands.MemberCommands.AddMember;

public class AddMemberCommandHandler : ICommandHandler<AddMemberCommand>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public AddMemberCommandHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<Unit> Handle(AddMemberCommand command, CancellationToken cancellationToken)
    {
        var userExists = await this._grimoireDbContext.Users.AnyAsync(x => x.Id == command.UserId, cancellationToken);
        var memberExists = await this._grimoireDbContext.Members.AnyAsync(x => x.UserId == command.UserId && x.GuildId == command.GuildId, cancellationToken);

        if (!userExists)
            await this._grimoireDbContext.Users.AddAsync(new User
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
                        TimeOut = DateTimeOffset.UtcNow
                    }
                },
                AvatarHistory = new List<Avatar>
                {
                    new Avatar
                    {
                        UserId = command.UserId,
                        GuildId = command.GuildId,
                        FileName = command.AvatarUrl,
                        Timestamp = DateTimeOffset.UtcNow
                    }
                },
                NicknamesHistory = new List<NicknameHistory>
                {
                    new NicknameHistory
                    {
                        UserId = command.UserId,
                        GuildId = command.GuildId,
                        Nickname = command.Nickname
                    }
                }
            };

            await this._grimoireDbContext.Members.AddAsync(member, cancellationToken);

        }
        if (!userExists || !memberExists)
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
