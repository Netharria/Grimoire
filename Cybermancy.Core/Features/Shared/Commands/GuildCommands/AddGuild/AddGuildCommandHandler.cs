// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
using Cybermancy.Core.Features.Logging;
using Cybermancy.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Shared.Commands.GuildCommands.AddGuild
{
    public class AddGuildCommandHandler : ICommandHandler<AddGuildCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;
        private readonly IInviteService _inviteService;

        public AddGuildCommandHandler(ICybermancyDbContext cybermancyDbContext, IInviteService inviteService)
        {
            this._cybermancyDbContext = cybermancyDbContext;
            this._inviteService = inviteService;
        }

        public async ValueTask<Unit> Handle(AddGuildCommand command, CancellationToken cancellationToken)
        {
            var usersAdded = await this._cybermancyDbContext.Users.AddMissingUsersAsync(command.Users, cancellationToken);

            var guildExists = await this._cybermancyDbContext.Guilds.AnyAsync(x => x.Id == command.GuildId, cancellationToken);

            if (!guildExists)
                await this._cybermancyDbContext.Guilds.AddAsync(
                    new Guild
                    {
                        Id = command.GuildId,
                        LevelSettings = new GuildLevelSettings(),
                        ModerationSettings = new GuildModerationSettings(),
                        LogSettings = new GuildLogSettings(),
                    }, cancellationToken);

            var rolesAdded = await this._cybermancyDbContext.Roles.AddMissingRolesAsync(command.Roles, cancellationToken);

            var channelsAdded = await this._cybermancyDbContext.Channels.AddMissingChannelsAsync(command.Channels, cancellationToken);

            var membersAdded = await this._cybermancyDbContext.Members.AddMissingMembersAsync(command.Members, cancellationToken);

            this._inviteService.UpdateAllInvites(command.Invites.ToList());

            if (usersAdded || !guildExists || rolesAdded || channelsAdded || membersAdded)
                await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
