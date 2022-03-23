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

namespace Cybermancy.Core.Features.Shared.Commands.GuildCommands.AddGuild
{
    public class AddGuildCommandHandler : IRequestHandler<AddGuildCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public AddGuildCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<Unit> Handle(AddGuildCommand request, CancellationToken cancellationToken)
        {
            var usersToAdd = request.Users
                .ExceptBy(this._cybermancyDbContext.Users.Select(x => x.Id),
                x => x.Id)
                .Select(x => new User
                {
                    AvatarUrl = x.AvatarUrl,
                    Id = x.Id,
                    UserName = x.UserName,
                    UsernameHistories = new List<UsernameHistory> {
                        new UsernameHistory {
                            NewUsername = x.UserName,
                            UserId = x.Id,
                        }
                    }
                });

            if (usersToAdd.Any())
                await this._cybermancyDbContext.Users.AddRangeAsync(usersToAdd, cancellationToken);

            var guildExists = await this._cybermancyDbContext.Guilds.AnyAsync(x => x.Id == request.GuildId, cancellationToken: cancellationToken);
            if (!guildExists)

                await this._cybermancyDbContext.Guilds.AddAsync(
                    new Guild
                    {
                        Id = request.GuildId,
                        LevelSettings = new GuildLevelSettings(),
                        ModerationSettings = new GuildModerationSettings(),
                        LogSettings = new GuildLogSettings(),
                    }, cancellationToken);

            var rolesToAdd = request.Roles
                .ExceptBy(this._cybermancyDbContext.Roles.Select(x => x.Id),
                x => x.Id)
                .Select(x => new Role
                {
                    Id = x.Id,
                    GuildId = x.GuildId
                });

            if (rolesToAdd.Any())
                await this._cybermancyDbContext.Roles.AddRangeAsync(rolesToAdd, cancellationToken);

            var channelsToAdd = request.Channels
                .ExceptBy(this._cybermancyDbContext.Channels.Select(x => x.Id),
                x => x.Id)
                .Select(x => new Channel
                {
                    Id = x.Id,
                    GuildId = x.GuildId,
                    Name = x.Name,
                });

            if (channelsToAdd.Any())
                await this._cybermancyDbContext.Channels.AddRangeAsync(channelsToAdd, cancellationToken);

            var guildUsersToAdd = request.GuildUsers
                .ExceptBy(this._cybermancyDbContext.GuildUsers.Select(x => new { x.UserId, x.GuildId }),
                x => new { x.UserId, x.GuildId })
                .Select(x => new GuildUser
                {
                    UserId = x.UserId,
                    GuildId= x.GuildId,
                    GuildAvatarUrl = x.GuildAvatarUrl,
                    DisplayName = x.DisplayName,
                    NicknamesHistory = new List<NicknameHistory> {
                        new NicknameHistory{
                            GuildId = x.GuildId,
                            UserId = x.UserId,
                            NewNickname = x.Nickname
                        },
                    }
                });

            if (guildUsersToAdd.Any())
                await this._cybermancyDbContext.GuildUsers.AddRangeAsync(guildUsersToAdd, cancellationToken);

            if (usersToAdd.Any() || !guildExists || rolesToAdd.Any() || channelsToAdd.Any() || guildUsersToAdd.Any())
                await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
