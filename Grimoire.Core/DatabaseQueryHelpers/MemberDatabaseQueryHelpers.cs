// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.DatabaseQueryHelpers
{
    public static class MemberDatabaseQueryHelpers
    {
        public static async Task<bool> AddMissingMembersAsync(this DbSet<Member> databaseMembers, IEnumerable<MemberDto> members, CancellationToken cancellationToken = default)
        {
            var membersToAdd = members
                .ExceptBy(databaseMembers.Select(x => new { x.UserId, x.GuildId }),
                x => new { x.UserId, x.GuildId })
                .Select(x =>
                {
                    var member = new Member
                    {
                        UserId = x.UserId,
                        GuildId = x.GuildId,
                        XpHistory = new List<XpHistory>
                        {
                            new XpHistory
                            {
                                UserId = x.UserId,
                                GuildId = x.GuildId,
                                Xp = 0,
                                Type = XpHistoryType.Created,
                                TimeOut = DateTime.UtcNow
                            }
                        },
                        NicknamesHistory = new List<NicknameHistory>
                        {
                            new NicknameHistory
                            {
                                GuildId = x.GuildId,
                                UserId = x.UserId,
                                Nickname = x.Nickname
                            }
                        },
                        AvatarHistory = new List<Avatar>
                        {
                            new Avatar
                            {
                                UserId = x.UserId,
                                GuildId = x.GuildId,
                                FileName = x.AvatarUrl
                            }
                        }
                    };
                    return member;
                });

            if (membersToAdd.Any())
                await databaseMembers.AddRangeAsync(membersToAdd, cancellationToken);
            return membersToAdd.Any();
        }

        public static IQueryable<Member> WhereLoggingEnabled(this IQueryable<Member> members)
            => members.Where(x => x.Guild.UserLogSettings.ModuleEnabled);

        public static IQueryable<Member> WhereLevelingEnabled(this IQueryable<Member> members)
            => members.Where(x => x.Guild.LevelSettings.ModuleEnabled);

        public static IQueryable<Member> WhereMemberNotIgnored(this IQueryable<Member> members, ulong channelId, ulong[] roleIds)
                => members
                .WhereIgnored(false)
                .Where(x => !x.Guild.Channels.Where(y => y.Id == channelId).Any(y => y.IsXpIgnored))
                .Where(x => !x.Guild.Roles.Where(y => roleIds.Any(z => z == y.Id)).Any(y => y.IsXpIgnored));
    }
}
