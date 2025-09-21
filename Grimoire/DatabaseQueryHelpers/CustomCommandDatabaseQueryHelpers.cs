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
        this IQueryable<CustomCommand> customCommands, ulong guildId, string commandName)
        =>
            customCommands
                .AsSplitQuery()
                .Where(command => command.GuildId == guildId && command.Name == commandName)
                .Select(command => new GetCustomCommandQueryResult
                {
                    Content = command.Content,
                    HasMention = command.HasMention,
                    HasMessage = command.HasMessage,
                    IsEmbedded = command.IsEmbedded,
                    EmbedColor = command.EmbedColor,
                    RestrictedUse = command.RestrictedUse,
                    PermissionRoles = command.CustomCommandRoles.Select(commandRole => commandRole.RoleId).ToArray()
                });

    public record GetCustomCommandQueryResult
    {
        public required string Content { get; init; }
        public required bool HasMention { get; init; }
        public required bool HasMessage { get; init; }
        public required bool IsEmbedded { get; init; }
        public string? EmbedColor { get; init; }
        public required bool RestrictedUse { get; init; }
        public required ulong[] PermissionRoles { get; init; }
    }
}
