// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public class MessageHistory
{
    public ulong MessageId { get; set; }
    public Message Message { get; set; } = null!;
    public ulong GuildId { get; set; }
    public Guild Guild { get; set; } = null!;
    public MessageAction Action { get; set; }
    public string MessageContent { get; set; } = string.Empty;
    public ulong? DeletedByModeratorId { get; set; }
    public virtual Member? DeletedByModerator { get; set; } = null!;
    public DateTimeOffset TimeStamp { get; set; }
}

public enum MessageAction
{
    Created,
    Updated,
    Deleted
}
