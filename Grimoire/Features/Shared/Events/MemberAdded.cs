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

        var userId = eventArgs.Member.GetUserId();
        var guildId = eventArgs.Guild.GetGuildId();

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

        if (!Username.Equals(latestUsername, eventArgs.Member.GetUsername(),
                StringComparison.CurrentCultureIgnoreCase))
            await dbContext.UsernameHistory.AddAsync(
                new UsernameHistory { Username = eventArgs.Member.GetUsername(), UserId = eventArgs.Member.GetUserId() });

        if (!Nickname.Equals(latestNickname, eventArgs.Member.GetNickname(),
                StringComparison.CurrentCultureIgnoreCase))
            await dbContext.NicknameHistory.AddAsync(
                new NicknameHistory
                {
                    UserId = eventArgs.Member.GetUserId(), GuildId = eventArgs.Guild.GetGuildId(), Nickname = eventArgs.Member.GetNickname()
                });

        if (!AvatarFileName.Equals(latestAvatar, eventArgs.Member.GetAvatarFileName(MediaFormat.Auto, 128),
                StringComparison.Ordinal))
            await dbContext.Avatars.AddAsync(
                new Avatar
                {
                    UserId = eventArgs.Member.GetUserId(),
                    GuildId = eventArgs.Guild.GetGuildId(),
                    FileName = eventArgs.Member.GetAvatarFileName(MediaFormat.Auto, 128)
                });

        if (!Username.Equals(latestUsername, eventArgs.Member.GetUsername(), StringComparison.CurrentCultureIgnoreCase)
            || !Nickname.Equals(latestNickname, eventArgs.Member.GetNickname(), StringComparison.CurrentCultureIgnoreCase)
            || !AvatarFileName.Equals(latestAvatar, eventArgs.Member.GetAvatarFileName(MediaFormat.Auto, 128), StringComparison.Ordinal))
            await dbContext.SaveChangesAsync();
    }
}
