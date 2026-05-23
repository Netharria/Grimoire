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
    public required DateTimeOffset Timestamp { get; init; }
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

public readonly record struct MessageContent
{
    private MessageContent(string content)
    {
        Content = content;
    }

    public string Content { get; }

    internal static MessageContent FromDatabase(string content) => new(content);

    public static Validation<MessageContent> Create(string? content)
    {
        if (content is null || content.Length > 4000)
            return Validation<MessageContent>.Fail(
                new Error("message-content.invalid", "Message content cannot exceed 4000 characters."));
        return Validation<MessageContent>.Succeed(new MessageContent(content));
    }

    [Pure]
    public bool Equals(MessageContent other, StringComparison comparison)
        => string.Equals(Content, other.Content, comparison);

    public override string ToString() => Content;
}
