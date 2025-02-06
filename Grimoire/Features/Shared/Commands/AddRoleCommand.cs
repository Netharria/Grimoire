// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Commands;

public sealed record AddRoleCommand : IRequest
{
    public ulong RoleId { get; init; }
    public ulong GuildId { get; init; }
}

public sealed class AddRoleCommandHandler(IDbContextFactory<GrimoireDbContext> dbContextFactory) : IRequestHandler<AddRoleCommand>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    public async Task Handle(AddRoleCommand command, CancellationToken cancellationToken)
    {
        var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Roles.AddAsync(new Role { Id = command.RoleId, GuildId = command.GuildId },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
