// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Diagnostics;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Values;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Helpers;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    private static readonly FrozenSet<GuildSettingType> _levelingSettingKeys =
    [
        GuildSettingType.XpTimeoutPeriod,
        GuildSettingType.LevelScalingBase,
        GuildSettingType.LevelScalingModifier,
        GuildSettingType.XpGainAmount
    ];

    public async Task<Result<LevelingSettingEntry>> GetLevelingSettings(
        GuildId guildId,
        CancellationToken cancellationToken = default) =>
        Result<LevelingSettingEntry>.Ok(
            await this._cache.GetOrCreateAsync(
                CacheKey.LevelingSettings(guildId),
                guildId,
                GetLevelingSettingsCacheEntry,
                this._cacheEntryOptions,
                cancellationToken: cancellationToken));

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
        (
            XpTimeoutPeriod.FromDatabaseOrDefault(
                latestByKey.GetValueOrDefault(GuildSettingType.XpTimeoutPeriod)),
            LevelScalingModifier.FromDatabaseOrDefault(
                latestByKey.GetValueOrDefault(GuildSettingType.LevelScalingModifier)),
            LevelScalingBase.FromDatabaseOrDefault(
                latestByKey.GetValueOrDefault(GuildSettingType.LevelScalingBase)),
            XpGainAmount.FromDatabaseOrDefault(
                latestByKey.GetValueOrDefault(GuildSettingType.XpGainAmount))
        );
    }

    public Task<Result<int>> SetLevelingSettings(
        GuildId guildId,
        ModeratorId setBy,
        LevelSettings settingToChange,
        int newValue,
        CancellationToken cancellationToken = default)
        => CreateLevelingSetting(settingToChange, newValue)
            .MatchAsync(
                async x =>
                {
                    var (settingType, setting) = x;
                    if (GuildSettingCustomValue.Create(settingType, guildId, setBy, DateTimeOffset.UtcNow, setting)
                        is not Validation<GuildSettingCustomValue>.Valid(var customValue))
                        return Result<int>.Fail(new Error("guild-setting.invalid", "Invalid guild setting value."));
                    var result = await SetGuildSetting(customValue, cancellationToken);
                    if (result is Result<GuildSetting>.Success)
                        await this._cache.RemoveAsync(CacheKey.LevelingSettings(guildId), cancellationToken);
                    return result.Map(_ => newValue);
                },
                errors => Result<int>.Fail(errors));

    private static Validation<(GuildSettingType, string)> CreateLevelingSetting(LevelSettings setting, int value)
        => setting switch
        {
            LevelSettings.Amount => XpGainAmount.Create(value).ToDatabaseString()
                .Map(x => (GuildSettingType.XpGainAmount, x)),
            LevelSettings.Base => LevelScalingBase.Create(value).ToDatabaseString()
                .Map(x => (GuildSettingType.LevelScalingBase, x)),
            LevelSettings.Modifier => LevelScalingModifier.Create(value).ToDatabaseString()
                .Map(x => (GuildSettingType.LevelScalingModifier, x)),
            LevelSettings.XpTimeoutPeriod => XpTimeoutPeriod.Create(value).ToDatabaseString()
                .Map(x => (GuildSettingType.XpTimeoutPeriod, x)),
            _ => throw new UnreachableException()
        };

    public sealed record LevelingSettingEntry(
        XpTimeoutPeriod XpTimeoutPeriod,
        LevelScalingModifier Modifier,
        LevelScalingBase Base,
        XpGainAmount Amount)
    {
        public int GetLevelFromXp(long xp)
        {
            var i = 0;
            if (xp > 1000)
                // This is to reduce the number of iterations. Minor inaccuracy is acceptable.
                // ReSharper disable once PossibleLossOfFraction
                i = (int)Math.Floor(Math.Sqrt((xp - Base.Value) * 100 /
                                              (Base.Value * Modifier.Value)));
            while (true)
            {
                var xpNeeded = Base.Value + (
                    (long)Math.Round(Base.Value *
                                     (Modifier.Value / 100.0) * i) * i);
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
                0 => Base.Value,
                _ => Base.Value + ((long)Math.Round(Base.Value * (Modifier.Value / 100.0) * level) * level)
            };
        }
    }
}
