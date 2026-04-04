// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Diagnostics.Contracts;
using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public enum LevelSettings
    {
        TextTime,
        Base,
        Modifier,
        Amount
    }

    private readonly LevelingSettingEntry _defaultLevelingSettings = new()
    {
        Amount = new XpGainAmount(5),
        Base = new LevelScalingBase(15),
        Modifier = new LevelScalingModifier(15),
        TextTime = TimeSpan.FromMinutes(3)
    };

    private static LevelingSettings GetDefaultLevelingSettings(GuildId guildId) =>
        new()
        {
            GuildId = guildId,
            Amount = new XpGainAmount(5),
            Base = new LevelScalingBase(15),
            Modifier = new LevelScalingModifier(15),
            TextTime = TimeSpan.FromMinutes(3)
        };

    private static string GetLevelingCacheKey(GuildId guildId) => $"LevelingSettings_{guildId}";

    public async Task<LevelingSettingEntry> GetLevelingSettings(GuildId guildId,
        CancellationToken cancellationToken = default) =>
        await this._memoryCache.GetOrCreateAsync(GetLevelingCacheKey(guildId),
                cacheEntry =>
                {
                    cacheEntry.SetOptions(this._cacheEntryOptions);
                    return GetLevelingSettingsCacheEntry(guildId, cancellationToken);
                })
        ?? this._defaultLevelingSettings;

    private async Task<LevelingSettingEntry> GetLevelingSettingsCacheEntry(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settingsOption = await dbContext.LevelingSettings
            .Where(settings => settings.GuildId == guildId)
            .FirstOrDefaultAsync(cancellationToken);
        return settingsOption switch
        {
            not null => new LevelingSettingEntry
            {
                Amount = settingsOption.Amount,
                Base = settingsOption.Base,
                Modifier = settingsOption.Modifier,
                TextTime = settingsOption.TextTime
            },
            _ => this._defaultLevelingSettings
        };
    }



    public async Task SetLevelingSettings(
        LevelSettings levelingSettings,
        int value,
        GuildId guildId,
        CancellationToken cancellationToken = default) {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settingsResult = await dbContext.LevelingSettings
            .Where(settings => settings.GuildId == guildId)
            .FirstOrDefaultAsync(cancellationToken);

        var settings = await AddLevelSettingsIfNull(settingsResult, dbContext, guildId);
        settings = UpdateLevelSettings(settings, levelingSettings, value);
        await dbContext.SaveChangesAsync(cancellationToken);
        this._memoryCache.Remove(GetLevelingCacheKey(guildId));
    }


    private static async Task<LevelingSettings> AddLevelSettingsIfNull(LevelingSettings? levelingSettings,
        SettingsDbContext dbContext, GuildId guildId)
    {
        if (levelingSettings is not null)
            return levelingSettings;

        levelingSettings = GetDefaultLevelingSettings(guildId);
        await dbContext.LevelingSettings.AddAsync(levelingSettings);
        return levelingSettings;
    }

    private static LevelingSettings UpdateLevelSettings(
        LevelingSettings levelingSettings,
        LevelSettings levelSettings,
        int value)
    {
        GetLevelSettingUpdater(levelSettings, value)(levelingSettings);
        return levelingSettings;
    }

    [Pure]
    private static Action<LevelingSettings> GetLevelSettingUpdater(
        LevelSettings levelSettings,
        int value) =>
        levelSettings switch
        {
            LevelSettings.Amount => settings => settings.Amount = new XpGainAmount(value),
            LevelSettings.Base => settings => settings.Base = new LevelScalingBase(value),
            LevelSettings.Modifier => settings => settings.Modifier = new LevelScalingModifier(value),
            LevelSettings.TextTime => settings => settings.TextTime = TimeSpan.FromMinutes(value),
            _ => _ => { }
        };

    public sealed record LevelingSettingEntry
    {
        public TimeSpan TextTime { get; init; }
        public LevelScalingBase Base { get; init; }
        public LevelScalingModifier Modifier { get; init; }
        public XpGainAmount Amount { get; init; }

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
