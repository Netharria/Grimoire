// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using static LanguageExt.Prelude;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{

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
        await this._memoryCache.GetOrCreateAsync(GetLevelingCacheKey(guildId), async cacheEntry =>
        {
            cacheEntry.SetOptions(this._cacheEntryOptions);
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
                       }).FirstOrDefaultAsync(cancellationToken) ?? this._defaultLevelingSettings;
        }) ?? this._defaultLevelingSettings;

    public IO<LevelingSettingEntry> GetLevelingSettingsTest(GuildId guildId,
        CancellationToken cancellationToken = default) =>
        liftIO(() => this._memoryCache.GetOrCreateAsync(GetLevelingCacheKey(guildId),
            async cacheEntry =>
            {
                cacheEntry.SetOptions(this._cacheEntryOptions);
                return await GetLevelingSettingsDb(guildId, cancellationToken).RunAsync().AsTask();
            }))
            .Map(result => result ?? this._defaultLevelingSettings);

    private IO<LevelingSettingEntry> GetLevelingSettingsDb(
        GuildId guildId,
        CancellationToken cancellationToken = default) =>
        from dbContext in useAsync(liftIO(() => this._dbContextFactory.CreateDbContextAsync(cancellationToken)))
        from settings in liftIO(() =>
                dbContext.LevelingSettings
                    .Where(settings => settings.GuildId == guildId)
                    .FirstOrDefaultAsync(cancellationToken)
                    .Map(result =>
                        result ?? GetDefaultLevelingSettings(guildId)))
            .Map(settings => new LevelingSettingEntry
                {
                    Amount = settings.Amount,
                    Base = settings.Base,
                    Modifier = settings.Modifier,
                    TextTime = settings.TextTime
                })
        from _ in release(dbContext)
        select settings;

    public async Task SetLevelingSettings(
        GuildId guildId,
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
        this._memoryCache.Remove(GetLevelingCacheKey(guildId));
    }

    public Eff<Unit> SetLevelingSettingsTest(
        GuildId guildId,
        LevelingSettingEntry levelingSettings,
        CancellationToken cancellationToken = default) =>
        from dbContext in useAsync(liftIO(() => this._dbContextFactory.CreateDbContextAsync(cancellationToken)))
        from io in liftIO(() =>
                dbContext.LevelingSettings
                    .Where(settings => settings.GuildId == guildId)
                    .FirstOrDefaultAsync(cancellationToken))
                .Map(settings => settings ?? GetDefaultLevelingSettings(guildId))
                .Map(settings =>
                    {
                        settings.Amount = levelingSettings.Amount;
                        settings.Base = levelingSettings.Base;
                        settings.Modifier = levelingSettings.Modifier;
                        settings.TextTime = levelingSettings.TextTime;
                        return settings;
                    })
                .Bind(settings => liftIO(() => dbContext.LevelingSettings.AddAsync(settings, cancellationToken).AsTask()))
                .Action(liftIO(() => dbContext.SaveChangesAsync(cancellationToken)))
                .Bind(_ => liftEff(() => this._memoryCache.Remove(GetLevelingCacheKey(guildId))))
                .As()
        from _ in release(dbContext)
        select io;

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
