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

namespace Cybermancy.Core.Features.Shared.Commands.UpdateAllGuilds
{
    public class UpdateAllGuildsQueryHandler : IRequestHandler<UpdateAllGuildsQuery>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public UpdateAllGuildsQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<Unit> Handle(UpdateAllGuildsQuery request, CancellationToken cancellationToken)
        {
            var usersToAdd = request.Users
                .ExceptBy(this._cybermancyDbContext.Users.Select(x => x.Id),
                x => x.Id)
                .Select(x => new User
                {
                    AvatarUrl = x.AvatarUrl,
                    Id = x.Id,
                    UserName = x.UserName,
                });

            if(usersToAdd.Any())
                await this._cybermancyDbContext.Users.AddRangeAsync(usersToAdd, cancellationToken);

            var guildsToAdd = request.Guilds
                .ExceptBy(
                this._cybermancyDbContext.Guilds.Select(x => x.Id),
                x => x.Id)
                .Select(x => new Guild
                {
                    Id = x.Id,
                    LevelSettings = new GuildLevelSettings(),
                    ModerationSettings = new GuildModerationSettings(),
                    LogSettings = new GuildLogSettings(),
                });

            if(guildsToAdd.Any())
                await this._cybermancyDbContext.Guilds.AddRangeAsync(guildsToAdd, cancellationToken);

            var rolesToAdd = request.Roles
                .ExceptBy(this._cybermancyDbContext.Roles.Select(x => x.Id),
                x => x.Id)
                .Select(x => new Role
                {
                    Id = x.Id,
                    GuildId = x.GuildId
                });

            if(rolesToAdd.Any())
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

            if(channelsToAdd.Any())
                await this._cybermancyDbContext.Channels.AddRangeAsync(channelsToAdd, cancellationToken);

            var guildUsersToAdd = request.GuildUsers
                .ExceptBy(this._cybermancyDbContext.GuildUsers.Select(x => new { x.UserId, x.GuildId }),
                x => new { x.UserId, x.GuildId })
                .Select(x => new GuildUser
                {
                    UserId = x.UserId,
                    GuildId= x.GuildId,
                    DisplayName = x.DisplayName
                });

            if(guildUsersToAdd.Any())
                await this._cybermancyDbContext.GuildUsers.AddRangeAsync(guildUsersToAdd, cancellationToken);

            if(usersToAdd.Any() || guildsToAdd.Any() || rolesToAdd.Any() || channelsToAdd.Any() || guildUsersToAdd.Any())
                await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }


}
