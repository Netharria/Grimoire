// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public abstract record CommandOutputFormat
{
    public sealed record Text : CommandOutputFormat;

    public sealed record Embedded(CustomCommandEmbedColor? Color) : CommandOutputFormat;
}

public static class CustomCommandDatabaseQueryHelpers
{
    public static IQueryable<CustomCommand> GetCustomCommandQuery(
        this IQueryable<CustomCommand> customCommands, GuildId guildId, CustomCommandName commandName)
        => customCommands
            .AsNoTracking()
            .Include(command => command.Roles)
            .Where(command => command.GuildId == guildId && command.Name == commandName)
            .Where(command => !customCommands.Any(y =>
                y.GuildId == command.GuildId && y.Name == command.Name && y.CreatedAt > command.CreatedAt));
}
