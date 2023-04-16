// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Shared.Commands.RoleCommands.AddRole
{
    public class AddRoleCommandHandler : ICommandHandler<AddRoleCommand>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public AddRoleCommandHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<Unit> Handle(AddRoleCommand command, CancellationToken cancellationToken)
        {
            await this._grimoireDbContext.Roles.AddAsync(new Role
                {
                    Id = command.RoleId,
                    GuildId = command.GuildId
                }, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}
