// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using LanguageExt;

namespace Grimoire.Functional.Boundary;

/// <summary>
///     What a command shows on success. Later pages go out as follow-ups with the reply's visibility (KI-30, KI-113).
///     <see cref="Log" />, if set, is posted after the reply (T-1).
/// </summary>
public sealed record Reply(DiscordEmbed Embed, Seq<DiscordEmbed> FollowUps, Option<GuildLogMessageBase> Log)
{
    [Pure]
    public static Reply Of(DiscordEmbed embed) => new(embed, [], None);

    [Pure]
    public static Reply Of(DiscordColor colour, string message, string title = "") =>
        Of(Replies.Embed(colour, message, title));

    /// <summary>The first page is the reply; each later page is a follow-up.</summary>
    [Pure]
    public static Reply Pages(DiscordColor colour, string title, Seq<string> pages) =>
        pages.Head.Match(
            first => new Reply(Replies.Embed(colour, first, title),
                pages.Tail.Map(page => Replies.Embed(colour, page, title)), None),
            () => Of(colour, string.Empty, title));

    /// <summary>Posts the same embed to the guild's log of <paramref name="type" />.</summary>
    [Pure]
    public Reply Logged(GuildId guild, GuildLogType type) =>
        this with
        {
            Log = new GuildLogMessageCustomEmbed { GuildId = guild, GuildLogType = type, Embed = this.Embed }
        };

    /// <summary>Posts a log that differs from the reply.</summary>
    [Pure]
    public Reply Logged(GuildLogMessageBase log) => this with { Log = Optional(log) };
}
