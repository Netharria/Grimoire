// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Extensions;

public static class DiscordClientExtensions
{
    public static async Task<DiscordChannel?> GetChannelOrDefaultAsync(this DiscordClient client, ulong channelId)
    {
        try
        {
            return await client.GetChannelAsync(channelId);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task<DiscordUser?> GetUserOrDefaultAsync(this DiscordClient client, ulong userId)
    {
        try
        {
            return await client.GetUserAsync(userId);
        }
        catch (Exception)
        {
            return null;
        }
    }


    public static async Task<string?> GetUserAvatar(this DiscordClient client, ulong userId, DiscordGuild? guild = null)
    {
        if (guild?.Members.TryGetValue(userId, out var member) is true)
            return member.GetGuildAvatarUrl(MediaFormat.Auto);
        var user = await client.GetUserOrDefaultAsync(userId);
        return user?.GetAvatarUrl(MediaFormat.Auto);
    }
}
