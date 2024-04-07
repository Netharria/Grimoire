// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain;

public class Message : IIdentifiable<ulong>, IMember
{
    public ulong Id { get; set; }

    public ulong UserId { get; set; }
    public virtual Member Member { get; set; } = null!;

    public ulong ChannelId { get; set; }

    public virtual Channel Channel { get; set; } = null!;

    public ulong GuildId { get; set; }

    public virtual Guild Guild { get; set; } = null!;

    public DateTimeOffset CreatedTimestamp { get; set; }

    public ulong? ReferencedMessageId { get; set; }

    public virtual ProxiedMessageLink ProxiedMessageLink { get; set; } = null!;

    public virtual ProxiedMessageLink OriginalMessageLink { get; set; } = null!;

    public virtual ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();

    public virtual ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
    public virtual ICollection<MessageHistory> MessageHistory { get; set; } = new List<MessageHistory>();

}
