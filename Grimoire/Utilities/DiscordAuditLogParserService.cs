// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities.AuditLogs;
using DSharpPlus.Exceptions;
using Grimoire.Features.Shared.GuildIssues;
using Microsoft.Extensions.Caching.Hybrid;

namespace Grimoire.Utilities;

public interface IDiscordAuditLogParserService
{
    /// <param name="authorId">The deleted message's author, as stored.</param>
    Task<DiscordAuditLogMessageEntry?> ParseAuditLogForDeletedMessageAsync(GuildId guildId, ChannelId channelId,
        UserId authorId);
}

public sealed class DiscordAuditLogParserService(
    DiscordClient discordClient,
    HybridCache cache,
    GuildIssueReporter issueReporter) : IDiscordAuditLogParserService
{
    private static readonly HybridCacheEntryOptions _entryOptions = new() { Expiration = TimeSpan.FromMinutes(10) };

    public async Task<DiscordAuditLogMessageEntry?> ParseAuditLogForDeletedMessageAsync(GuildId guildId,
        ChannelId channelId,
        UserId authorId)
    {
        if (!discordClient.Guilds.TryGetValue(guildId.Value, out var guild))
            return null;
        if (!guild.CurrentMember.Permissions.HasPermission(DiscordPermission.ViewAuditLog))
        {
            await issueReporter.ReportAsync(GuildIssueCatalog.CannotReadAuditLog(guildId));
            return null;
        }

        DiscordAuditLogMessageEntry? deleteEntry;

        try
        {
            deleteEntry = await DiscordRetryPolicy.RetryDiscordCall(async token => await guild
                .GetAuditLogsAsync(10, actionType: DiscordAuditLogActionType.MessageDelete)
                .OfType<DiscordAuditLogMessageEntry>()
                .Where(x => x.Target.GetUserId() == authorId && x.Channel.GetChannelId() == channelId)
                .OrderByDescending(x => x.CreationTimestamp)
                .FirstOrDefaultAsync(token));
        }
        catch (UnauthorizedException)
        {
            await issueReporter.ReportAsync(GuildIssueCatalog.CannotReadAuditLog(guildId));
            return null;
        }
        catch (Exception)
        {
            return null;
        }

        if (deleteEntry is null
            || deleteEntry.CreationTimestamp < DateTime.UtcNow.AddMinutes(-10))
            return null;

        var key = GetDeleteAuditCacheKey(deleteEntry.Id);

        var cachedMessageCount = await cache.GetOrCreateAsync(
            key,
            static _ => ValueTask.FromResult(0),
            _entryOptions);

        if (deleteEntry.MessageCount <= cachedMessageCount)
            return null;

        await cache.SetAsync(key, deleteEntry.MessageCount, _entryOptions);
        return deleteEntry;
    }

    private static string GetDeleteAuditCacheKey(ulong auditEntryId) => $"audit-delete:{auditEntryId}";
}
