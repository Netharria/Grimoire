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

    public static IEnumerable<ulong> BuildChannelTree(this DiscordChannel? channel)
    {
        while (channel is not null)
        {
            yield return channel.Id;
            channel = channel.Parent;
        }
    }
}
