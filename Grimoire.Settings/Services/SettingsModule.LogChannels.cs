// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public Task<Fin<Option<ChannelId>>> GetLogChannelSetting(GuildLogType guildLogType, GuildId guildId,
        CancellationToken cancellationToken = default)
        => GetGuildSetting(guildLogType.ToGuildSettingType(), guildId, cancellationToken)
            .MapFin(ParseChannelId);

    /// <summary>
    ///     Whether this log has a channel, so a caller can skip building a log nobody will see. A failed lookup counts
    ///     as no: the publisher would make the same lookup and drop the log too.
    /// </summary>
    public async Task<bool> IsLogChannelSet(GuildLogType guildLogType, GuildId guildId,
        CancellationToken cancellationToken = default)
        => (await GetLogChannelSetting(guildLogType, guildId, cancellationToken)
            .OrNullDuringMigration()) is not null;

    public Task<Fin<Write<Option<ChannelId>>>> SetLogChannelSetting(
        GuildLogType guildLogType,
        GuildId guildId,
        ModeratorId moderatorId,
        ChannelId? channelId,
        CancellationToken cancellationToken = default)
        => SetChannelSetting(guildLogType.ToGuildSettingType(), guildId, moderatorId, channelId, cancellationToken);
}
