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
        var existingNicknames = await databaseNicknames
            .AsNoTracking()
            .Where(x => x.GuildId == discordGuild.Id)
            .Where(x => discordGuild.Members.Keys.Contains(x.UserId))
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
            .Where(x => !existingNicknames.Contains((x.Id, x.Guild.Id, x.Nickname)))
            .Select(x => new NicknameHistory { GuildId = x.Guild.Id, UserId = x.Id, Nickname = x.Nickname })
            .ToArray();

        if (nicknamesToAdd.Length == 0)
            return false;
        await databaseNicknames.AddRangeAsync(nicknamesToAdd, cancellationToken);
        return true;
    }

    public static async Task<bool> AddMissingAvatarsHistoryAsync(this DbSet<Avatar> databaseAvatars,
        DiscordGuild discordGuild, CancellationToken cancellationToken = default)
    {
        var existingAvatars = await databaseAvatars
            .AsNoTracking()
            .Where(avatar => avatar.GuildId == discordGuild.Id)
            .Where(avatar => discordGuild.Members.Keys.Contains(avatar.UserId))
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
            .Where(x => !existingAvatars.Contains((x.Id, x.Guild.Id, x.AvatarUrl)))
            .Select(x => new Avatar { UserId = x.Id, GuildId = x.Guild.Id, FileName = x.AvatarUrl })
            .ToArray();

        if (avatarsToAdd.Length == 0)
            return false;

        await databaseAvatars.AddRangeAsync(avatarsToAdd, cancellationToken);
        return true;
    }
}
