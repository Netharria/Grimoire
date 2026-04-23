// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public abstract record MessageHistoryEntry
{
    public required MessageId MessageId { get; init; }
    public required GuildId GuildId { get; init; }
    public DateTimeOffset TimeStamp { get; } = DateTimeOffset.UtcNow;
    public Message? Message { get; init; }
}

public abstract record MessageHistoryContentEntry : MessageHistoryEntry
{
    public required MessageContent Content { get; init; }
}

public sealed record MessageCreatedEntry : MessageHistoryContentEntry;

public sealed record MessageEditedEntry : MessageHistoryContentEntry;

public sealed record MessageDeletedEntry : MessageHistoryEntry;

public sealed record MessageDeletedByModeratorEntry : MessageHistoryEntry
{
    public required ModeratorId ModeratorId { get; init; }
}

public readonly record struct MessageContent(string Content)
{
    public override string ToString() => Content;

    [Pure]
    public static bool Equals(MessageContent? a, MessageContent? b)
        => a is { } aObj && b is { } bObj && string.Equals(aObj.Content, bObj.Content);

    [Pure]
    public static bool Equals(MessageContent? a, MessageContent? b, StringComparison stringComparison)
        => a is { } aObj && b is { } bObj && string.Equals(aObj.Content, bObj.Content, stringComparison);
}
