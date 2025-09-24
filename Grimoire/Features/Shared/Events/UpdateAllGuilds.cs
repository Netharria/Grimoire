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
        var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        var changesDetected = await dbContext.Guilds.AddMissingGuildsAsync(eventArgs.Guilds.Keys.ToList());

        var guildInvites = new List<GuildInviteDto>();

        foreach (var guild in eventArgs.Guilds)
        {
            var usersAdded = await dbContext.Users.AddMissingUsersAsync(guild.Value);

            var rolesAdded = await dbContext.Roles.AddMissingRolesAsync(guild.Value);

            var channelsAdded =
                await dbContext.Channels.AddMissingChannelsAsync(guild.Value);

            var membersAdded =
                await dbContext.Members.AddMissingMembersAsync(guild.Value);

            var usernamesUpdated =
                await dbContext.UsernameHistory.AddMissingUsernameHistoryAsync(guild.Value);

            var nicknamesUpdated =
                await dbContext.NicknameHistory.AddMissingNickNameHistoryAsync(guild.Value);

            var avatarsUpdated =
                await dbContext.Avatars.AddMissingAvatarsHistoryAsync(guild.Value);

            var invites = await guild.Value.GetInvitesAsync();
            guildInvites.Add(new GuildInviteDto
            {
                GuildId = guild,
                Invites = new ConcurrentDictionary<string, Invite>(
                    invites
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
            changesDetected = changesDetected
                              || usersAdded
                              || rolesAdded
                              || channelsAdded
                              || membersAdded
                              || usernamesUpdated
                              || nicknamesUpdated
                              || avatarsUpdated;
        }

        this._inviteService.UpdateAllInvites(guildInvites);

        if (changesDetected)
            await dbContext.SaveChangesAsync();
    }
}
