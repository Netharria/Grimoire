// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain.Shared;

namespace Cybermancy.Core.DatabaseQueryHelpers
{
    public static class IMemberDatabaseQueryHelpers
    {
        public static IQueryable<TSource> WhereMemberHasId<TSource>(this IQueryable<TSource> members, ulong userId, ulong guildId) where TSource : IMember
            => members.Where(x => x.UserId == userId && x.GuildId == guildId);

        public static IQueryable<TSource> WhereMembersHaveIds<TSource>(this IQueryable<TSource> members, ulong[] userIds, ulong guildId) where TSource : IMember
            => members.Where(x => x.GuildId == guildId && userIds.Contains(x.UserId));
    }
}
