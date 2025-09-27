// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;

namespace Grimoire.Features.Shared.Channels.GuildLog;

public abstract record GuildLogMessageBase
{
    public required ulong GuildId { get; init; }
    public required GuildLogType GuildLogType { get; init; }
    public abstract DiscordMessageBuilder GetMessageBuilder();
}

public record GuildLogMessage : GuildLogMessageBase
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

public record GuildLogMessageCustomEmbed : GuildLogMessageBase
{
    public required DiscordEmbed Embed { get; init; }

    public override DiscordMessageBuilder GetMessageBuilder()
        => new DiscordMessageBuilder()
            .AddEmbed(Embed);
}

public record GuildLogMessageCustomMessage : GuildLogMessageBase
{
    public required DiscordMessageBuilder Message { get; init; }

    public override DiscordMessageBuilder GetMessageBuilder()
        => Message;
}
