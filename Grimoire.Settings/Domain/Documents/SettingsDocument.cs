// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;

namespace Grimoire.Settings.Domain.Documents;

/// <summary>
///     The kinds of guild settings document (spec 2.2, TD-6 as amended by C13). Stored by name in
///     <c>Settings."GuildSettings"."Document"</c>, so a value is never renamed without a migration.
/// </summary>
public enum SettingsDocument
{
    Modules,
    General,
    Leveling,
    MessageLog,
    Moderation,
    UserLog
}

/// <summary>
///     A guild settings document. Every field is optional: a missing document or field means the default, so
///     <see cref="Empty" /> is what a guild that never changed anything has.
/// </summary>
public interface ISettingsDocument<TSelf> where TSelf : class, ISettingsDocument<TSelf>
{
    static abstract SettingsDocument Kind { get; }

    static abstract TSelf Empty { get; }
}

/// <summary>A document that holds the channels of some guild logs. A null channel means the log is off.</summary>
public interface ILogChannelDocument<TSelf> : ISettingsDocument<TSelf>
    where TSelf : class, ILogChannelDocument<TSelf>
{
    ChannelId? LogChannel(GuildLogType type);

    TSelf WithLogChannel(GuildLogType type, ChannelId? channel);
}

public static class SettingsDocumentExtensions
{
    extension(GuildLogType guildLogType)
    {
        /// <summary>The document that holds this log's channel.</summary>
        public SettingsDocument Document()
            => guildLogType switch
            {
                GuildLogType.Moderation => SettingsDocument.General,
                GuildLogType.Leveling => SettingsDocument.Leveling,
                GuildLogType.MessageEdited or GuildLogType.MessageDeleted or GuildLogType.BulkMessageDeleted
                    => SettingsDocument.MessageLog,
                GuildLogType.UserJoined or GuildLogType.UserLeft or GuildLogType.AvatarUpdated
                    or GuildLogType.NicknameUpdated or GuildLogType.UsernameUpdated => SettingsDocument.UserLog,
                GuildLogType.PublicModeration => SettingsDocument.Moderation,
                _ => throw new ArgumentOutOfRangeException(nameof(guildLogType), guildLogType, null)
            };
    }

    /// <summary>A log type that the document doesn't hold: a bug in the caller's dispatch.</summary>
    internal static InvariantViolationException NotIn(GuildLogType type, SettingsDocument document)
        => new($"The {document} document holds no channel for the {type} log.");
}
