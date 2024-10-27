// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public class MessageHistory
{
    public ulong MessageId { get; init; }
    public Message Message { get; init; } = null!;
    public ulong GuildId { get; init; }
    public Guild Guild { get; init; } = null!;
    public MessageAction Action { get; init; }
    public string MessageContent { get; init; } = string.Empty;
    public ulong? DeletedByModeratorId { get; init; }
    public virtual Member? DeletedByModerator { get; init; }
    public DateTimeOffset TimeStamp { get; init; }
}

public enum MessageAction
{
    Created,
    Updated,
    Deleted
}
