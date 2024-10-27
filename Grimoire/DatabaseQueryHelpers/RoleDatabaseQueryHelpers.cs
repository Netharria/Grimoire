// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public static class RoleDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingRolesAsync(this DbSet<Role> databaseRoles,
        IReadOnlyCollection<RoleDto> roles,
        CancellationToken cancellationToken = default)
    {
        var incomingRoles = roles
            .Select(x => x.Id);

        var existingRoleIds = await databaseRoles
            .AsNoTracking()
            .Where(x => incomingRoles.Contains(x.Id))
            .Select(x => x.Id)
            .AsAsyncEnumerable()
            .ToHashSetAsync(cancellationToken);

        var rolesToAdd = roles
            .Where(x => !existingRoleIds.Contains(x.Id))
            .Select(x => new Role { Id = x.Id, GuildId = x.GuildId })
            .ToArray().AsReadOnly();

        if (rolesToAdd.Count == 0)
            return false;

        await databaseRoles.AddRangeAsync(rolesToAdd, cancellationToken);
        return true;
    }
}
