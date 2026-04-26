// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public static class CustomCommandDatabaseQueryHelpers
{
    public static IQueryable<GetCustomCommandQueryResult> GetCustomCommandQuery(
        this IQueryable<CustomCommand> customCommands, GuildId guildId, CustomCommandName commandName)
        => customCommands
            .Where(command => command.GuildId == guildId && command.Name == commandName)
            .Where(command => !customCommands.Any(y =>
                y.GuildId == command.GuildId && y.Name == command.Name && y.CreatedAt > command.CreatedAt))
            .Select(command => new GetCustomCommandQueryResult
            {
                Content = command.Content,
                HasMention = command.Content.Contains("%mention"),
                HasMessage = command.Content.Contains("%message"),
                OutputFormat = command.IsEmbedded
                    ? new CommandOutputFormat.Embedded(command.EmbedColor)
                    : new CommandOutputFormat.PlainText(),
                RestrictedUse = command.RestrictedUse,
                PermissionRoles = command.Roles.Select(role => role.RoleId).ToArray()
            });

    public record GetCustomCommandQueryResult
    {
        public required string Content { get; init; }
        public required bool HasMention { get; init; }
        public required bool HasMessage { get; init; }
        public required CommandOutputFormat OutputFormat { get; init; }
        internal bool RestrictedUse { get; init; }
        internal RoleId[] PermissionRoles { get; init; } = [];

        public CommandAccess Access => (RestrictedUse, PermissionRoles) switch
        {
            (false, { Length: 0 }) => new CommandAccess.Open(),
            (true, var roles)      => new CommandAccess.Allowlist(roles),
            (false, var roles)     => new CommandAccess.Blocklist(roles),
        };
    }
}
