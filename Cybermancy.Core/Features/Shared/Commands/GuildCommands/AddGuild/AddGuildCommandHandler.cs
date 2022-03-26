// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
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
            var usersAdded = await this._cybermancyDbContext.Users.AddMissingUsersAsync(request.Users, cancellationToken);

            var guildExists = await this._cybermancyDbContext.Guilds.AnyAsync(x => x.Id == request.GuildId, cancellationToken);

            if (!guildExists)
                await this._cybermancyDbContext.Guilds.AddAsync(
                    new Guild
                    {
                        Id = request.GuildId,
                        LevelSettings = new GuildLevelSettings(),
                        ModerationSettings = new GuildModerationSettings(),
                        LogSettings = new GuildLogSettings(),
                    }, cancellationToken);

            var rolesAdded = await this._cybermancyDbContext.Roles.AddMissingRolesAsync(request.Roles, cancellationToken);

            var channelsAdded = await this._cybermancyDbContext.Channels.AddMissingChannelsAsync(request.Channels, cancellationToken);

            var guildUsersAdded = await this._cybermancyDbContext.GuildUsers.AddMissingGuildUsersAsync(request.GuildUsers, cancellationToken);

            if (usersAdded || !guildExists || rolesAdded || channelsAdded || guildUsersAdded)
                await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
