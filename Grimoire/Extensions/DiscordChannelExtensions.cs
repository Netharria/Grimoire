// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;

namespace Grimoire.Extensions;

public static class DiscordChannelExtensions
{
    /// <summary>How far back a filtered purge looks. Unfiltered purges read exactly the number they're asked to delete.</summary>
    private const int FilteredPurgeScanLimit = 1000;

    /// <summary>Stops a message from reaching Discord's 14-day bulk delete limit between being read and being deleted.</summary>
    private static readonly TimeSpan _bulkDeleteAgeMargin = TimeSpan.FromMinutes(5);

    public static async Task<int> PurgeMessagesAsync(
        this DiscordChannel channel,
        int count,
        string reason = "",
        Func<DiscordMessage, bool>? filter = null)
    {
        var oldTimestamp = DateTimeOffset.UtcNow.AddDays(-14) + _bulkDeleteAgeMargin;
        var messages = await channel.GetMessagesAsync(filter is null ? count : FilteredPurgeScanLimit)
            .TakeWhile(x => x.Timestamp > oldTimestamp)
            .Where(filter ?? (_ => true))
            .Take(count)
            .ToArrayAsync();
        // Bulk delete takes 2 to 100 messages, so a chunk of one is deleted on its own.
        foreach (var messageChunk in messages.Chunk(100))
            if (messageChunk.Length == 1)
                await messageChunk[0].DeleteAsync(reason);
            else
                await channel.DeleteMessagesAsync(messageChunk, reason);

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

    extension(DiscordChannel channel)
    {
        public Task<DiscordMessage?> GetMessageOrDefaultAsync(MessageId? messageId)
            => messageId is { } id
                ? channel.GetMessageOrDefaultAsync(id)
                : Task.FromResult<DiscordMessage?>(null);

        /// <summary>Null only when Discord says the message doesn't exist. Any other failure, an outage included, throws.</summary>
        public async Task<DiscordMessage?> GetMessageOrDefaultAsync(MessageId messageId)
        {
            try
            {
                return await channel.GetMessageAsync(messageId.Value);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        [Pure]
        public ChannelId GetChannelId() => new(channel.Id);

        [Pure]
        public ChannelId? GetParentChannelId() =>
            channel.ParentId is not null ? new ChannelId(channel.ParentId.Value) : null;

        public Task<DiscordMessage> GetMessageAsync(MessageId id,
            bool skipCache = false)
            => channel.GetMessageAsync(id.Value, skipCache);
    }
}
