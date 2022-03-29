// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Shared.SharedDtos;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.DatabaseQueryHelpers
{
    public static class MemberDatabaseQueryHelpers
    {
        public static async Task<bool> AddMissingMembersAsync(this DbSet<Member> databaseMembers, IEnumerable<MemberDto> members, CancellationToken cancellationToken)
        {
            var membersToAdd = members
                .ExceptBy(databaseMembers.Select(x => new { x.UserId, x.GuildId }),
                x => new { x.UserId, x.GuildId })
                .Select(x =>
                {
                    var member = new Member
                    {
                        UserId = x.UserId,
                        GuildId = x.GuildId
                    };
                    if(x.Nickname != null)
                        member.NicknamesHistory.Add(
                        new NicknameHistory
                        {
                            GuildId = x.GuildId,
                            UserId = x.UserId,
                            Nickname = x.Nickname
                        });
                    return member;
                });

            if (membersToAdd.Any())
                await databaseMembers.AddRangeAsync(membersToAdd, cancellationToken);
            return membersToAdd.Any();
        }

        public static IQueryable<Member> WhereLoggingEnabled(this IQueryable<Member> members)
            => members.Where(x => x.Guild.LogSettings.ModuleEnabled);

        public static IQueryable<Member> WhereLevelingEnabled(this IQueryable<Member> members)
            => members.Where(x => x.Guild.LevelSettings.ModuleEnabled);

        public static IQueryable<Member> WhereMemberNotIgnored(this IQueryable<Member> members, ulong channelId, ulong[] roleIds)
            => members
                .WhereIgnored()
                .Where(x => !x.Guild.Roles.Where(x => roleIds.Contains(x.Id)).Any(y => y.IsXpIgnored)
                || !x.Guild.Channels.Where(x => x.Id == channelId).Any(y => y.IsXpIgnored));
    }
}
