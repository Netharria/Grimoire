// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain.Shared;

public interface IIgnored
{
    ulong Id { get; init; }
    ulong GuildId { get; init; }
}

public enum IgnoredType
{
    Channel,
    Role,
    User
}

internal static class IgnoredTypeExtensions
{
    public static string GetIgnoredTypeCacheKey(this IgnoredType ignoredType, ulong id, ulong guildId)
    {
        return ignoredType switch
        {
            IgnoredType.Channel => $"IgnoredChannel-{guildId}-{id}",
            IgnoredType.Role => $"IgnoredRole-{guildId}-{id}",
            IgnoredType.User => $"IgnoredUser-{guildId}-{id}",
            _ => throw new ArgumentOutOfRangeException(nameof(ignoredType), ignoredType, null)
        };
    }
}
