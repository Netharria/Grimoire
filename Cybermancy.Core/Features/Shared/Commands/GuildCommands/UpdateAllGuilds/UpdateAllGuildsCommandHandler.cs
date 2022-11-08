// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Features.Logging;
using Mediator;

namespace Cybermancy.Core.Features.Shared.Commands.GuildCommands.UpdateAllGuilds
{
    public class UpdateAllGuildsCommandHandler : ICommandHandler<UpdateAllGuildsCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        private readonly IInviteService _inviteService;

        public UpdateAllGuildsCommandHandler(ICybermancyDbContext cybermancyDbContext, IInviteService inviteService)
        {
            this._cybermancyDbContext = cybermancyDbContext;
            this._inviteService = inviteService;
        }

        public async ValueTask<Unit> Handle(UpdateAllGuildsCommand request, CancellationToken cancellationToken)
        {
            var usersAdded = await this._cybermancyDbContext.Users.AddMissingUsersAsync(request.Users, cancellationToken);

            var guildsAdded = await this._cybermancyDbContext.Guilds.AddMissingGuildsAsync(request.Guilds, cancellationToken);

            var rolesAdded = await this._cybermancyDbContext.Roles.AddMissingRolesAsync(request.Roles, cancellationToken);

            var channelsAdded = await this._cybermancyDbContext.Channels.AddMissingChannelsAsync(request.Channels, cancellationToken);

            var membersAdded = await this._cybermancyDbContext.Members.AddMissingMembersAsync(request.Members, cancellationToken);

            this._inviteService.UpdateAllInvites(request.Invites.ToList());

            if (usersAdded || guildsAdded || rolesAdded || channelsAdded || membersAdded)
                await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }


}
