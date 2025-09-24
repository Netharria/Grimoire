// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Events;

internal sealed class RoleAdded(IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : IEventHandler<GuildRoleCreatedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    public async Task HandleEventAsync(DiscordClient sender, GuildRoleCreatedEventArgs eventArgs)
    {
        var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        await dbContext.Roles.AddAsync(new Role { Id = eventArgs.Role.Id, GuildId = eventArgs.Guild.Id });
        await dbContext.SaveChangesAsync();
    }
}
