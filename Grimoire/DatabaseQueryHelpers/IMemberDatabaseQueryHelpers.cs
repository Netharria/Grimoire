// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.DatabaseQueryHelpers;

public static class IMemberDatabaseQueryHelpers
{
    public static IQueryable<TSource> WhereMemberHasId<TSource>(this IQueryable<TSource> members, ulong userId, ulong guildId) where TSource : IMember
        => members.Where(x => x.UserId == userId && x.GuildId == guildId);
}
