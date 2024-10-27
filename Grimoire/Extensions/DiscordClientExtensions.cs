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

    public static Task<DiscordMessage?> SendMessageToLoggingChannel(this DiscordClient client, ulong? logChannelId, DiscordEmbedBuilder discordEmbedBuilder)
        => SendMessageToLoggingChannel(client, logChannelId, new DiscordMessageBuilder().AddEmbed(discordEmbedBuilder));

    public static async Task<DiscordMessage?> SendMessageToLoggingChannel(this DiscordClient client, ulong? logChannelId, DiscordMessageBuilder discordMessageBuilder)
    {
        if (logChannelId is not ulong loggingChannelId)
            return null;
        var channel = await client.GetChannelOrDefaultAsync(loggingChannelId);
        if (channel is null)
            return null;
        return await DiscordRetryPolicy.RetryDiscordCall(async token => await channel.SendMessageAsync(discordMessageBuilder));

    }

    public static async Task<string?> GetUserAvatar(this DiscordClient client, ulong userId, DiscordGuild? guild = null)
    {
        if (guild?.Members.TryGetValue(userId, out var member) is true)
        {
            return member.GetGuildAvatarUrl(ImageFormat.Auto);
        }
        var user = await client.GetUserAsync(userId);
        return user?.GetAvatarUrl(ImageFormat.Auto);
    }
}
