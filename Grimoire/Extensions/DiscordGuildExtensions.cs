// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities.AuditLogs;

namespace Grimoire.Extensions;

public static class DiscordGuildExtensions
{
    public static async ValueTask<T?> GetRecentAuditLogAsync<T>(this DiscordGuild guild, DiscordAuditLogActionType? actionType = null, int allowedTimeSpan = 500)
        where T : DiscordAuditLogEntry
        => await guild.GetAuditLogsAsync(1, actionType: actionType)
            .OfType<T>()
            .FirstOrDefaultAsync(x => x.CreationTimestamp + TimeSpan.FromMilliseconds(allowedTimeSpan) > DateTime.UtcNow);
}
