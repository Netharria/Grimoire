// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Features.Logging;

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

        public async ValueTask<Unit> Handle(UpdateAllGuildsCommand command, CancellationToken cancellationToken)
        {
            var usersAdded = await this._cybermancyDbContext.Users.AddMissingUsersAsync(command.Users, cancellationToken);

            var guildsAdded = await this._cybermancyDbContext.Guilds.AddMissingGuildsAsync(command.Guilds, cancellationToken);

            var rolesAdded = await this._cybermancyDbContext.Roles.AddMissingRolesAsync(command.Roles, cancellationToken);

            var channelsAdded = await this._cybermancyDbContext.Channels.AddMissingChannelsAsync(command.Channels, cancellationToken);

            var membersAdded = await this._cybermancyDbContext.Members.AddMissingMembersAsync(command.Members, cancellationToken);

            this._inviteService.UpdateAllInvites(command.Invites.ToList());

            if (usersAdded || guildsAdded || rolesAdded || channelsAdded || membersAdded)
                await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }


}
