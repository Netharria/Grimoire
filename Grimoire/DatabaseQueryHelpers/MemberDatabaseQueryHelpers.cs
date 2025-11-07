// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public static class MemberDatabaseQueryHelpers
{
    public static async Task<bool> AddMissingNickNameHistoryAsync(this DbSet<NicknameHistory> databaseNicknames,
        DiscordGuild discordGuild, CancellationToken cancellationToken = default)
    {
        var discordMembers = discordGuild.Members.Select(member => member.Value.GetUserId());

        var existingNicknames = await databaseNicknames
            .AsNoTracking()
            .Where(x => x.GuildId == discordGuild.GetGuildId())
            .Where(x => discordMembers.Contains(x.UserId))
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

        var nicknamesToAdd = discordGuild.Members.Values
            .Where(x => !existingNicknames.Contains((x.GetUserId(), x.GetGuildId(), x.GetNickname())))
            .Select(x => new NicknameHistory { GuildId = x.GetGuildId(), UserId = x.GetUserId(), Nickname = x.GetNickname() })
            .ToArray();

        if (nicknamesToAdd.Length == 0)
            return false;
        await databaseNicknames.AddRangeAsync(nicknamesToAdd, cancellationToken);
        return true;
    }

    public static async Task<bool> AddMissingAvatarsHistoryAsync(this DbSet<Avatar> databaseAvatars,
        DiscordGuild discordGuild, CancellationToken cancellationToken = default)
    {
        var discordMembers = discordGuild.Members.Select(member => member.Value.GetUserId());

        var existingAvatars = await databaseAvatars
            .AsNoTracking()
            .Where(avatar => avatar.GuildId == discordGuild.GetGuildId())
            .Where(avatar => discordMembers.Contains(avatar.UserId))
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

        var avatarsToAdd = discordGuild.Members.Values
            .Where(x => !existingAvatars.Contains((x.GetUserId(), x.GetGuildId(), x.GetAvatarFileName())))
            .Select(x => new Avatar { UserId = x.GetUserId(), GuildId = x.GetGuildId(), FileName = x.GetAvatarFileName() })
            .ToArray();

        if (avatarsToAdd.Length == 0)
            return false;

        await databaseAvatars.AddRangeAsync(avatarsToAdd, cancellationToken);
        return true;
    }
}
