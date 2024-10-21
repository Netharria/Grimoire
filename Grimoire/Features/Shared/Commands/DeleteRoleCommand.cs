// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Commands;

public sealed record DeleteRoleCommand : IRequest
{
    public ulong RoleId { get; init; }
}

public sealed class DeleteRoleCommandHandler(GrimoireDbContext grimoireDbContext) : IRequestHandler<DeleteRoleCommand>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async Task Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {

        this._grimoireDbContext.Roles.Remove(this._grimoireDbContext.Roles.First(x => x.Id == command.RoleId));
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
    }
}
