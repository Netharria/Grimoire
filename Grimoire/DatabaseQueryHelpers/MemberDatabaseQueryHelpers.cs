// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public static class MemberDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingMembersAsync(this DbSet<Member> databaseMembers, IEnumerable<MemberDto> members, CancellationToken cancellationToken = default)
    {
        var userIds = members.Select(x => x.UserId);
        var guildIds = members.Select(x => x.GuildId);

        var existingMemberIds = await databaseMembers
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && guildIds.Contains(x.GuildId))
            .Select(x => new { x.UserId, x.GuildId })
            .AsAsyncEnumerable()
            .ToHashSetAsync(cancellationToken);


        var membersToAdd = members
            .Where(x => !existingMemberIds.Contains(new { x.UserId, x.GuildId }))
            .Select(x => new Member
            {
                UserId = x.UserId,
                GuildId = x.GuildId,
                IsIgnoredMember = null,
                XpHistory =
                    [
                        new() {
                            UserId = x.UserId,
                            GuildId = x.GuildId,
                            Xp = 0,
                            Type = XpHistoryType.Created,
                            TimeOut = DateTime.UtcNow
                        }
                    ]
            });

        if (membersToAdd.Any())
        {
            await databaseMembers.AddRangeAsync(membersToAdd, cancellationToken);
            return true;
        }
        return false;
    }

    public static async Task<bool> AddMissingNickNameHistoryAsync(this DbSet<NicknameHistory> databaseNicknames, IEnumerable<MemberDto> users, CancellationToken cancellationToken = default)
    {
        var existingNicknames = await databaseNicknames
            .AsNoTracking()
            .GroupBy(x => new { x.UserId, x.GuildId })
            .Select(x => new { x.Key.UserId, x.Key.GuildId, x.OrderByDescending(x => x.Timestamp).First().Nickname })
            .AsAsyncEnumerable()
            .Select(x => (x.UserId, x.GuildId, x.Nickname))
            .ToHashSetAsync(cancellationToken);


        var nicknamesToAdd = users
            .Where(x => !existingNicknames.Contains((x.UserId, x.GuildId, x.Nickname)))
            .Select(x =>  new NicknameHistory
            {
                GuildId = x.GuildId,
                UserId = x.UserId,
                Nickname = x.Nickname
            });
        if (nicknamesToAdd.Any())
        {
            await databaseNicknames.AddRangeAsync(nicknamesToAdd, cancellationToken);
            return true;
        }
        return false;
    }

    public static async Task<bool> AddMissingAvatarsHistoryAsync(this DbSet<Avatar> databaseAvatars, IEnumerable<MemberDto> users, CancellationToken cancellationToken = default)
    {
        var existingAvatars = await databaseAvatars
            .AsNoTracking()
            .GroupBy(x => new { x.UserId, x.GuildId })
            .Select(x => new { x.Key.UserId, x.Key.GuildId, x.OrderByDescending(x => x.Timestamp).First().FileName })
            .AsAsyncEnumerable()
            .Select(x => (x.UserId, x.GuildId, x.FileName))
            .ToHashSetAsync(cancellationToken);

        var avatarsToAdd = users
            .Where(x => !existingAvatars.Contains((x.UserId, x.GuildId, x.AvatarUrl)))
            .Select(x =>  new Avatar
            {
                UserId = x.UserId,
                GuildId = x.GuildId,
                FileName = x.AvatarUrl
            });
        if (avatarsToAdd.Any())
        {
            await databaseAvatars.AddRangeAsync(avatarsToAdd, cancellationToken);
            return true;
        }
        return false;
    }
}
