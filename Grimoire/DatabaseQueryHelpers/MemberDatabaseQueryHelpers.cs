// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public static class MemberDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingMembersAsync(this DbSet<Member> databaseMembers,
        IReadOnlyCollection<MemberDto> members, CancellationToken cancellationToken = default)
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
                XpHistory =
                [
                    new XpHistory
                    {
                        UserId = x.UserId,
                        GuildId = x.GuildId,
                        Xp = 0,
                        Type = XpHistoryType.Created,
                        TimeOut = DateTime.UtcNow
                    }
                ]
            }).ToArray().AsReadOnly();

        if (membersToAdd.Count == 0)
            return false;
        await databaseMembers.AddRangeAsync(membersToAdd, cancellationToken);
        return true;
    }

    public static async Task<bool> AddMissingNickNameHistoryAsync(this DbSet<NicknameHistory> databaseNicknames,
        IReadOnlyCollection<MemberDto> users, CancellationToken cancellationToken = default)
    {
        var userIds = users.Select(x => x.UserId);
        var guildIds = users.Select(x => x.GuildId);

        var existingNicknames = await databaseNicknames
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && guildIds.Contains(x.GuildId))
            .GroupBy(nickname => new { nickname.UserId, nickname.GuildId })
            .Select(nicknameGroup => new
            {
                nicknameGroup.Key.UserId,
                nicknameGroup.Key.GuildId,
                nicknameGroup.OrderByDescending(nickName => nickName.Timestamp).First().Nickname
            })
            .AsAsyncEnumerable()
            .Select(nickname => (nickname.UserId, nickname.GuildId, nickname.Nickname))
            .ToHashSetAsync(cancellationToken);

        var nicknamesToAdd = users
            .Where(x => !existingNicknames.Contains((x.UserId, x.GuildId, x.Nickname)))
            .Select(x => new NicknameHistory { GuildId = x.GuildId, UserId = x.UserId, Nickname = x.Nickname })
            .ToArray().AsReadOnly();

        if (nicknamesToAdd.Count == 0)
            return false;
        await databaseNicknames.AddRangeAsync(nicknamesToAdd, cancellationToken);
        return true;
    }

    public static async Task<bool> AddMissingAvatarsHistoryAsync(this DbSet<Avatar> databaseAvatars,
        IReadOnlyCollection<MemberDto> users, CancellationToken cancellationToken = default)
    {
        var userIds = users.Select(x => x.UserId);
        var guildIds = users.Select(x => x.GuildId);

        var existingAvatars = await databaseAvatars
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && guildIds.Contains(x.GuildId))
            .GroupBy(avatar => new { avatar.UserId, avatar.GuildId })
            .Select(avatarGroup
                => new
                {
                    avatarGroup.Key.UserId,
                    avatarGroup.Key.GuildId,
                    avatarGroup.OrderByDescending(x => x.Timestamp).First().FileName
                })
            .AsAsyncEnumerable()
            .Select(avatar => (avatar.UserId, avatar.GuildId, avatar.FileName))
            .ToHashSetAsync(cancellationToken);

        var avatarsToAdd = users
            .Where(x => !existingAvatars.Contains((x.UserId, x.GuildId, x.AvatarUrl)))
            .Select(x => new Avatar { UserId = x.UserId, GuildId = x.GuildId, FileName = x.AvatarUrl }).ToArray()
            .AsReadOnly();

        if (avatarsToAdd.Count == 0)
            return false;

        await databaseAvatars.AddRangeAsync(avatarsToAdd, cancellationToken);
        return true;
    }
}
