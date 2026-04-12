// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities.AuditLogs;
using Microsoft.Extensions.Caching.Hybrid;

namespace Grimoire.Utilities;

public interface IDiscordAuditLogParserService
{
    Task<DiscordAuditLogMessageEntry?> ParseAuditLogForDeletedMessageAsync(GuildId guildId, ChannelId channelId,
        MessageId messageId);
}

public sealed class DiscordAuditLogParserService(
    DiscordClient discordClient,
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    HybridCache cache) : IDiscordAuditLogParserService
{
    private static readonly HybridCacheEntryOptions _entryOptions = new() { Expiration = TimeSpan.FromMinutes(10) };
    private readonly HybridCache _cache = cache;
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly DiscordClient _discordClient = discordClient;

    public async Task<DiscordAuditLogMessageEntry?> ParseAuditLogForDeletedMessageAsync(GuildId guildId,
        ChannelId channelId,
        MessageId messageId)
    {
        if (!this._discordClient.Guilds.TryGetValue(guildId.Value, out var guild)
            || !guild.CurrentMember.Permissions.HasPermission(DiscordPermission.ViewAuditLog))
            return null;
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        var result = await dbContext.Messages
            .Where(messages => messages.Id == messageId)
            .Select(message => new { message.UserId })
            .FirstOrDefaultAsync();

        if (result is null)
            return null;

        DiscordAuditLogMessageEntry? deleteEntry;

        try
        {
            deleteEntry = await DiscordRetryPolicy.RetryDiscordCall(async token => await guild
                .GetAuditLogsAsync(10, actionType: DiscordAuditLogActionType.MessageDelete)
                .OfType<DiscordAuditLogMessageEntry>()
                .Where(x => x.Target.GetUserId() == result.UserId && x.Channel.GetChannelId() == channelId)
                .OrderByDescending(x => x.CreationTimestamp)
                .FirstOrDefaultAsync(token));
        }
        catch (Exception)
        {
            return null;
        }

        if (deleteEntry is null
            || deleteEntry.CreationTimestamp < DateTime.UtcNow.AddMinutes(-10))
            return null;

        var key = GetDeleteAuditCacheKey(deleteEntry.Id);

        var cachedMessageCount = await this._cache.GetOrCreateAsync(
            key,
            static _ => ValueTask.FromResult(0),
            _entryOptions);

        if (deleteEntry.MessageCount <= cachedMessageCount)
            return null;

        await this._cache.SetAsync(key, deleteEntry.MessageCount, _entryOptions);
        return deleteEntry;
    }

    private static string GetDeleteAuditCacheKey(ulong auditEntryId) => $"audit-delete:{auditEntryId}";
}
