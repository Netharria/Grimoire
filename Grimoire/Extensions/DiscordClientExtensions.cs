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
    public static Task<DiscordChannel?> GetChannelOrDefaultAsync(this DiscordClient client, ulong? channelId)
        => channelId is not { } id
            ? Task.FromResult<DiscordChannel?>(null)
            : GetChannelOrDefaultAsync(client, id);

    public static async Task<DiscordChannel?> GetChannelOrDefaultAsync(this DiscordClient client, ulong channelId)
    {
        try
        {
            return await client.GetChannelAsync(channelId);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public static Task<DiscordUser?> GetUserOrDefaultAsync(this DiscordClient client, ulong? userId)
        => userId is not { } id
            ? Task.FromResult<DiscordUser?>(null)
            : GetUserOrDefaultAsync(client, id);

    public static async Task<DiscordUser?> GetUserOrDefaultAsync(this DiscordClient client, ulong userId)
    {
        try
        {
            return await client.GetUserAsync(userId);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public static Task<DiscordGuild?> GetGuildOrDefaultAsync(this DiscordClient client, ulong? guildId)
        => guildId is not { } id
            ? Task.FromResult<DiscordGuild?>(null)
            : GetGuildOrDefaultAsync(client, id);

    public static async Task<DiscordGuild?> GetGuildOrDefaultAsync(this DiscordClient client, ulong guildId)
    {
        try
        {
            return await client.GetGuildAsync(guildId);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public static Task<string?> GetUserAvatar(this DiscordClient client, ulong? userId, DiscordGuild? guild = null)
        => userId is not { } id
            ? Task.FromResult<string?>(null)
            : GetUserAvatar(client, id, guild);


    public static async Task<string?> GetUserAvatar(this DiscordClient client, ulong userId, DiscordGuild? guild = null)
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
}
