// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.DatabaseQueryHelpers
{
    public static class UserDatabaseQueryHelpers
    {
        public static async Task<bool> AddMissingUsersAsync(this DbSet<User> databaseUsers, IEnumerable<UserDto> users, CancellationToken cancellationToken = default)
        {
            var usersToAdd = users
                .ExceptBy(databaseUsers.Select(x => x.Id),
                x => x.Id)
                .Select(x => new User
                {
                    Id = x.Id,
                    UsernameHistories = new List<UsernameHistory> {
                        new UsernameHistory {
                            Username = x.UserName,
                            UserId = x.Id,
                        }
                    }
                });

            if (usersToAdd.Any())
                await databaseUsers.AddRangeAsync(usersToAdd, cancellationToken);
            return usersToAdd.Any();
        }
    }
}
