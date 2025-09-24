// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Events;

internal sealed class RoleDeleted(IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : IEventHandler<GuildRoleDeletedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    public async Task HandleEventAsync(DiscordClient sender, GuildRoleDeletedEventArgs eventArgs)
    {
        var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        await dbContext.Roles.Where(role => role.Id == eventArgs.Role.Id).ExecuteDeleteAsync();
    }
}
