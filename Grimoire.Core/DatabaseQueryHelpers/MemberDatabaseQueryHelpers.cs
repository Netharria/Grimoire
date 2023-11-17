// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.DatabaseQueryHelpers;

public static class MemberDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingMembersAsync(this DbSet<Member> databaseMembers, IEnumerable<MemberDto> members, CancellationToken cancellationToken = default)
    {
        var membersToAdd = members
            .ExceptBy(databaseMembers.AsNoTracking().Select(x => new { x.UserId, x.GuildId }),
            x => new { x.UserId, x.GuildId })
            .Select(x =>
            {
                var member = new Member
                {
                    UserId = x.UserId,
                    GuildId = x.GuildId,
                    XpHistory = new List<XpHistory>
                    {
                        new() {
                            UserId = x.UserId,
                            GuildId = x.GuildId,
                            Xp = 0,
                            Type = XpHistoryType.Created,
                            TimeOut = DateTime.UtcNow
                        }
                    },
                    NicknamesHistory = new List<NicknameHistory>
                    {
                        new() {
                            GuildId = x.GuildId,
                            UserId = x.UserId,
                            Nickname = x.Nickname
                        }
                    },
                    AvatarHistory = new List<Avatar>
                    {
                        new() {
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

    public static async Task<bool> AddMissingNickNameHistoryAsync(this DbSet<NicknameHistory> databaseNicknames, IEnumerable<MemberDto> users, CancellationToken cancellationToken = default)
    {
        var nicknamesToAdd = users
            .ExceptBy(databaseNicknames
            .AsNoTracking()
            .GroupBy(x => new { x.UserId, x.GuildId })
            .Select(x => new { x.Key.UserId, x.Key.GuildId, x.OrderByDescending(x => x.Timestamp).First().Nickname })
            , x => new { x.UserId, x.GuildId, x.Nickname })
            .Select(x =>  new NicknameHistory
            {
                GuildId = x.GuildId,
                UserId = x.UserId,
                Nickname = x.Nickname
            });
        if (nicknamesToAdd.Any())
            await databaseNicknames.AddRangeAsync(nicknamesToAdd, cancellationToken);
        return nicknamesToAdd.Any();
    }

    public static async Task<bool> AddMissingAvatarsHistoryAsync(this DbSet<Avatar> databaseAvatars, IEnumerable<MemberDto> users, CancellationToken cancellationToken = default)
    {

        var avatarsToAdd = users
            .ExceptBy(databaseAvatars
            .AsNoTracking()
            .GroupBy(x => new { x.UserId, x.GuildId })
            .Select(x => new { x.Key.UserId, x.Key.GuildId, x.OrderByDescending(x => x.Timestamp).First().FileName })
            , x => new { x.UserId, x.GuildId, FileName = x.AvatarUrl })
            .Select(x =>  new Avatar
            {
                UserId = x.UserId,
                GuildId = x.GuildId,
                FileName = x.AvatarUrl
            });
        if (avatarsToAdd.Any())
            await databaseAvatars.AddRangeAsync(avatarsToAdd, cancellationToken);
        return avatarsToAdd.Any();
    }

    public static IQueryable<Member> WhereLoggingEnabled(this IQueryable<Member> members)
        => members.Where(x => x.Guild.UserLogSettings.ModuleEnabled);

    public static IQueryable<Member> WhereLevelingEnabled(this IQueryable<Member> members)
        => members.Where(x => x.Guild.LevelSettings.ModuleEnabled);

    public static IQueryable<Member> WhereMemberNotIgnored(this IQueryable<Member> members, ulong channelId, ulong[] roleIds)
            => members
            .Where(x => x.IsIgnoredMember == null)
            .Where(x => !x.Guild.IgnoredChannels.Any(y => y.ChannelId == channelId))
            .Where(x => !x.Guild.IgnoredRoles.Any(y => roleIds.Any(z => z == y.RoleId)));
}
