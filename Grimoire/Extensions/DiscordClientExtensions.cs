// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;

namespace Grimoire.Extensions;

public static class DiscordClientExtensions
{
    extension(DiscordClient client)
    {
        public Task<DiscordChannel?> GetChannelOrDefaultAsync(ChannelId? channelId,
            CancellationToken ct = default)
            => channelId is not { } id
                ? Task.FromResult<DiscordChannel?>(null)
                : client.GetChannelOrDefaultAsync(id, ct);

        public async Task<DiscordChannel?> GetChannelOrDefaultAsync(ChannelId channelId,
            CancellationToken ct = default)
        {
            try
            {
                return await client.GetChannelAsync(channelId.Value);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        public Task<DiscordUser?> GetUserOrDefaultAsync(UserId? userId)
            => userId is not { } id
                ? Task.FromResult<DiscordUser?>(null)
                : client.GetUserOrDefaultAsync(id);

        public async Task<DiscordUser?> GetUserOrDefaultAsync(UserId userId)
        {
            try
            {
                return await client.GetUserAsync(userId.Value);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        public Task<DiscordGuild?> GetGuildOrDefaultAsync(GuildId? guildId)
            => guildId is not { } id
                ? Task.FromResult<DiscordGuild?>(null)
                : client.GetGuildOrDefaultAsync(id);

        public async Task<DiscordGuild?> GetGuildOrDefaultAsync(GuildId guildId)
        {
            try
            {
                return await client.GetGuildAsync(guildId.Value);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        public Task<string?> GetUserAvatar(UserId? userId, DiscordGuild? guild = null)
            => userId is not { } id
                ? Task.FromResult<string?>(null)
                : client.GetUserAvatar(id, guild);

        public async Task<string?> GetUserAvatar(UserId userId,
            DiscordGuild? guild = null)
        {
            if (guild is not null)
            {
                var member = await guild.GetMemberOrDefaultAsync(userId);
                var guildAvatar = member?.GetGuildAvatarUrl(MediaFormat.Auto);
                if (guildAvatar is not null)
                    return guildAvatar;
            }

            var user = await client.GetUserOrDefaultAsync(userId);
            return user?.GetAvatarUrl(MediaFormat.Auto);
        }

        public Task<DiscordUser> GetUserAsync(UserId userId, bool updateCache = false)
            => client.GetUserAsync(userId.Value, updateCache);

        public ModeratorId GetGrimoireModeratorId() =>
            new ModeratorId(client.CurrentUser.Id);
    }
}
