// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    private static TimeSpan GetDefaultAutoPardonDuration() => TimeSpan.FromDays(10950);

    public Task<Result<TimeSpan>> GetAutoPardonDuration(GuildId guildId,
        CancellationToken cancellationToken = default) =>
        GetGuildSetting(
            GuildSettingType.SinAutoPardonDuration,
            guildId,
            cancellationToken)
            .AsTask()
        .Map(cachedSetting =>
        {
            var span = cachedSetting switch
            {
                CachedDefaultSetting => GetDefaultAutoPardonDuration(),
                CachedCustomSetting customSetting => TimeSpan.Parse(customSetting.Value, CultureInfo.InvariantCulture),
                CachedDisabledSetting => GetDefaultAutoPardonDuration(),
                _ => throw new UnreachableException()
            };
            return span != TimeSpan.Zero ? span : GetDefaultAutoPardonDuration();
        });

    public Task<Result<TimeSpan>> SetAutoPardonDuration(
        GuildId guildId,
        ModeratorId moderatorId,
        TimeSpan autoPardonAfter,
        CancellationToken cancellationToken = default)
        => ApplyGuildSetting(GuildSettingCustomValue.Create(GuildSettingType.SinAutoPardonDuration, guildId, moderatorId,
                DateTimeOffset.UtcNow, autoPardonAfter.ToString("c", CultureInfo.InvariantCulture)), cancellationToken)
            .Map(_ => autoPardonAfter);

    public Task<Result<TimeSpan>> ResetAutoPardonDuration(
        GuildId guildId,
        ModeratorId moderatorId,
        CancellationToken cancellationToken = default)
        => ApplyGuildSetting(GuildSettingCustomValue.Create(GuildSettingType.SinAutoPardonDuration, guildId, moderatorId,
                DateTimeOffset.UtcNow, GetDefaultAutoPardonDuration().ToString("c", CultureInfo.InvariantCulture)),  cancellationToken)
            .Map(_ => GetDefaultAutoPardonDuration());
}
