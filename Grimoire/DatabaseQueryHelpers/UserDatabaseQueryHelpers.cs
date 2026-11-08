// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public static class UserDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingUsernameHistoryAsync(this DbSet<UsernameHistory> databaseUsernames,
        DiscordGuild discordGuild, CancellationToken cancellationToken = default)
    {
        var discordMembers = discordGuild.Members.Select(member => member.Value.GetUserId());

        var existingUsernames = await databaseUsernames
            .AsNoTracking()
            .Where(user => discordMembers.Contains(user.UserId))
            .GroupBy(username => username.UserId)
            .Select(usernameGroup =>
                new
                {
                    UserId = usernameGroup.Key,
                    usernameGroup.OrderByDescending(username => username.Timestamp).First().Username
                })
            .AsAsyncEnumerable()
            .Select(x => (x.UserId, x.Username))
            .ToHashSetAsync(cancellationToken: cancellationToken);

        var usernamesToAdd = discordGuild.Members.Values
            .Where(x => !existingUsernames.Contains((x.GetUserId(), x.GetUsername())))
            .Select(x => new UsernameHistory
            {
                UserId = x.GetUserId(), Username = x.GetUsername(), Timestamp = DateTimeOffset.UtcNow
            })
            .ToArray();

        if (usernamesToAdd.Length == 0)
            return false;

        await databaseUsernames.AddRangeAsync(usernamesToAdd, cancellationToken);
        return true;
    }

    /// <summary>
    ///     The user's latest known username, or their mention when none is stored: users who left before history was
    ///     kept have none (KI-33), and an empty name can't go in an embed field.
    /// </summary>
    public static async Task<string> GetDisplayNameAsync(this DbSet<UsernameHistory> databaseUsernames, UserId userId,
        CancellationToken cancellationToken = default)
        => await databaseUsernames
                .AsNoTracking()
                .Where(history => history.UserId == userId)
                .OrderByDescending(history => history.Timestamp)
                .Select(history => (Username?)history.Username)
                .FirstOrDefaultAsync(cancellationToken) is { } username
            ? username.Value
            : UserExtensions.Mention(userId);
}
