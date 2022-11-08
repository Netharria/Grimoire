// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Mediator;

namespace Cybermancy.Core.Features.Shared.Commands.RoleCommands.DeleteRole
{
    public class DeleteRoleCommandHandler : ICommandHandler<DeleteRoleCommand>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public DeleteRoleCommandHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<Unit> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
        {

            this._cybermancyDbContext.Roles.Remove(this._cybermancyDbContext.Roles.First(x => x.Id == request.RoleId));
            await this._cybermancyDbContext.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }
    }
}
