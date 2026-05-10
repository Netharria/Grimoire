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
            .Select(x => (Username?)x.Username)
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
            .Select(x => (AvatarFileName?)x.FileName)
            .FirstOrDefaultAsync();

        var newUsername = eventArgs.Member.GetUsername();
        var newNickname = eventArgs.Member.GetNickname();
        var newAvatar = eventArgs.Member.GetAvatarFileName(MediaFormat.Auto, 128);

        var usernameChanged = latestUsername is null
                              || !latestUsername.Value.Equals(newUsername, StringComparison.CurrentCultureIgnoreCase);
        var nicknameChanged = latestNickname != newNickname
                              && !(latestNickname.HasValue && newNickname.HasValue
                                                           && latestNickname.Value.Equals(newNickname.Value,
                                                               StringComparison.CurrentCultureIgnoreCase));
        var avatarChanged = newAvatar is not null
                            && (latestAvatar is null ||
                                !latestAvatar.Value.Equals(newAvatar.Value, StringComparison.Ordinal));

        if (usernameChanged)
            await dbContext.UsernameHistory.AddAsync(
                new UsernameHistory { Username = newUsername, UserId = userId });

        if (nicknameChanged)
            await dbContext.NicknameHistory.AddAsync(
                new NicknameHistory { UserId = userId, GuildId = guildId, Nickname = newNickname });

        if (avatarChanged)
            await dbContext.Avatars.AddAsync(
                new Avatar { UserId = userId, GuildId = guildId, FileName = newAvatar!.Value });

        if (usernameChanged || nicknameChanged || avatarChanged)
            await dbContext.SaveChangesAsync();
    }
}
