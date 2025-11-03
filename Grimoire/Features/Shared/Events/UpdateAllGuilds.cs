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

internal sealed class UpdateAllGuilds(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    IInviteService inviteService) : IEventHandler<GuildDownloadCompletedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly IInviteService _inviteService = inviteService;

    public async Task HandleEventAsync(DiscordClient sender, GuildDownloadCompletedEventArgs eventArgs)
    {
        await sender.UpdateStatusAsync(new DiscordActivity($"{sender.Guilds.Count} servers.",
            DiscordActivityType.Watching));
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        var guildInvites = new List<GuildInviteDto>();

        var changesDetected = false;

        foreach (var guild in eventArgs.Guilds)
        {
            var usernamesUpdated =
                await dbContext.UsernameHistory.AddMissingUsernameHistoryAsync(guild.Value);

            var nicknamesUpdated =
                await dbContext.NicknameHistory.AddMissingNickNameHistoryAsync(guild.Value);

            var avatarsUpdated =
                await dbContext.Avatars.AddMissingAvatarsHistoryAsync(guild.Value);

            var invites = await guild.Value.GetInvitesAsync();
            guildInvites.Add(new GuildInviteDto
            {
                GuildId = guild.Value.GetGuildId(),
                Invites = new ConcurrentDictionary<InviteCode, Invite>(
                    invites
                        .Select(x => new Invite
                        {
                            Code = x.GetInviteCode(),
                            Inviter = x.Inviter.GetUsername(),
                            Url = x.GetInviteUrl(),
                            Uses = x.Uses,
                            MaxUses = x.MaxUses
                        })
                        .ToDictionary(x => x.Code))
            });
            changesDetected = changesDetected
                              || usernamesUpdated
                              || nicknamesUpdated
                              || avatarsUpdated;
        }

        this._inviteService.UpdateAllInvites(guildInvites);

        if (changesDetected)
            await dbContext.SaveChangesAsync();
    }
}
