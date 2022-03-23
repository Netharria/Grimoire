// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Domain;
using MediatR;

namespace Cybermancy.Core.Features.Shared.Commands.RoleCommands.DeleteRole
{
    public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public DeleteRoleCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<Unit> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
        {
            this._cybermancyDbContext.Roles.Remove(new Role { Id = request.RoleId });
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}
