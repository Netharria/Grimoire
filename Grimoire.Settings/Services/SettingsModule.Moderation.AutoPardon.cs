// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Settings.Services;

public partial class SettingsModule
{
    private static string GetAutoPardonCacheKey(ulong guildId)
        => $"AutoPardon-{guildId}";

    public async Task<TimeSpan> GetAutoPardonDuration(ulong guildId, CancellationToken cancellationToken = default)
    {
        return await this._memoryCache.GetOrCreateAsync(GetAutoPardonCacheKey(guildId), async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(2);
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var autoPardonDuration = await dbContext.ModerationSettings
                .AsNoTracking()
                .Where(settings => settings.GuildId == guildId)
                .Select(settings => settings.AutoPardonAfter)
                .FirstOrDefaultAsync(cancellationToken);
            return autoPardonDuration == TimeSpan.Zero ? TimeSpan.FromDays(30 * 365) : autoPardonDuration;
        });
    }

    public async Task SetAutoPardonDuration(
        ulong guildId,
        TimeSpan autoPardonAfter,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingSettings = await dbContext.ModerationSettings
                                   .Where(settings => settings.GuildId == guildId)
                                   .FirstOrDefaultAsync(cancellationToken)
                               ?? new ModerationSettings { GuildId = guildId };

        existingSettings.AutoPardonAfter = autoPardonAfter;

        await dbContext.ModerationSettings.AddAsync(existingSettings, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        this._memoryCache.Remove(GetAutoPardonCacheKey(guildId));
    }
}
