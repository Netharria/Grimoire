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
    public required MessageId MessageId { get; init; }
    public Message? Message { get; init; }
    public required GuildId GuildId { get; init; }
    public required MessageAction Action { get; init; }
    public required MessageContent? MessageContent { get; init; }
    public ModeratorId? DeletedByModeratorId { get; init; }
    public DateTimeOffset TimeStamp { get; } = DateTimeOffset.UtcNow;
}

public enum MessageAction
{
    Created,
    Updated,
    Deleted
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

