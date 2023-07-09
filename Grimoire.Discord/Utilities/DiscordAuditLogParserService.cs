// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Core.Features.Logging.Queries.GetMessageAuthor;
using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Discord.Utilities;

public interface IDiscordAuditLogParserService
{
    Task<DiscordAuditLogMessageEntry?> ParseAuditLogForDeletedMessageAsync(ulong guildId, ulong targetId);
}

public class DiscordAuditLogParserService : IDiscordAuditLogParserService
{
    private readonly IDiscordClientService _discordClientService;
    private readonly IMemoryCache _memoryCache;
    private readonly IMediator _mediator;

    public DiscordAuditLogParserService(IDiscordClientService discordClientService, IMemoryCache memoryCache, IMediator mediator)
    {
        this._discordClientService = discordClientService;
        this._memoryCache = memoryCache;
        this._mediator = mediator;
    }

    public async Task<DiscordAuditLogMessageEntry?> ParseAuditLogForDeletedMessageAsync(ulong guildId, ulong messageId)
    {
        if (!this._discordClientService.Client.Guilds.TryGetValue(guildId, out var guild))
            return null;

        IReadOnlyList<DiscordAuditLogEntry> auditLogEntries = new List<DiscordAuditLogEntry>();

        for(var i = 1; i <= 3; i++)
        {
            try
            {
                auditLogEntries = await guild.GetAuditLogsAsync(10, action_type: AuditLogActionType.MessageDelete);
                break;
            }
            catch(ServerErrorException ex) when (ex.WebResponse.ResponseCode == 502)
            {
                if (i < 3)
                    await Task.Delay(500 * i);
                else
                    return null;
            }
        }

        if (!auditLogEntries.Any()) return null;

        var result = await this._mediator.Send(new GetMessageAuthorQuery{ MessageId = messageId });

        if (result is null) return null;

        var deleteEntry = auditLogEntries
            .OrderByDescending(x => x.CreationTimestamp)
            .OfType<DiscordAuditLogMessageEntry>()
            .Where(x => x.Target.Id == result)
            .FirstOrDefault();

        if (deleteEntry is null) return null;

        if (_memoryCache.TryGetValue(deleteEntry.Id, out DiscordAuditLogMessageEntry? cachedEntry))
        {
            if (cachedEntry is null)
                return null;

            if (deleteEntry.MessageCount <= cachedEntry.MessageCount)
                return null;
        }

        _memoryCache.Set(deleteEntry.Id, deleteEntry, TimeSpan.FromMinutes(10));
        return deleteEntry;
    }
}
