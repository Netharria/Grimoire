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
        DiscordGuild guild,
        CancellationToken cancellationToken = default)
    {

        var existingRoleIds = await databaseRoles
            .AsNoTracking()
            .Where(x => guild.Roles.Keys.Contains(x.Id))
            .Select(x => x.Id)
            .AsAsyncEnumerable()
            .ToHashSetAsync(cancellationToken);

        var rolesToAdd = guild.Roles.Keys
            .Where(x => !existingRoleIds.Contains(x))
            .Select(x => new Role { Id = x, GuildId = guild.Id })
            .ToArray();

        if (rolesToAdd.Length == 0)
            return false;

        await databaseRoles.AddRangeAsync(rolesToAdd, cancellationToken);
        return true;
    }

    public static async Task<bool> AddMissingRolesAsync(this DbSet<Role> databaseRoles,
        IEnumerable<ulong> roles,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {

        var existingRoleIds = await databaseRoles
            .AsNoTracking()
            .Where(x => roles.Contains(x.Id))
            .Select(x => x.Id)
            .AsAsyncEnumerable()
            .ToHashSetAsync(cancellationToken);

        var rolesToAdd = roles
            .Where(x => !existingRoleIds.Contains(x))
            .Select(x => new Role { Id = x, GuildId = guildId })
            .ToArray();

        if (rolesToAdd.Length == 0)
            return false;

        await databaseRoles.AddRangeAsync(rolesToAdd, cancellationToken);
        return true;
    }
}
