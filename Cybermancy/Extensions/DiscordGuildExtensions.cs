// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;

namespace Cybermancy.Extensions
{
    public static class DiscordGuildExtensions
    {
        public static async Task<DiscordAuditLogEntry?> GetRecentAuditLogAsync(this DiscordGuild guild, AuditLogActionType? actionType = null)
        {
            var auditLogEntries = await guild.GetAuditLogsAsync(1, action_type: actionType);
            if(!auditLogEntries.Any())
                return null;
            var auditLogEntry = auditLogEntries[0];
            if(auditLogEntry.CreationTimestamp + TimeSpan.FromMilliseconds(500) > DateTime.UtcNow)
                return auditLogEntry;
            return null;
        }
    }
}
