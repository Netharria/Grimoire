// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed record Message
{
    public required ChannelId ChannelId { get; init; }
    public required DateTimeOffset CreatedTimestamp { get; init; }
    public MessageId? ReferencedMessageId { get; init; }
    /// <summary>
    /// Set when this message is the PluralKit webhook (proxied) message.
    /// Links back to the original message the user sent with their main Discord account before PluralKit deleted it.
    /// </summary>
    public ProxiedMessageLink? ProxiedMessageLink { get; init; }

    /// <summary>
    /// Set when this message is the original message sent by the user's main Discord account,
    /// which PluralKit subsequently deleted and replaced with a webhook message.
    /// Links forward to the proxied webhook message.
    /// </summary>
    public ProxiedMessageLink? OriginalMessageLink { get; init; }
    public ICollection<Attachment> Attachments { get; init; } = [];
    public ICollection<MessageHistoryEntry> MessageHistory { get; init; } = [];
    public required MessageId Id { get; init; }
    public required UserId UserId { get; init; }
    public required GuildId GuildId { get; init; }
}
