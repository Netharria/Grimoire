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
        filter ??= message => true;
        var oldTimestamp = DateTimeOffset.UtcNow.AddDays(-14);
        var messages = await channel.GetMessagesAsync().Where(filter).TakeWhile(x => x.Timestamp > oldTimestamp).Take(count).ToArrayAsync();
        if (messages.Count() == 1)
            await messages.First().DeleteAsync(reason);
        if (messages.Count() > 1)
            await messages.Chunk(100).ToAsyncEnumerable()
                .ForEachAsync(async messages => await channel.DeleteMessagesAsync(messages, reason));
        return messages.Count();
    }
}
