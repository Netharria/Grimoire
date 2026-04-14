// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Channels.TrackerLog;

public abstract record TrackerEventBase
{
    public required GuildId GuildId { get; init; }
    public required TrackerMessageBase Message { get; init; }
    public DiscordMessageBuilder GetMessageBuilder() => Message.GetMessageBuilder();
}

public sealed record TrackerEventChannel : TrackerEventBase
{
    public required ChannelId ChannelId { get; init; }
}

public sealed record TrackerEventUser : TrackerEventBase
{
    public required UserId UserId { get; init; }
}

public abstract record TrackerMessageBase
{
    public abstract DiscordMessageBuilder GetMessageBuilder();
}

public record TrackerMessage : TrackerMessageBase
{
    public string Title { get; init; } = string.Empty;
    public required string Description { get; init; } = string.Empty;
    public string Footer { get; init; } = string.Empty;
    public DateTimeOffset? Timestamp { get; init; }
    public DiscordColor? Color { get; init; }

    public override DiscordMessageBuilder GetMessageBuilder()
        => new DiscordMessageBuilder()
            .AddEmbed(new DiscordEmbedBuilder()
                .WithAuthor(Title)
                .WithDescription(Description)
                .WithFooter(Footer)
                .WithTimestamp(Timestamp ?? DateTimeOffset.UtcNow)
                .WithColor(Color ?? GrimoireColor.Purple)
                .Build());
}

public record TrackerMessageCustomEmbed : TrackerMessageBase
{
    public required DiscordEmbed Embed { get; init; }

    public override DiscordMessageBuilder GetMessageBuilder()
        => new DiscordMessageBuilder()
            .AddEmbed(Embed);
}

public record TrackerMessageCustomMessage : TrackerMessageBase
{
    public required DiscordMessageBuilder Message { get; init; }

    public override DiscordMessageBuilder GetMessageBuilder()
        => Message;
}
