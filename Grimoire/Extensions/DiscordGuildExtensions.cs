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
    public static ValueTask<T?> GetRecentAuditLogAsync<T>(this DiscordGuild guild,
        DiscordAuditLogActionType? actionType = null, int allowedTimeSpan = 500)
        where T : DiscordAuditLogEntry
        => guild.GetAuditLogsAsync(1, actionType: actionType)
            .OfType<T>()
            .FirstOrDefaultAsync(x =>
                x.CreationTimestamp + TimeSpan.FromMilliseconds(allowedTimeSpan) > DateTime.UtcNow);

    public static async Task<DiscordRole?> GetRoleOrDefaultAsync(this DiscordGuild guild, ulong roleId)
    {
        try
        {
            return await guild.GetRoleAsync(roleId);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task<DiscordChannel?> GetChannelOrDefaultAsync(this DiscordGuild guild, ulong channelId)
    {
        try
        {
            return await guild.GetChannelAsync(channelId);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
