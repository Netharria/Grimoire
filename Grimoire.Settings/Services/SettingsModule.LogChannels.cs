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
    public async Task<Result<ChannelId?>> GetEffectiveLogChannelSetting(
        GuildLogType guildLogType,
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(guildLogType.GetLogTypeModule(), guildId, cancellationToken).GetOrElse(() => false))
            return Result<ChannelId?>.Ok(null);

        return await GetConfiguredLogChannelSetting(guildLogType, guildId, cancellationToken);
    }

    public Task<Result<ChannelId?>> GetConfiguredLogChannelSetting(GuildLogType guildLogType, GuildId guildId,
        CancellationToken cancellationToken = default)
        => GetGuildSetting(guildLogType.ToGuildSettingType(), guildId, cancellationToken)
            .AsTask()
            .Map(ParseChannelId);

    public async Task<Result<ChannelId?>> SetLogChannelSetting(
        GuildLogType guildLogType,
        GuildId guildId,
        ModeratorId moderatorId,
        ChannelId? channelId,
        CancellationToken cancellationToken = default)
        => (channelId switch
        {
            not null => await GuildSettingCustomValue.Create(guildLogType.ToGuildSettingType(), guildId, moderatorId,
                    DateTimeOffset.UtcNow, channelId.Value.Value.ToString(CultureInfo.InvariantCulture))
                .MatchAsync(
                    setting => SetGuildSetting(setting, cancellationToken),
                    errors => Result<GuildSetting>.Fail(errors)),
            _ => await GuildSettingDisabled.Create(guildLogType.ToGuildSettingType(), guildId, moderatorId,
                    DateTimeOffset.UtcNow)
                .MatchAsync(
                    setting => SetGuildSetting(setting, cancellationToken),
                    errors => Result<GuildSetting>.Fail(errors))
        }).Map(_ => channelId);
}
