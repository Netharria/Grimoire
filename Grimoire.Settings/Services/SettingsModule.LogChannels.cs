// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Globalization;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Helpers;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public async Task<ChannelId?> GetEffectiveLogChannelSetting(
        GuildLogType guildLogType,
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(guildLogType.GetLogTypeModule(), guildId, cancellationToken))
            return null;

        return await GetConfiguredLogChannelSetting(guildLogType, guildId, cancellationToken);
    }

    public async Task<ChannelId?> GetConfiguredLogChannelSetting(GuildLogType guildLogType, GuildId guildId,
        CancellationToken cancellationToken = default)
        => ParseChannelId(await GetGuildSetting(guildLogType.ToGuildSettingType(), guildId, cancellationToken));

    public Task<SettingsResult> SetLogChannelSetting(
        GuildLogType guildLogType,
        GuildId guildId,
        ModeratorId moderatorId,
        ChannelId? channelId,
        CancellationToken cancellationToken = default)
    {
        if (channelId is null)
            return SetGuildSetting(
                new GuildSettingDisabled
                {
                    GuildId = guildId,
                    Type = guildLogType.ToGuildSettingType(),
                    SetBy = moderatorId,
                    SetAt = DateTimeOffset.UtcNow,
                }, cancellationToken);
        return SetGuildSetting(
            new GuildSettingCustomValue
            {
                GuildId = guildId,
                Type = guildLogType.ToGuildSettingType(),
                SetBy = moderatorId,
                SetAt = DateTimeOffset.UtcNow,
                Value = channelId.Value.Value.ToString(CultureInfo.InvariantCulture)
            }, cancellationToken);
    }
}
