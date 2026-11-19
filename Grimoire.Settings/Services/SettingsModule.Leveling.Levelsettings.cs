// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Documents;
using Grimoire.Settings.Domain.Values;
using Grimoire.Settings.Enums;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    /// <summary>
    ///     The leveling curve and timeout, with defaults for what the guild hasn't set. S-1: stored values are
    ///     trusted, so a legacy value outside today's limits keeps the guild's curve.
    /// </summary>
    public Task<Fin<LevelingSettingEntry>> GetLevelingSettings(
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => GetDocument<LevelingConfig>(guildId, cancellationToken).MapFin(leveling => leveling.Resolve());

    public Task<Fin<Write<int>>> SetLevelingSettings(
        GuildId guildId,
        ModeratorId setBy,
        LevelSettings settingToChange,
        int newValue,
        CancellationToken cancellationToken = default)
        => ApplyLevelingSetting(settingToChange, newValue).ToFin()
            .BindAsync(apply => UpdateDocument<LevelingConfig>(guildId, setBy, leveling => apply(leveling),
                cancellationToken))
            .MapFin(write => write.Map(_ => newValue));

    /// <summary>Checks the new value and gives the change to make to the document.</summary>
    private static Validation<Error, Func<LevelingConfig, LevelingConfig>> ApplyLevelingSetting(
        LevelSettings setting, int value)
        => setting switch
        {
            LevelSettings.Amount => XpGainAmount.Create(value)
                .Map<Func<LevelingConfig, LevelingConfig>>(x => leveling => leveling with { XpGainAmount = x }),
            LevelSettings.Base => LevelScalingBase.Create(value)
                .Map<Func<LevelingConfig, LevelingConfig>>(x => leveling => leveling with { LevelScalingBase = x }),
            LevelSettings.Modifier => LevelScalingModifier.Create(value)
                .Map<Func<LevelingConfig, LevelingConfig>>(x =>
                    leveling => leveling with { LevelScalingModifier = x }),
            LevelSettings.XpTimeoutPeriod => XpTimeoutPeriod.Create(value)
                .Map<Func<LevelingConfig, LevelingConfig>>(x => leveling => leveling with { XpTimeout = x }),
            _ => throw new InvariantViolationException()
        };
}
