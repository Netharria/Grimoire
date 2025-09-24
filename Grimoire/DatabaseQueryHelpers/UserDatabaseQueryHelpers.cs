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
        DiscordGuild discordGuild,
        CancellationToken cancellationToken = default)
    {

        var existingUserIds = await databaseUsers
            .AsNoTracking()
            .Where(user => discordGuild.Members.Keys.Contains(user.Id))
            .Select(user => user.Id)
            .AsAsyncEnumerable()
            .ToHashSetAsync(cancellationToken);

        var usersToAdd = discordGuild.Members.Keys
            .Where(x => !existingUserIds.Contains(x))
            .Select(x => new User { Id = x })
            .ToArray();

        if (usersToAdd.Length == 0)
            return false;

        await databaseUsers.AddRangeAsync(usersToAdd, cancellationToken);
        return true;
    }

    public static async Task<bool> AddMissingUsernameHistoryAsync(this DbSet<UsernameHistory> databaseUsernames,
        DiscordGuild discordGuild, CancellationToken cancellationToken = default)
    {

        var existingUsernames = await databaseUsernames
            .AsNoTracking()
            .Where(user => discordGuild.Members.Keys.Contains(user.UserId))
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

        var usernamesToAdd = discordGuild.Members.Values
            .Where(x => !existingUsernames.Contains((x.Id, x.Username)))
            .Select(x => new UsernameHistory { UserId = x.Id, Username = x.Username })
            .ToArray();

        if (usernamesToAdd.Length == 0)
            return false;

        await databaseUsernames.AddRangeAsync(usernamesToAdd, cancellationToken);
        return true;
    }
}
