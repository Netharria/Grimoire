// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Cybermancy.Core.Extensions;
using Cybermancy.Domain;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Core.Features.Leveling
{
    public static class LevelingExtensions
    {
        public static IQueryable<GuildUser> GetRankedUsersAsync(
            this IQueryable<GuildUser> guildUsers,
            ulong guildId, int count = 15, int page = 0) =>
            guildUsers.Where(x => x.GuildId == guildId)
                .OrderByDescending(x => x.Xp)
                .Skip(page * 15)
                .Take(count);

        public static IEnumerable<T> UpdateIgnoredStatus<T>(this IEnumerable<T> ignorableItems, bool shouldBeIgnored, StringBuilder? outputString = null) where T : IXpIgnore
        {
            foreach (var ignorable in ignorableItems)
            {
                ignorable.IsXpIgnored = shouldBeIgnored;
                if (outputString is not null)
                    outputString.Append(ignorable.Mention()).Append(' ');
                yield return ignorable;
            }
        }
    }
}
