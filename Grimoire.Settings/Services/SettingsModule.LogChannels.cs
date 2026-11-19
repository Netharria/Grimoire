// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Documents;
using Grimoire.Settings.Enums;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public Task<Fin<Option<ChannelId>>> GetLogChannelSetting(GuildLogType guildLogType, GuildId guildId,
        CancellationToken cancellationToken = default)
        => guildLogType.Document() switch
        {
            SettingsDocument.General => GetLogChannel<GeneralConfig>(guildLogType, guildId, cancellationToken),
            SettingsDocument.Leveling => GetLogChannel<LevelingConfig>(guildLogType, guildId, cancellationToken),
            SettingsDocument.MessageLog => GetLogChannel<MessageLogConfig>(guildLogType, guildId, cancellationToken),
            SettingsDocument.Moderation => GetLogChannel<ModerationConfig>(guildLogType, guildId, cancellationToken),
            SettingsDocument.UserLog => GetLogChannel<UserLogConfig>(guildLogType, guildId, cancellationToken),
            _ => throw new InvariantViolationException()
        };

    /// <summary>
    ///     Whether this log has a channel, so a caller can skip building a log nobody will see. A failed lookup counts
    ///     as no: the publisher would make the same lookup and drop the log too.
    /// </summary>
    public async Task<bool> IsLogChannelSet(GuildLogType guildLogType, GuildId guildId,
        CancellationToken cancellationToken = default)
        => (await GetLogChannelSetting(guildLogType, guildId, cancellationToken)
            .OrNullDuringMigration()) is not null;

    /// <summary>Saves a log's channel. A null channel turns the log off.</summary>
    public Task<Fin<Write<Option<ChannelId>>>> SetLogChannelSetting(
        GuildLogType guildLogType,
        GuildId guildId,
        ModeratorId moderatorId,
        ChannelId? channelId,
        CancellationToken cancellationToken = default)
        => (guildLogType.Document() switch
            {
                SettingsDocument.General => SetLogChannel<GeneralConfig>(guildLogType, guildId, moderatorId,
                    channelId, cancellationToken),
                SettingsDocument.Leveling => SetLogChannel<LevelingConfig>(guildLogType, guildId, moderatorId,
                    channelId, cancellationToken),
                SettingsDocument.MessageLog => SetLogChannel<MessageLogConfig>(guildLogType, guildId, moderatorId,
                    channelId, cancellationToken),
                SettingsDocument.Moderation => SetLogChannel<ModerationConfig>(guildLogType, guildId, moderatorId,
                    channelId, cancellationToken),
                SettingsDocument.UserLog => SetLogChannel<UserLogConfig>(guildLogType, guildId, moderatorId,
                    channelId, cancellationToken),
                _ => throw new InvariantViolationException()
            })
            .MapFin(write => write.Map(_ => Optional(channelId)));

    private Task<Fin<Option<ChannelId>>> GetLogChannel<T>(GuildLogType type, GuildId guildId,
        CancellationToken cancellationToken) where T : class, ILogChannelDocument<T>
        => GetDocument<T>(guildId, cancellationToken).MapFin(document => Optional(document.LogChannel(type)));

    private async Task<Fin<Write<Unit>>> SetLogChannel<T>(GuildLogType type, GuildId guildId,
        ModeratorId moderatorId, ChannelId? channelId, CancellationToken cancellationToken)
        where T : class, ILogChannelDocument<T>
        => (await UpdateDocument<T>(guildId, moderatorId, document => document.WithLogChannel(type, channelId),
                cancellationToken))
            .Map(write => write.Map(_ => unit));
}
