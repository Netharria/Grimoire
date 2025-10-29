// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed class Message
{
    public required ulong ChannelId { get; init; }
    public DateTimeOffset CreatedTimestamp { get; } = DateTimeOffset.UtcNow;
    public ulong? ReferencedMessageId { get; init; }
    public ProxiedMessageLink? ProxiedMessageLink { get; init; }
    public ProxiedMessageLink? OriginalMessageLink { get; init; }
    public ICollection<Attachment> Attachments { get; init; } = [];
    public ICollection<MessageHistory> MessageHistory { get; init; } = [];
    public required ulong Id { get; init; }
    public required ulong UserId { get; init; }
    public required ulong GuildId { get; init; }
}
