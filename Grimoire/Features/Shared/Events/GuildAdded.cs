// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Logging.UserLogging;

namespace Grimoire.Features.Shared.Events;

internal sealed class GuildAdded(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    IInviteService inviteService)
    : IEventHandler<GuildCreatedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly IInviteService _inviteService = inviteService;

    public async Task HandleEventAsync(DiscordClient sender, GuildCreatedEventArgs eventArgs)
    {
        await sender.UpdateStatusAsync(new DiscordActivity($"{sender.Guilds.Count} servers.",
            DiscordActivityType.Watching));
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        var usernamesUpdated =
            await dbContext.UsernameHistory.AddMissingUsernameHistoryAsync(eventArgs.Guild);

        var nicknamesUpdated =
            await dbContext.NicknameHistory.AddMissingNickNameHistoryAsync(eventArgs.Guild);

        var avatarsUpdated =
            await dbContext.Avatars.AddMissingAvatarsHistoryAsync(eventArgs.Guild);

        this._inviteService.UpdateGuildInvites(
            new GuildInviteDto
            {
                GuildId = eventArgs.Guild.Id,
                Invites = new ConcurrentDictionary<string, Invite>((await eventArgs.Guild.GetInvitesAsync())
                    .Select(x => new Invite
                    {
                        Code = x.Code,
                        Inviter = x.Inviter.Username,
                        Url = x.ToString(),
                        Uses = x.Uses,
                        MaxUses = x.MaxUses
                    })
                    .ToDictionary(x => x.Code))
            });

        if (usernamesUpdated || nicknamesUpdated || avatarsUpdated)
            await dbContext.SaveChangesAsync();
    }
}
