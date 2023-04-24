// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Features.Logging;

namespace Grimoire.Core.Features.Shared.Commands.GuildCommands.AddGuild
{
    public class AddGuildCommandHandler : ICommandHandler<AddGuildCommand>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;
        private readonly IInviteService _inviteService;

        public AddGuildCommandHandler(IGrimoireDbContext grimoireDbContext, IInviteService inviteService)
        {
            this._grimoireDbContext = grimoireDbContext;
            this._inviteService = inviteService;
        }

        public async ValueTask<Unit> Handle(AddGuildCommand command, CancellationToken cancellationToken)
        {
            var usersAdded = await this._grimoireDbContext.Users.AddMissingUsersAsync(command.Users, cancellationToken);

            var guildExists = await this._grimoireDbContext.Guilds.AnyAsync(x => x.Id == command.GuildId, cancellationToken);

            if (!guildExists)
                await this._grimoireDbContext.Guilds.AddAsync(
                    new Guild
                    {
                        Id = command.GuildId,
                        LevelSettings = new GuildLevelSettings(),
                        ModerationSettings = new GuildModerationSettings(),
                        UserLogSettings = new GuildUserLogSettings(),
                        MessageLogSettings = new GuildMessageLogSettings(),
                    }, cancellationToken);

            var rolesAdded = await this._grimoireDbContext.Roles.AddMissingRolesAsync(command.Roles, cancellationToken);

            var channelsAdded = await this._grimoireDbContext.Channels.AddMissingChannelsAsync(command.Channels, cancellationToken);

            var membersAdded = await this._grimoireDbContext.Members.AddMissingMembersAsync(command.Members, cancellationToken);

            this._inviteService.UpdateAllInvites(command.Invites.ToList());

            if (usersAdded || !guildExists || rolesAdded || channelsAdded || membersAdded)
                await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
