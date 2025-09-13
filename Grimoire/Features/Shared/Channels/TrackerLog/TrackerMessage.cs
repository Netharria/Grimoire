// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Channels.TrackerLog;

public abstract record TrackerMessageBase
{
    public required ulong TrackerId { get; init; }
    public required TrackerIdType TrackerIdType { get; init; }
    public required ulong GuildId { get; init; }
    public abstract DiscordMessageBuilder GetMessageBuilder();
}



public record TrackerMessage : TrackerMessageBase {
    public string Title { get; init; } = string.Empty;
    public required string Description { get; init; } = string.Empty;
    public string Footer { get; init; } = string.Empty;
    public DateTimeOffset? Timestamp { get; init; }
    public DiscordColor? Color { get; init; }

    public override DiscordMessageBuilder GetMessageBuilder()
        => new DiscordMessageBuilder()
            .AddEmbed(new DiscordEmbedBuilder()
                .WithAuthor(this.Title)
                .WithDescription(this.Description)
                .WithFooter(this.Footer)
                .WithTimestamp(this.Timestamp ?? DateTimeOffset.UtcNow)
                .WithColor(this.Color ?? GrimoireColor.Purple)
                .Build()); }

public record TrackerMessageCustomEmbed : TrackerMessageBase {
    public required DiscordEmbed Embed { get; init; }
    public override DiscordMessageBuilder GetMessageBuilder()
        => new DiscordMessageBuilder()
            .AddEmbed(Embed);}

public record TrackerMessageCustomMessage : TrackerMessageBase
{
    public required DiscordMessageBuilder Message { get; init; }
    public override DiscordMessageBuilder GetMessageBuilder()
        => this.Message;
}
