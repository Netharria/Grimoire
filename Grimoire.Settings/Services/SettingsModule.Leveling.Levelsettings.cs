// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Globalization;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Helpers;

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

    private static readonly FrozenSet<GuildSettingType> _levelingSettingKeys =
    [
        GuildSettingType.TextTime,
        GuildSettingType.LevelScalingBase,
        GuildSettingType.LevelScalingModifier,
        GuildSettingType.XpGainAmount
    ];

    private readonly LevelingSettingEntry _defaultLevelingSettings = new()
    {
        Amount = new XpGainAmount(5),
        Base = new LevelScalingBase(15),
        Modifier = new LevelScalingModifier(50),
        TextTime = TimeSpan.FromMinutes(3)
    };

    public async Task<LevelingSettingEntry> GetLevelingSettings(
        GuildId guildId,
        CancellationToken cancellationToken = default) =>
        await this._cache.GetOrCreateAsync(
            CacheKey.LevelingSettings(guildId),
            guildId,
            GetLevelingSettingsCacheEntry,
            this._cacheEntryOptions,
            cancellationToken: cancellationToken);

    private async ValueTask<LevelingSettingEntry> GetLevelingSettingsCacheEntry(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        var latestByKey =
            await GetGuildSettings(guildId, _levelingSettingKeys, cancellationToken)
                .ToDictionaryAsync(x => x.Type, x => x switch
                {
                    GuildSettingCustomValue customValue => customValue.Value,
                    _ => null
                }, cancellationToken: cancellationToken);

        return new LevelingSettingEntry
        {
            Amount = TryReadPositiveInt(latestByKey, GuildSettingType.XpGainAmount, out var amount)
                ? new XpGainAmount(amount)
                : this._defaultLevelingSettings.Amount,
            Base = TryReadPositiveInt(latestByKey, GuildSettingType.LevelScalingBase, out var @base)
                ? new LevelScalingBase(@base)
                : this._defaultLevelingSettings.Base,
            Modifier = TryReadPositiveInt(latestByKey, GuildSettingType.LevelScalingModifier, out var modifier)
                ? new LevelScalingModifier(modifier)
                : this._defaultLevelingSettings.Modifier,
            TextTime = TryReadValidTextTime(latestByKey, CultureInfo.InvariantCulture, out var timeSpan)
                ? timeSpan
                : this._defaultLevelingSettings.TextTime
        };

        static bool TryReadValidTextTime(
            IReadOnlyDictionary<GuildSettingType, string?> map,
            CultureInfo cultureInfo,
            out TimeSpan value)
        {
            if (!map.TryGetValue(GuildSettingType.TextTime, out var timeSpanStr)
                || !TimeSpan.TryParse(timeSpanStr, cultureInfo, out var timeSpan)
                || timeSpan <= TimeSpan.Zero
                || timeSpan.TotalMinutes > 60)
            {
                value = TimeSpan.Zero;
                return false;
            }

            value = timeSpan;
            return true;
        }

        static bool TryReadPositiveInt(
            IReadOnlyDictionary<GuildSettingType, string?> map,
            GuildSettingType key,
            out int value)
        {
            value = 0;
            return map.TryGetValue(key, out var str)
                   && str is not null
                   && int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                   && value >= 1;
        }
    }

    public async Task<SettingsResult> SetLevelingSettings(
        GuildId guildId,
        ModeratorId setBy,
        LevelSettings settingToChange,
        int newValue,
        CancellationToken cancellationToken = default)
    {
        if (!LevelingConfigValid(settingToChange, newValue))
            return SettingsResult.Invalid($"{settingToChange} value {newValue} is out of range.");

        var result = await SetGuildSetting(
            new GuildSettingCustomValue
            {
                GuildId = guildId,
                Type = settingToChange switch
                {
                    LevelSettings.TextTime => GuildSettingType.TextTime,
                    LevelSettings.Base => GuildSettingType.LevelScalingBase,
                    LevelSettings.Modifier => GuildSettingType.LevelScalingModifier,
                    LevelSettings.Amount => GuildSettingType.XpGainAmount,
                    _ => throw new ArgumentOutOfRangeException(nameof(settingToChange), settingToChange, null)
                },
                SetBy = setBy,
                SetAt = DateTimeOffset.UtcNow,
                Value = settingToChange switch
                {
                    LevelSettings.TextTime => TimeSpan.FromMinutes(newValue)
                        .ToString("c", CultureInfo.InvariantCulture),
                    _ => newValue.ToString(CultureInfo.InvariantCulture)
                }
            }, cancellationToken);

        if (result is SettingsWritten)
            await this._cache.RemoveAsync(CacheKey.LevelingSettings(guildId), cancellationToken);

        return result;
    }

    private static bool LevelingConfigValid(LevelSettings settingToValidate, int setting)
        => settingToValidate switch
        {
            LevelSettings.Amount => setting is >= 1 and <= 100,
            LevelSettings.Base => setting is >= 1 and <= 500,
            LevelSettings.Modifier => setting is >= 1 and <= 200,
            LevelSettings.TextTime => setting is >= 1 and <= 60,
            _ => false
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
