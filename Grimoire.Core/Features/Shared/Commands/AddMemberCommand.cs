// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Shared.Commands;

public sealed record AddMemberCommand : ICommand
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? Nickname { get; init; }
    public string AvatarUrl { get; set; } = string.Empty;
}


public sealed class AddMemberCommandHandler(GrimoireDbContext grimoireDbContext) : ICommandHandler<AddMemberCommand>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<Unit> Handle(AddMemberCommand command, CancellationToken cancellationToken)
    {
        var userResult = await this._grimoireDbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == command.UserId)
            .Select(x => new
            {
                x.UsernameHistories.OrderByDescending(username => username.Timestamp).First().Username,
            }).FirstOrDefaultAsync(cancellationToken);
        var memberResult = await this._grimoireDbContext.Members
            .AsNoTracking()
            .Where(x => x.UserId == command.UserId && x.GuildId == command.GuildId)
            .Select(x => new
            {
                x.NicknamesHistory.OrderByDescending(Nickname => Nickname.Timestamp).First().Nickname,
                x.AvatarHistory.OrderByDescending(avatar => avatar.Timestamp).First().FileName
            }).FirstOrDefaultAsync(cancellationToken);

        if (userResult is null)
            await this._grimoireDbContext.Users.AddAsync(new User
            {
                Id = command.UserId,
                UsernameHistories = new List<UsernameHistory> {
                        new() {
                            Username = command.UserName,
                            UserId = command.UserId
                        }
                    }
            }, cancellationToken);

        if (userResult is not null)
            if (!string.Equals(userResult.Username, command.UserName, StringComparison.CurrentCultureIgnoreCase))
                await this._grimoireDbContext.UsernameHistory.AddAsync(new UsernameHistory
                {
                    Username = command.UserName,
                    UserId = command.UserId
                }, cancellationToken);

        if (memberResult is null)
        {
            var member = new Member
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                XpHistory = new List<XpHistory>
                {
                    new() {
                        UserId = command.UserId,
                        GuildId = command.GuildId,
                        Type = XpHistoryType.Created,
                        Xp = 0,
                        TimeOut = DateTimeOffset.UtcNow
                    }
                },
                AvatarHistory = new List<Avatar>
                {
                    new() {
                        UserId = command.UserId,
                        GuildId = command.GuildId,
                        FileName = command.AvatarUrl
                    }
                },
                NicknamesHistory = new List<NicknameHistory>
                {
                    new() {
                        UserId = command.UserId,
                        GuildId = command.GuildId,
                        Nickname = command.Nickname
                    }
                }
            };

            await this._grimoireDbContext.Members.AddAsync(member, cancellationToken);
        }

        if (memberResult is not null)
        {
            if (!string.Equals(memberResult.Nickname, command.Nickname, StringComparison.CurrentCultureIgnoreCase))
                await this._grimoireDbContext.NicknameHistory.AddAsync(new NicknameHistory
                {
                    UserId = command.UserId,
                    GuildId = command.GuildId,
                    Nickname = command.Nickname
                }, cancellationToken);

            if (!string.Equals(memberResult.FileName, command.AvatarUrl, StringComparison.Ordinal))
                await this._grimoireDbContext.Avatars.AddAsync(new Avatar
                {
                    UserId = command.UserId,
                    GuildId = command.GuildId,
                    FileName = command.AvatarUrl
                }, cancellationToken);
        }
        if (userResult is null
            || memberResult is null
            || !string.Equals(userResult.Username, command.UserName, StringComparison.CurrentCultureIgnoreCase)
            || !string.Equals(memberResult.Nickname, command.Nickname, StringComparison.CurrentCultureIgnoreCase)
            || !string.Equals(memberResult.FileName, command.AvatarUrl, StringComparison.Ordinal))
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
