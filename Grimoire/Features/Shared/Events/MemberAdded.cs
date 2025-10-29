// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Events;

internal sealed class MemberAdded(IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : IEventHandler<GuildMemberAddedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    public async Task HandleEventAsync(DiscordClient sender, GuildMemberAddedEventArgs eventArgs)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        var userId = eventArgs.Member.Id;
        var guildId = eventArgs.Guild.Id;

        var latestUsername = await dbContext.UsernameHistory
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Timestamp)
            .Select(x => x.Username)
            .FirstOrDefaultAsync();

        var latestNickname = await dbContext.NicknameHistory
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .OrderByDescending(x => x.Timestamp)
            .Select(x => x.Nickname)
            .FirstOrDefaultAsync();

        var latestAvatar = await dbContext.Avatars
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .OrderByDescending(x => x.Timestamp)
            .Select(x => x.FileName)
            .FirstOrDefaultAsync();

        if (!string.Equals(latestUsername, eventArgs.Member.Username,
                StringComparison.CurrentCultureIgnoreCase))
            await dbContext.UsernameHistory.AddAsync(
                new UsernameHistory { Username = eventArgs.Member.Username, UserId = eventArgs.Member.Id });

        if (!string.Equals(latestNickname, eventArgs.Member.Nickname,
                StringComparison.CurrentCultureIgnoreCase))
            await dbContext.NicknameHistory.AddAsync(
                new NicknameHistory
                {
                    UserId = eventArgs.Member.Id, GuildId = eventArgs.Guild.Id, Nickname = eventArgs.Member.Nickname
                });

        if (!string.Equals(latestAvatar, eventArgs.Member.GetGuildAvatarUrl(MediaFormat.Auto, 128),
                StringComparison.Ordinal))
            await dbContext.Avatars.AddAsync(
                new Avatar
                {
                    UserId = eventArgs.Member.Id,
                    GuildId = eventArgs.Guild.Id,
                    FileName = eventArgs.Member.GetGuildAvatarUrl(MediaFormat.Auto, 128)
                });

        if (!string.Equals(latestUsername, eventArgs.Member.Username, StringComparison.CurrentCultureIgnoreCase)
            || !string.Equals(latestNickname, eventArgs.Member.Nickname, StringComparison.CurrentCultureIgnoreCase)
            || !string.Equals(latestAvatar, eventArgs.Member.AvatarUrl, StringComparison.Ordinal))
            await dbContext.SaveChangesAsync();
    }
}
