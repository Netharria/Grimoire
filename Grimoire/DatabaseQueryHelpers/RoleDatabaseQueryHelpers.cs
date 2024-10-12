// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public static class RoleDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingRolesAsync(this DbSet<Role> databaseRoles, IEnumerable<RoleDto> roles, CancellationToken cancellationToken = default)
    {
        var incomingRoles = roles
            .Select(x => new Role
            {
                Id = x.Id,
                GuildId = x.GuildId
            });

        var rolesToAdd = incomingRoles.ExceptBy(databaseRoles
            .AsNoTracking()
            .Where(x => incomingRoles.Contains(x))
            .Select(x => x.Id), x => x.Id);

        if (rolesToAdd.Any())
            await databaseRoles.AddRangeAsync(rolesToAdd, cancellationToken);
        return rolesToAdd.Any();
    }
}
