// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities.AuditLogs;
using Grimoire.Features.Logging.MessageLogging;
using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Utilities;

public interface IDiscordAuditLogParserService
{
    Task<DiscordAuditLogMessageEntry?> ParseAuditLogForDeletedMessageAsync(ulong guildId, ulong channelId, ulong targetId);
}

public sealed class DiscordAuditLogParserService(DiscordClient discordClient, IMemoryCache memoryCache, IMediator mediator) : IDiscordAuditLogParserService
{
    private readonly DiscordClient _discordClient = discordClient;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly IMediator _mediator = mediator;

    public async Task<DiscordAuditLogMessageEntry?> ParseAuditLogForDeletedMessageAsync(ulong guildId, ulong channelId, ulong messageId)
    {
        if (!this._discordClient.Guilds.TryGetValue(guildId, out var guild)
            || !guild.CurrentMember.Permissions.HasPermission(DiscordPermissions.ViewAuditLog))
            return null;

        var result = await this._mediator.Send(new GetMessageAuthor.Query{ MessageId = messageId });

        if (result is null)
            return null;

        DiscordAuditLogMessageEntry? deleteEntry;

        try
        {
            deleteEntry = await DiscordRetryPolicy.RetryDiscordCall(
                async () => await guild.GetAuditLogsAsync(10, actionType: DiscordAuditLogActionType.MessageDelete)
                .OfType<DiscordAuditLogMessageEntry>()
                .Where(x => x.Target.Id == result && x.Channel.Id == channelId)
                .OrderByDescending(x => x.CreationTimestamp)
                .FirstOrDefaultAsync());
        }
        catch (Exception)
        {
            return null;
        }

        if (deleteEntry is null
            || deleteEntry.CreationTimestamp < DateTime.UtcNow.AddMinutes(-10))
            return null;

        if (this._memoryCache.TryGetValue(deleteEntry.Id, out DiscordAuditLogMessageEntry? cachedEntry))
            if (cachedEntry is null
                || deleteEntry.MessageCount <= cachedEntry.MessageCount)
                return null;

        this._memoryCache.Set(deleteEntry.Id, deleteEntry, TimeSpan.FromMinutes(10));
        return deleteEntry;
    }
}
