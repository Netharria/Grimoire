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
        var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var userResult = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == eventArgs.Member.Id)
            .Select(x => new
            {
                dbContext.UsernameHistory.OrderByDescending(username => username.Timestamp).First().Username,
                dbContext.NicknameHistory.OrderByDescending(nickname => nickname.Timestamp).First().Nickname,
                dbContext.Avatars.OrderByDescending(avatar => avatar.Timestamp).First().FileName
            })
            .FirstOrDefaultAsync();

        if (!string.Equals(userResult?.Username, eventArgs.Member.Username,
                StringComparison.CurrentCultureIgnoreCase))
            await dbContext.UsernameHistory.AddAsync(
                new UsernameHistory { Username = eventArgs.Member.Username, UserId = eventArgs.Member.Id });

        if (!string.Equals(userResult?.Nickname, eventArgs.Member.Nickname,
                StringComparison.CurrentCultureIgnoreCase))
            await dbContext.NicknameHistory.AddAsync(
                new NicknameHistory
                {
                    UserId = eventArgs.Member.Id, GuildId = eventArgs.Guild.Id, Nickname = eventArgs.Member.Nickname
                });

        if (!string.Equals(userResult?.FileName, eventArgs.Member.GetGuildAvatarUrl(MediaFormat.Auto, 128),
                StringComparison.Ordinal))
            await dbContext.Avatars.AddAsync(
                new Avatar
                {
                    UserId = eventArgs.Member.Id,
                    GuildId = eventArgs.Guild.Id,
                    FileName = eventArgs.Member.GetGuildAvatarUrl(MediaFormat.Auto, 128)
                });

        if (userResult is null
            || !string.Equals(userResult.Username, eventArgs.Member.Username, StringComparison.CurrentCultureIgnoreCase)
            || !string.Equals(userResult.Nickname, eventArgs.Member.Nickname, StringComparison.CurrentCultureIgnoreCase)
            || !string.Equals(userResult.FileName, eventArgs.Member.AvatarUrl, StringComparison.Ordinal))
            await dbContext.SaveChangesAsync();
    }
}
