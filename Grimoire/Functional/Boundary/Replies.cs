// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Functional.Boundary;

/// <summary>The embeds the boundary sends. Pure.</summary>
public static class Replies
{
    /// <summary>
    ///     A refusal: each distinct message on its own line (UX-1), no title (UX-4). Red if any error is
    ///     <c>Invalid</c> or <c>Forbidden</c>, yellow when every one is <c>NotFound</c> (Q2c).
    /// </summary>
    [Pure]
    public static DiscordEmbed Refusal(Error error)
    {
        var errors = error.AsIterable().ToArray();
        var colour = errors.All(e => e is NotFound) ? GrimoireColor.Yellow : GrimoireColor.Red;
        var message = string.Join('\n', errors.Select(e => e.Message).Distinct());
        return Embed(colour, message);
    }

    /// <summary>The reply for an outage or a defect. It says nothing about the cause (UX-2).</summary>
    [Pure]
    public static DiscordEmbed Generic(string command, string errorId) =>
        Embed(GrimoireColor.Yellow,
            $"Something went wrong while running `/{command}`. It's been reported. "
            + $"If it keeps happening, quote ID `{errorId}`.");

    [Pure]
    public static DiscordEmbed Embed(DiscordColor colour, string message, string title = "") =>
        new DiscordEmbedBuilder()
            .WithColor(colour)
            .WithAuthor(title)
            .WithDescription(message)
            .Build();
}
