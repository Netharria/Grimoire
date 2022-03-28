// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class MessageHistory : Identifiable
    {
        public ulong MessageId { get; set; }
        public Message Message { get; set; } = null!;
        public ulong GuildId { get; set; }
        public Guild Guild { get; set; } = null!;
        public MessageAction Action { get; set; }
        public string MessageContent { get; set; } = string.Empty;
        public ulong? DeletedByModeratorId { get; set; }
        public virtual GuildUser? DeletedByModerator { get; set; } = null!;
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    }

    public enum MessageAction
    {
        Created,
        Updated,
        Deleted
    }
}
