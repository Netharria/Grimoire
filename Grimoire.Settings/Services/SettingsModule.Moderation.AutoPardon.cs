// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Globalization;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Helpers;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    private static TimeSpan GetDefaultAutoPardonDuration() => TimeSpan.FromDays(10950);

    public async Task<TimeSpan> GetAutoPardonDuration(GuildId guildId, CancellationToken cancellationToken = default)
    {
        var result = await GetGuildSetting(
            GuildSettingType.SinAutoPardonDuration,
            guildId,
            cancellationToken);

        if (result is not CachedCustomSetting setting)
            return GetDefaultAutoPardonDuration();
        if (TimeSpan.TryParse(setting.Value, CultureInfo.InvariantCulture, out var autoPardon)
            && autoPardon != TimeSpan.Zero)
            return autoPardon;

        return GetDefaultAutoPardonDuration();
    }

    public Task<SettingsResult> SetAutoPardonDuration(
        GuildId guildId,
        ModeratorId moderatorId,
        TimeSpan autoPardonAfter,
        CancellationToken cancellationToken = default)
        => SetGuildSetting(
            new GuildSettingCustomValue
            {
                GuildId = guildId,
                Type = GuildSettingType.SinAutoPardonDuration,
                SetBy = moderatorId,
                SetAt =  DateTimeOffset.UtcNow,
                Value = autoPardonAfter.ToString("c", CultureInfo.InvariantCulture)
            }, cancellationToken);

    public Task<SettingsResult> ResetAutoPardonDuration(
        GuildId guildId,
        ModeratorId moderatorId,
        CancellationToken cancellationToken = default)
        => SetGuildSetting(
            new GuildSettingCustomValue
            {
                GuildId = guildId,
                Type = GuildSettingType.SinAutoPardonDuration,
                SetBy = moderatorId,
                SetAt = DateTimeOffset.UtcNow,
                Value = GetDefaultAutoPardonDuration().ToString("c", CultureInfo.InvariantCulture)
            }, cancellationToken);
}
