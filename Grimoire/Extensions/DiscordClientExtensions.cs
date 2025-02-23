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

    public static Task<DiscordMessage?> SendMessageToLoggingChannel(this DiscordClient client, ulong? logChannelId,
        Action<DiscordEmbedBuilder> discordEmbedBuilder)
        => SendMessageToLoggingChannel(client, logChannelId,
            builder =>
            {
                var embedBuilder = new DiscordEmbedBuilder();
                discordEmbedBuilder(embedBuilder);
                builder.AddEmbed(embedBuilder);
            });

    public static async Task<DiscordMessage?> SendMessageToLoggingChannel(this DiscordClient client,
        ulong? logChannelId, Action<DiscordMessageBuilder> discordMessageBuilder)
    {
        if (logChannelId is null)
            return null;
        var channel = await client.GetChannelOrDefaultAsync(logChannelId.Value);
        if (channel is null)
            return null;
        return await DiscordRetryPolicy.RetryDiscordCall(async _ =>
            await channel.SendMessageAsync(discordMessageBuilder));
    }

    public static Task<DiscordMessage?> SendMessageToLoggingChannel(this DiscordClient client, ulong? logChannelId,
        Func<DiscordEmbedBuilder, Task> discordEmbedBuilder)
        => SendMessageToLoggingChannel(client, logChannelId,
            async builder =>
            {
                var embedBuilder = new DiscordEmbedBuilder();
                await discordEmbedBuilder(embedBuilder);
                builder.AddEmbed(embedBuilder);
            });

    public static async Task<DiscordMessage?> SendMessageToLoggingChannel(this DiscordClient client,
        ulong? logChannelId, Func<DiscordMessageBuilder, Task> discordMessageBuilder)
    {
        if (logChannelId is null)
            return null;
        var channel = await client.GetChannelOrDefaultAsync(logChannelId.Value);
        if (channel is null)
            return null;
        var builder = new DiscordMessageBuilder();

        await discordMessageBuilder(builder);

        return await DiscordRetryPolicy.RetryDiscordCall(async _ =>
            await channel.SendMessageAsync(builder));
    }

    public static async Task<DiscordMessage?> SendMessageToLoggingChannel(this DiscordClient client,
        ulong? logChannelId, Func<Task<DiscordMessageBuilder>> discordMessageBuilder)
    {
        if (logChannelId is null)
            return null;
        var channel = await client.GetChannelOrDefaultAsync(logChannelId.Value);
        if (channel is null)
            return null;
        var builder = await discordMessageBuilder();

        return await DiscordRetryPolicy.RetryDiscordCall(async _ =>
            await channel.SendMessageAsync(builder));
    }

    public static async Task<string?> GetUserAvatar(this DiscordClient client, ulong userId, DiscordGuild? guild = null)
    {
        if (guild?.Members.TryGetValue(userId, out var member) is true)
            return member.GetGuildAvatarUrl(MediaFormat.Auto);
        var user = await client.GetUserAsync(userId);
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        return user?.GetAvatarUrl(MediaFormat.Auto);
    }
}
