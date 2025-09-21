// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed class MessageHistory
{
    public required ulong MessageId { get; init; }
    public Message? Message { get; init; }
    public required ulong GuildId { get; init; }
    public Guild? Guild { get; init; }
    public required MessageAction Action { get; init; }
    public required string MessageContent { get; init; }
    public ulong? DeletedByModeratorId { get; init; }
    public Member? DeletedByModerator { get; init; }
    public DateTimeOffset TimeStamp { get; private init; }
}

public enum MessageAction
{
    Created,
    Updated,
    Deleted
}
