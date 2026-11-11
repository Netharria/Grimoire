// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;

namespace Grimoire.Extensions;

public static class PagedReplyExtensions
{
    /// <summary>Discord's limit for an embed description.</summary>
    internal const int MaxPageLength = 4096;

    extension(CommandContext ctx)
    {
        /// <summary>
        ///     Replies with a list that may not fit in one embed. Each page is <paramref name="template" /> with as many
        ///     whole lines as fit, and the invoker gets buttons to move between them. A list that fits is an ordinary
        ///     reply with no buttons.
        /// </summary>
        /// <param name="ephemeral">Show the pages to the invoker only, for a reply that was deferred that way.</param>
        public Task ReplyPagedAsync(DiscordEmbedBuilder template, IEnumerable<string> lines, bool ephemeral = false)
            => ctx.ReplyPagesAsync(
                [.. Paginate(lines).DefaultIfEmpty("").Select(text => new DiscordEmbedBuilder(template).WithDescription(text))],
                ephemeral);

        /// <summary>
        ///     Replies with ready-made pages. One page is an ordinary reply with no buttons; more get a "Page n of m"
        ///     footer and buttons for the invoker to move between them.
        /// </summary>
        /// <param name="ephemeral">Show the pages to the invoker only, for a reply that was deferred that way.</param>
        public async Task ReplyPagesAsync(IReadOnlyList<DiscordEmbedBuilder> pages, bool ephemeral = false)
        {
            if (pages.Count <= 1)
            {
                await ctx.ReplyAsync(embed: (pages.SingleOrDefault() ?? new DiscordEmbedBuilder()).Build(), ephemeral: ephemeral);
                return;
            }

            var embedPages = pages
                .Select((page, index) => new Page(embed: new DiscordEmbedBuilder(page)
                    .WithFooter($"Page {index + 1} of {pages.Count}")))
                .ToArray();
            if (ctx is SlashCommandContext slashContext)
                await slashContext.Interaction.SendPaginatedResponseAsync(ephemeral, ctx.User, embedPages);
            else
                await ctx.Channel.SendPaginatedMessageAsync(ctx.User, embedPages);
        }
    }

    /// <summary>
    ///     Packs lines into pages of at most <paramref name="maxLength" /> characters, breaking only between lines. A
    ///     line too long for any page, such as a 4,096-character reward message with its prefix, is split across pages.
    /// </summary>
    internal static IReadOnlyList<string> Paginate(IEnumerable<string> lines, int maxLength = MaxPageLength)
    {
        List<string> pages = [];
        var page = new StringBuilder();
        foreach (var line in lines.SelectMany(line => line.Length <= maxLength ? [line] : line.Chunk(maxLength).Select(chunk => new string(chunk))))
        {
            if (page.Length > 0 && page.Length + 1 + line.Length > maxLength)
            {
                pages.Add(page.ToString());
                page.Clear();
            }

            if (page.Length > 0)
                page.Append('\n');
            page.Append(line);
        }

        if (page.Length > 0)
            pages.Add(page.ToString());
        return pages;
    }
}
