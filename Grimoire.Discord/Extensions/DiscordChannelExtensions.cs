// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Discord.Extensions;

public static class DiscordChannelExtensions
{
    public static async Task<int> PurgeMessagesAsync(
        this DiscordChannel channel,
        int count,
        string reason = "",
        Func<DiscordMessage, bool>? filter = null)
    {
        filter ??= message => true;
        var messages = await channel.GetMessagesAsync();
        var messagesToDelete = messages.Where(filter);
        while (messagesToDelete.Count() < count
            && messages.Count != 0
            && messages[^1].Timestamp > DateTimeOffset.UtcNow.AddDays(-14))
        {
            messages = await channel.GetMessagesBeforeAsync(messages[^1].Id);
            messagesToDelete = messagesToDelete.Concat(messages.Where(filter));
        }
        messagesToDelete = messagesToDelete.Where(x => x.Timestamp > DateTimeOffset.UtcNow.AddDays(-14))
            .Take(count);
        if (messagesToDelete.Count() == 1)
            await messagesToDelete.First().DeleteAsync(reason);
        if (messagesToDelete.Count() > 1)
            await messagesToDelete.Chunk(100).ToAsyncEnumerable()
                .ForEachAsync(async messages => await channel.DeleteMessagesAsync(messages, reason));
        return messagesToDelete.Count();
    }
}
