// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Discord.Extensions;

public static class DiscordGuildExtensions
{
    public static async ValueTask<T?> GetRecentAuditLogAsync<T>(this DiscordGuild guild, AuditLogActionType? actionType = null, int allowedTimeSpan = 500)
        where T : DiscordAuditLogEntry
        => (await guild.GetAuditLogsAsync(1, action_type: actionType)).OfType<T>()
            .FirstOrDefault(x => x.CreationTimestamp + TimeSpan.FromMilliseconds(allowedTimeSpan) > DateTime.UtcNow);
}
