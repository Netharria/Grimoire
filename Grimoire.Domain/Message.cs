// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;
using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public class Message : IIdentifiable<ulong>, IMember
{
    public virtual Member Member { get; init; } = null!;

    public ulong ChannelId { get; init; }

    public virtual Channel Channel { get; init; } = null!;

    public virtual Guild Guild { get; init; } = null!;

    public DateTimeOffset CreatedTimestamp { get; init; }

    public ulong? ReferencedMessageId { get; init; }

    public virtual ProxiedMessageLink ProxiedMessageLink { get; init; } = null!;

    public virtual ProxiedMessageLink OriginalMessageLink { get; init; } = null!;

    public virtual ICollection<Attachment> Attachments { get; init; } = [];

    public virtual ICollection<Reaction> Reactions { get; init; } = [];
    public virtual ICollection<MessageHistory> MessageHistory { get; init; } = [];
    public ulong Id { get; set; }

    public ulong UserId { get; set; }

    public ulong GuildId { get; set; }
}
