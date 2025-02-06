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

public sealed class DeleteRoleCommandHandler(IDbContextFactory<GrimoireDbContext> dbContextFactory) : IRequestHandler<DeleteRoleCommand>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    public async Task Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Roles.Remove(dbContext.Roles.First(x => x.Id == command.RoleId));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
