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

public sealed class AddRoleCommandHandler(GrimoireDbContext grimoireDbContext) : IRequestHandler<AddRoleCommand>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async Task Handle(AddRoleCommand command, CancellationToken cancellationToken)
    {
        await this._grimoireDbContext.Roles.AddAsync(new Role { Id = command.RoleId, GuildId = command.GuildId },
            cancellationToken);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
    }
}
