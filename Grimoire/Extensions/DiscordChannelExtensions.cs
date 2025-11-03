// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Extensions;

public static class DiscordChannelExtensions
{
    public static async Task<int> PurgeMessagesAsync(
        this DiscordChannel channel,
        int count,
        string reason = "",
        Func<DiscordMessage, bool>? filter = null)
    {
        filter ??= _ => true;
        var oldTimestamp = DateTimeOffset.UtcNow.AddDays(-14);
        var messages = await channel.GetMessagesAsync()
            .OrderByDescending(x => x.Timestamp)
            .Where(filter)
            .TakeWhile(x => x.Timestamp > oldTimestamp)
            .Take(count)
            .ToArrayAsync();
        switch (messages.Length)
        {
            case 1:
                await messages.First().DeleteAsync(reason);
                break;
            case > 1:
                await messages.Chunk(100).ToAsyncEnumerable()
                    .ForEachAwaitAsync(async messageChunk
                        => await channel.DeleteMessagesAsync(messageChunk, reason));
                break;
        }

        return messages.Length;
    }

    public static IEnumerable<KeyValuePair<ChannelId, ChannelId?>> BuildChannelTree(
        this DiscordChannel? channel)
    {
        while (channel is not null)
        {
            yield return new KeyValuePair<ChannelId, ChannelId?>(channel.GetChannelId(), channel.GetParentChannelId());
            channel = channel.Parent;
        }
    }

    public static Task<DiscordMessage?> GetMessageOrDefaultAsync(this DiscordChannel channel, MessageId? messageId)
        => messageId is { } id
            ? GetMessageOrDefaultAsync(channel, id)
            : Task.FromResult<DiscordMessage?>(null);

    public static async Task<DiscordMessage?> GetMessageOrDefaultAsync(this DiscordChannel channel, MessageId messageId)
    {
        try
        {
            return await channel.GetMessageAsync(messageId.Value);
        }
        catch (Exception)
        {
            return null;
        }
    }

    [Pure]
    public static ChannelId GetChannelId(this DiscordChannel channel) => new (channel.Id);
    [Pure]
    public static ChannelId? GetParentChannelId(this DiscordChannel channel) => channel.ParentId is not null ? new ChannelId(channel.ParentId.Value) : null;

    public static Task<DiscordMessage> GetMessageAsync(this DiscordChannel discordChannel, MessageId id, bool skipCache = false)
        => discordChannel.GetMessageAsync(id.Value, skipCache);
}
