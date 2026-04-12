// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Globalization;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;

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
    {

        var result = await GetGuildSetting(guildLogType.ToGuildSettingType(), guildId, cancellationToken);

        if (result is not CachedCustomSetting setting)
            return null;
        if (ulong.TryParse(setting.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var channelId)
            && channelId != 0)
            return new ChannelId(channelId);

        return null;
    }

    public Task SetLogChannelSetting(
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
                    GuildId = guildId, Type = guildLogType.ToGuildSettingType(), SetBy = moderatorId
                }, cancellationToken);
        return SetGuildSetting(
            new GuildSettingCustomValue
            {
                GuildId = guildId,
                Type = guildLogType.ToGuildSettingType(),
                SetBy = moderatorId,
                Value = channelId.Value.Value.ToString(CultureInfo.InvariantCulture)
            }, cancellationToken);
    }
}
