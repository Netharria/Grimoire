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
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Result<LevelingSettingEntry>.Ok(
                await this._cache.GetOrCreateAsync(
                    CacheKey.LevelingSettings(guildId),
                    guildId,
                    GetLevelingSettingsCacheEntry,
                    this._cacheEntryOptions,
                    cancellationToken: cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Result<LevelingSettingEntry>.Fail(new Error(
                "levelsettings.invalid",
                $"One or more leveling settings are invalid: {ex.Message}. Please reset them using the /leveling settings commands."));
        }
        catch (Exception ex)
        {
            LogOperationFailure(this._logger, ex.Message, ex);
            return Result<LevelingSettingEntry>.Fail(new Error(
                "levelsettings.fetch.failed",
                "Could not fetch level settings from cache"));
        }
    }


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

        return Validation.Combine(
            XpTimeoutPeriod.FromDatabase(latestByKey.GetValueOrDefault(GuildSettingType.XpTimeoutPeriod)),
            LevelScalingModifier.FromDatabase(latestByKey.GetValueOrDefault(GuildSettingType.LevelScalingModifier)),
            LevelScalingBase.FromDatabase(latestByKey.GetValueOrDefault(GuildSettingType.LevelScalingBase)),
            XpGainAmount.FromDatabase(latestByKey.GetValueOrDefault(GuildSettingType.XpGainAmount)))
            .Map(v => new LevelingSettingEntry(v.Item1, v.Item2, v.Item3, v.Item4))
            .Match(
                entry => entry,
                errors => throw new InvalidOperationException(
                    string.Join("; ", errors.Select(e => e.Message))));
    }

    public Task<Result<int>> SetLevelingSettings(
        GuildId guildId,
        ModeratorId setBy,
        LevelSettings settingToChange,
        int newValue,
        CancellationToken cancellationToken = default)
        => CreateLevelingSetting(settingToChange, newValue)
            .Bind(setting =>
                GuildSettingCustomValue.Create(setting.Item1, guildId, setBy, DateTimeOffset.UtcNow, setting.Item2))
            .ToResult()
            .BindAsync(setting => SetGuildSetting(setting, cancellationToken))
            .TapAsync(async _ => await this._cache.RemoveAsync(CacheKey.LevelingSettings(guildId), cancellationToken))
            .Map(_ => newValue);

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
}
