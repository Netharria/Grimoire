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

public sealed partial class SettingsModule
{
    private const string LevelingCacheKeyPrefix = "LevelingSettings_{0}";

    public async Task<LevelingSettingEntry> GetLevelingSettings(ulong guildId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = string.Format(LevelingCacheKeyPrefix, guildId);
        return await this._memoryCache.GetOrCreateAsync(cacheKey, async _ =>
               {
                   await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
                   return await dbContext.LevelingSettings
                              .AsNoTracking()
                              .Where(settings => settings.GuildId == guildId)
                              .Select(settings => new LevelingSettingEntry
                              {
                                  Amount = settings.Amount,
                                  Base = settings.Base,
                                  Modifier = settings.Modifier,
                                  TextTime = settings.TextTime
                              }).FirstOrDefaultAsync(cancellationToken) ??
                          new LevelingSettingEntry
                          {
                              Amount = 5, Base = 15, Modifier = 50, TextTime = TimeSpan.FromMinutes(3)
                          };
               }, this._cacheEntryOptions) ??
               new LevelingSettingEntry { Amount = 5, Base = 15, Modifier = 50, TextTime = TimeSpan.FromMinutes(3) };
    }

    public async Task SetLevelingSettings(
        ulong guildId,
        LevelingSettingEntry levelingSettings,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingSettings = await dbContext.LevelingSettings
                                   .Where(settings => settings.GuildId == guildId)
                                   .FirstOrDefaultAsync(cancellationToken)
                               ?? new LevelingSettings { GuildId = guildId };

        existingSettings.Amount = levelingSettings.Amount;
        existingSettings.Base = levelingSettings.Base;
        existingSettings.Modifier = levelingSettings.Modifier;
        existingSettings.TextTime = levelingSettings.TextTime;

        await dbContext.LevelingSettings.AddAsync(existingSettings, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        this._memoryCache.Remove(string.Format(LevelingCacheKeyPrefix, guildId));
    }

    public sealed record LevelingSettingEntry
    {
        public TimeSpan TextTime { get; init; }
        public int Base { get; init; }
        public int Modifier { get; init; }
        public int Amount { get; init; }

        public int GetLevelFromXp(long xp)
        {
            var i = 0;
            if (xp > 1000)
                // This is to reduce the number of iterations. Minor inaccuracy is acceptable.
                // ReSharper disable once PossibleLossOfFraction
                i = (int)Math.Floor(Math.Sqrt((xp - Base) * 100 /
                                              (Base * Modifier)));
            while (true)
            {
                var xpNeeded = Base + (
                    (long)Math.Round(Base *
                                     (Modifier / 100.0) * i) * i);
                if (xp < xpNeeded)
                    return i + 1;

                i += 1;
            }
        }

        public long GetXpNeededForLevel(int level, int levelModifier = 0)
        {
            level = level - 2 + levelModifier;
            return level switch
            {
                < 0 => 0,
                0 => Base,
                _ => Base + ((long)Math.Round(Base * (Modifier / 100.0) * level) * level)
            };
        }
    }
}
