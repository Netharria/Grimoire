// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public static class UserDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingUsersAsync(this DbSet<User> databaseUsers, IEnumerable<UserDto> users, CancellationToken cancellationToken = default)
    {
        var incomingUserIds = users
            .Select(x => x.Id);

        var existingUserIds = await databaseUsers
            .AsNoTracking()
            .Where(x => incomingUserIds.Contains(x.Id))
            .Select(x => x.Id)
            .AsAsyncEnumerable()
            .ToHashSetAsync(cancellationToken);

        var usersToAdd = users
            .Where(x => !existingUserIds.Contains(x.Id))
            .Select(x => new User
            {
                Id = x.Id
            });

        if (usersToAdd.Any())
        {
            await databaseUsers.AddRangeAsync(usersToAdd, cancellationToken);
            return true;
        }
            
        return false;
    }

    public static async Task<bool> AddMissingUsernameHistoryAsync(this DbSet<UsernameHistory> databaseUsernames, IEnumerable<UserDto> users, CancellationToken cancellationToken = default)
    {
        var existingUsernames = await databaseUsernames
            .AsNoTracking()
            .GroupBy(x => x.UserId)
            .Select(x => new { UserId = x.Key, x.OrderByDescending(x => x.Timestamp).First().Username })
            .AsAsyncEnumerable()
            .Select(x => (x.UserId, x.Username))
            .ToHashSetAsync(cancellationToken);

        var usernamesToAdd = users
            .Where(x => !existingUsernames.Contains((x.Id, x.Username)))
            .Select(x => new UsernameHistory
            {
                UserId = x.Id,
                Username = x.Username
            });
        if (usernamesToAdd.Any())
        {
            await databaseUsernames.AddRangeAsync(usernamesToAdd, cancellationToken);
            return true;
        }
        return false;
    }
}
