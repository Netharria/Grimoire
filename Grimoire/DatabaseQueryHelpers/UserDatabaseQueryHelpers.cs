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
        var incomingUsers = users
            .Select(x => new User
            {
                Id = x.Id
            });

        var usersToAdd = incomingUsers.ExceptBy(databaseUsers
            .AsNoTracking()
            .Where(x => incomingUsers.Contains(x))
            .Select(x => x.Id), x => x.Id);

        if (usersToAdd.Any())
            await databaseUsers.AddRangeAsync(usersToAdd, cancellationToken);
        return usersToAdd.Any();
    }

    public static async Task<bool> AddMissingUsernameHistoryAsync(this DbSet<UsernameHistory> databaseUsernames, IEnumerable<UserDto> users, CancellationToken cancellationToken = default)
    {
        var usernamesToAdd = users
            .ExceptBy(databaseUsernames
            .AsNoTracking()
            .GroupBy(x => x.UserId)
            .Select(x => new { UserId = x.Key, x.OrderByDescending(x => x.Timestamp).First().Username })
            , x => new { UserId = x.Id, x.Username })
            .Select(x => new UsernameHistory
            {
                UserId = x.Id,
                Username = x.Username
            });
        if (usernamesToAdd.Any())
            await databaseUsernames.AddRangeAsync(usernamesToAdd, cancellationToken);
        return usernamesToAdd.Any();
    }
}
