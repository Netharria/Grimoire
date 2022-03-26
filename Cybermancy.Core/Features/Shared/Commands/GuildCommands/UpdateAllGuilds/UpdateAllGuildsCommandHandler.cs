// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
using MediatR;

namespace Cybermancy.Core.Features.Shared.Commands.GuildCommands.UpdateAllGuilds
{
    public class UpdateAllGuildsCommandHandler : IRequestHandler<UpdateAllGuildsCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public UpdateAllGuildsCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<Unit> Handle(UpdateAllGuildsCommand request, CancellationToken cancellationToken)
        {
            var usersAdded = await this._cybermancyDbContext.Users.AddMissingUsersAsync(request.Users, cancellationToken);

            var guildsAdded = await this._cybermancyDbContext.Guilds.AddMissingGuildsAsync(request.Guilds, cancellationToken);

            var rolesAdded = await this._cybermancyDbContext.Roles.AddMissingRolesAsync(request.Roles, cancellationToken);

            var channelsAdded = await this._cybermancyDbContext.Channels.AddMissingChannelsAsync(request.Channels, cancellationToken);

            var guildUsersAdded = await this._cybermancyDbContext.GuildUsers.AddMissingGuildUsersAsync(request.GuildUsers, cancellationToken);

            if (usersAdded || guildsAdded || rolesAdded || channelsAdded || guildUsersAdded)
                await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }


}
