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
    public static Task<DiscordChannel?> GetChannelOrDefaultAsync(this DiscordClient client, ChannelId? channelId)
        => channelId is not { } id
            ? Task.FromResult<DiscordChannel?>(null)
            : GetChannelOrDefaultAsync(client, id);

    public static async Task<DiscordChannel?> GetChannelOrDefaultAsync(this DiscordClient client, ChannelId channelId)
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

    public static Task<DiscordUser?> GetUserOrDefaultAsync(this DiscordClient client, UserId? userId)
        => userId is not { } id
            ? Task.FromResult<DiscordUser?>(null)
            : GetUserOrDefaultAsync(client, id);

    public static async Task<DiscordUser?> GetUserOrDefaultAsync(this DiscordClient client, UserId userId)
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

    public static Task<DiscordGuild?> GetGuildOrDefaultAsync(this DiscordClient client, GuildId? guildId)
        => guildId is not { } id
            ? Task.FromResult<DiscordGuild?>(null)
            : GetGuildOrDefaultAsync(client, id);

    public static async Task<DiscordGuild?> GetGuildOrDefaultAsync(this DiscordClient client, GuildId guildId)
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

    public static Task<string?> GetUserAvatar(this DiscordClient client, UserId? userId, DiscordGuild? guild = null)
        => userId is not { } id
            ? Task.FromResult<string?>(null)
            : GetUserAvatar(client, id, guild);


    public static async Task<string?> GetUserAvatar(this DiscordClient client, UserId userId, DiscordGuild? guild = null)
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

    public static Task<DiscordUser> GetUserAsync(this DiscordClient client, UserId userId, bool updateCache = false)
    => client.GetUserAsync(userId.Value, updateCache);
}
