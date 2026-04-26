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
    public Task<Result<ChannelId?>> GetLogChannelSetting(GuildLogType guildLogType, GuildId guildId,
        CancellationToken cancellationToken = default)
        => GetGuildSetting(guildLogType.ToGuildSettingType(), guildId, cancellationToken)
            .AsTask()
            .Map(ParseChannelId);

    public Task<Result<ChannelId?>> SetLogChannelSetting(
        GuildLogType guildLogType,
        GuildId guildId,
        ModeratorId moderatorId,
        ChannelId? channelId,
        CancellationToken cancellationToken = default)
        => (channelId switch
        {
            not null => ApplyGuildSetting( GuildSettingCustomValue.Create(guildLogType.ToGuildSettingType(), guildId, moderatorId,
                    DateTimeOffset.UtcNow, channelId.Value.Value.ToString(CultureInfo.InvariantCulture)), cancellationToken),
            _ => ApplyGuildSetting(GuildSettingDisabled.Create(guildLogType.ToGuildSettingType(), guildId, moderatorId,
                    DateTimeOffset.UtcNow),  cancellationToken)
        }).Map(_ => channelId);
}
