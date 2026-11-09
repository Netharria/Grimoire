// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public static class CustomCommandDatabaseQueryHelpers
{
    /// <summary>The current version of each command: the rows with no newer version of the same name.</summary>
    public static IQueryable<CustomCommand> LatestVersions(this IQueryable<CustomCommand> customCommands)
        => customCommands.Where(command => !customCommands.Any(y =>
            y.GuildId == command.GuildId && y.Name == command.Name && y.CreatedAt > command.CreatedAt));

    /// <summary>The current version of one command, with its roles, untracked.</summary>
    public static IQueryable<CustomCommand> GetCustomCommandQuery(
        this IQueryable<CustomCommand> customCommands, GuildId guildId, CustomCommandName commandName)
        => customCommands
            .LatestVersions()
            .AsNoTracking()
            .Include(command => command.Roles)
            .Where(command => command.GuildId == guildId && command.Name == commandName);

    public static async Task RecordCommandUsageAsync(
        this IDbContextFactory<GrimoireDbContext> dbContextFactory,
        CustomCommandName name,
        GuildId guildId,
        UserId userId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.CustomCommandUsages.AddAsync(new CustomCommandUsage
        {
            Name = name, GuildId = guildId, UserId = userId, UsedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }
}
