// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Core.Extensions;
using Grimoire.Domain.Shared;

namespace Grimoire.Core.Features.Leveling
{
    public static class LevelingExtensions
    {
        public static IQueryable<Member> GetRankedUsersAsync(
            this IQueryable<Member> member,
            ulong guildId, int count = 15, int page = 0) =>
            member.Where(x => x.GuildId == guildId)
                .OrderByDescending(x => x.XpHistory.Sum(x => x.Xp))
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
