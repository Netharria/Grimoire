// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public static class UserDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingUsersAsync(this DbSet<User> databaseUsers,
        IReadOnlyCollection<UserDto> users,
        CancellationToken cancellationToken = default)
    {
        var incomingUserIds = users
            .Select(x => x.Id);

        var existingUserIds = await databaseUsers
            .AsNoTracking()
            .Where(user => incomingUserIds.Contains(user.Id))
            .Select(user => user.Id)
            .AsAsyncEnumerable()
            .ToHashSetAsync(cancellationToken);

        var usersToAdd = users
            .Where(x => !existingUserIds.Contains(x.Id))
            .Select(x => new User { Id = x.Id })
            .ToArray().AsReadOnly();

        if (usersToAdd.Count == 0)
            return false;

        await databaseUsers.AddRangeAsync(usersToAdd, cancellationToken);
        return true;
    }

    public static async Task<bool> AddMissingUsernameHistoryAsync(this DbSet<UsernameHistory> databaseUsernames,
        IReadOnlyCollection<UserDto> users, CancellationToken cancellationToken = default)
    {
        var incomingUserIds = users
            .Select(x => x.Id);

        var existingUsernames = await databaseUsernames
            .AsNoTracking()
            .Where(user => incomingUserIds.Contains(user.UserId))
            .GroupBy(username => username.UserId)
            .Select(usernameGroup =>
                new
                {
                    UserId = usernameGroup.Key,
                    usernameGroup.OrderByDescending(username => username.Timestamp).First().Username
                })
            .AsAsyncEnumerable()
            .Select(x => (x.UserId, x.Username))
            .ToHashSetAsync(cancellationToken);

        var usernamesToAdd = users
            .Where(x => !existingUsernames.Contains((x.Id, x.Username)))
            .Select(x => new UsernameHistory { UserId = x.Id, Username = x.Username })
            .ToArray().AsReadOnly();

        if (usernamesToAdd.Count == 0)
            return false;

        await databaseUsernames.AddRangeAsync(usernamesToAdd, cancellationToken);
        return true;
    }
}
