// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using DSharpPlus.Entities.AuditLogs;
using DSharpPlus.Exceptions;
using Grimoire.Features.Logging.UserLogging;

namespace Grimoire.Extensions;

public static class DiscordGuildExtensions
{
    extension(DiscordGuild guild)
    {
        /// <returns>
        ///     The guild's invites, or <c>null</c> when the bot lacks Manage Server, which listing them needs. That is
        ///     a normal setup, so it must not stop the caller from handling the guild in other ways.
        /// </returns>
        public async Task<GuildInviteDto?> GetGuildInvitesAsync()
            => guild.CurrentMember.Permissions.HasPermission(DiscordPermission.ManageGuild)
                ? new GuildInviteDto
                {
                    GuildId = guild.GetGuildId(),
                    Invites = new ConcurrentDictionary<InviteCode, Invite>((await guild.GetInvitesAsync())
                        .Select(invite => KeyValuePair.Create(invite.GetInviteCode(), invite.ToInvite())))
                }
                : null;

        public ValueTask<T?> GetRecentAuditLogAsync<T>(DiscordAuditLogActionType? actionType = null,
            int allowedTimeSpan = 500)
            where T : DiscordAuditLogEntry
            => guild.GetAuditLogsAsync(1, actionType: actionType)
                .OfType<T>()
                .FirstOrDefaultAsync(x =>
                    x.CreationTimestamp + TimeSpan.FromMilliseconds(allowedTimeSpan) > DateTime.UtcNow);

        public Task<DiscordRole?> GetRoleOrDefaultAsync(RoleId? roleId)
            => roleId is { } id
                ? guild.GetRoleOrDefaultAsync(id)
                : Task.FromResult<DiscordRole?>(null);

        /// <summary>Null only when Discord says the role doesn't exist. Any other failure, an outage included, throws.</summary>
        public async Task<DiscordRole?> GetRoleOrDefaultAsync(RoleId roleId)
        {
            try
            {
                return await guild.GetRoleAsync(roleId.Value);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        public Task<DiscordChannel?> GetChannelOrDefaultAsync(ChannelId? channelId)
            => channelId is { } id
                ? guild.GetChannelOrDefaultAsync(id)
                : Task.FromResult<DiscordChannel?>(null);

        /// <summary>Null only when Discord says the channel doesn't exist. Any other failure, an outage included, throws.</summary>
        public async Task<DiscordChannel?> GetChannelOrDefaultAsync(ChannelId channelId)
        {
            try
            {
                return await guild.GetChannelAsync(channelId.Value);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        public Task<DiscordMember?> GetMemberOrDefaultAsync(UserId? userId)
            => userId is { } id
                ? guild.GetMemberOrDefaultAsync(id)
                : Task.FromResult<DiscordMember?>(null);

        public async Task<DiscordMember?> GetMemberOrDefaultAsync(UserId userId)
        {
            try
            {
                return await guild.GetMemberAsync(userId.Value);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        [Pure]
        public GuildId GetGuildId() => new(guild.Id);
    }
}
