// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.DatabaseQueryHelpers;

public static class UserDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingUsersAsync(this DbSet<User> databaseUsers, IEnumerable<UserDto> users, CancellationToken cancellationToken = default)
    {
        var usersToAdd = users
            .ExceptBy(databaseUsers.AsNoTracking().Select(x => x.Id),
            x => x.Id)
            .Select(x => new User
            {
                Id = x.Id,
                UsernameHistories = new List<UsernameHistory> {
                    new UsernameHistory {
                        Username = x.Username,
                        UserId = x.Id,
                    }
                }
            });

        if (usersToAdd.Any())
            await databaseUsers.AddRangeAsync(usersToAdd, cancellationToken);
        return usersToAdd.Any();
    }

    public static async Task<bool> AddMissingUsernameHistoryAsync(this DbSet<UsernameHistory> databaseUsernames, IEnumerable<UserDto> users, CancellationToken cancellationToken = default)
    {
        var usernamesToAdd = users
            .ExceptBy(databaseUsernames
            .OrderByDescending(x => x.Timestamp)
            .AsNoTracking()
            .Select(x => new { x.UserId, x.Username })
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
