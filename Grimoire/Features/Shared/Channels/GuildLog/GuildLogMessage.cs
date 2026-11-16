// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;

namespace Grimoire.Features.Shared.Channels.GuildLog;

/// <summary>
///     One guild log post (Q26, SH-10; spec 1.4, section 3.8). <see cref="At" /> is when the event happened, never when
///     the post goes out, so a backed-up queue doesn't make the log late (defect 27).
/// </summary>
/// <remarks>
///     The builder is mutable, which the record can't prevent. It's built once by a factory and never changed after.
/// </remarks>
public sealed record GuildLogMessage(GuildId Guild, GuildLogType Type, DateTimeOffset At, DiscordMessageBuilder Message)
{
    /// <summary>The usual log post: one embed with a description, and optionally a colour, a title and a footer.</summary>
    [Pure]
    public static GuildLogMessage Text(GuildId guild, GuildLogType type, DateTimeOffset at, string description,
        DiscordColor? color = null, string title = "", string footer = "") =>
        Embed(guild, type, at, new DiscordEmbedBuilder()
            .WithAuthor(title)
            .WithDescription(description)
            .WithFooter(footer)
            .WithTimestamp(at)
            .WithColor(color ?? GrimoireColor.Purple)
            .Build());

    /// <summary>One embed. Without a timestamp of its own, it gets <paramref name="at" />.</summary>
    [Pure]
    public static GuildLogMessage Embed(GuildId guild, GuildLogType type, DateTimeOffset at, DiscordEmbed embed) =>
        new(guild, type, at, new DiscordMessageBuilder().AddEmbed(Stamped(embed, at)));

    /// <summary>
    ///     A whole message, for logs with files or several embeds. Each embed without a timestamp gets
    ///     <paramref name="at" />; the builder is changed in place, so pass a new one.
    /// </summary>
    public static GuildLogMessage Of(GuildId guild, GuildLogType type, DateTimeOffset at,
        DiscordMessageBuilder message)
    {
        if (message.Embeds.Any(embed => embed.Timestamp is null))
        {
            var stamped = message.Embeds.Select(embed => Stamped(embed, at)).ToList();
            message.ClearEmbeds();
            message.AddEmbeds(stamped);
        }

        return new GuildLogMessage(guild, type, at, message);
    }

    [Pure]
    private static DiscordEmbed Stamped(DiscordEmbed embed, DateTimeOffset at) =>
        embed.Timestamp is null ? new DiscordEmbedBuilder(embed).WithTimestamp(at).Build() : embed;
}

// The bridge for code not yet ported (spec 1.4, OQ-1). GuildLog converts these with ToMessage when they're queued,
// stamping the time then. Each Phase 3 section moves its sites to GuildLogMessage's factories; Phase 5 deletes these.

public abstract record GuildLogMessageBase
{
    public required GuildId GuildId { get; init; }
    public required GuildLogType GuildLogType { get; init; }
    public abstract DiscordMessageBuilder GetMessageBuilder(DateTimeOffset queuedAt);

    /// <summary>The message as the new record, stamped with the time it was queued.</summary>
    public GuildLogMessage ToMessage(DateTimeOffset queuedAt) =>
        GuildLogMessage.Of(this.GuildId, this.GuildLogType, queuedAt, this.GetMessageBuilder(queuedAt));
}

/// <summary>The old text log post. Was <c>GuildLogMessage</c> before the record took the name.</summary>
public record GuildLogTextMessage : GuildLogMessageBase
{
    public string Title { get; init; } = string.Empty;
    public required string Description { get; init; } = string.Empty;
    public string Footer { get; init; } = string.Empty;
    public DateTimeOffset? Timestamp { get; init; }
    public DiscordColor? Color { get; init; }

    public override DiscordMessageBuilder GetMessageBuilder(DateTimeOffset queuedAt)
        => GuildLogMessage.Text(this.GuildId, this.GuildLogType, this.Timestamp ?? queuedAt, this.Description,
            this.Color, this.Title, this.Footer).Message;
}

public record GuildLogMessageCustomEmbed : GuildLogMessageBase
{
    public required DiscordEmbed Embed { get; init; }

    public override DiscordMessageBuilder GetMessageBuilder(DateTimeOffset queuedAt)
        => new DiscordMessageBuilder()
            .AddEmbed(Embed);
}

public record GuildLogMessageCustomMessage : GuildLogMessageBase
{
    public required DiscordMessageBuilder Message { get; init; }

    public override DiscordMessageBuilder GetMessageBuilder(DateTimeOffset queuedAt)
        => Message;
}
