// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Features.Moderation.Diagnostics;
using Grimoire.Settings.Domain;

namespace Grimoire.Tests.Features.Moderation.Diagnostics;

/// <summary>KI-62: <c>/Diagnostics Ignore View</c> must never cut a mention, however long the list is.</summary>
public sealed class DiagnosticsIgnoreViewTests
{
    private static readonly GuildId Guild = new(1);
    private static readonly ModeratorId Moderator = new(2);

    private static DiagnosticsIgnoredItem IgnoredRole(int index)
        => DiagnosticsIgnoredRole.Create(
            new RoleId(1_000_000_000_000_000_000 + (ulong)index), Guild, Moderator, DateTimeOffset.UnixEpoch);

    private static DiagnosticsIgnoredItem IgnoredMember(int index)
        => DiagnosticsIgnoredMember.Create(
            new UserId(1_000_000_000_000_000_000 + (ulong)index), Guild, Moderator, DateTimeOffset.UnixEpoch);

    private static List<(string Name, string Value)> Fields(DiscordEmbed page)
        => [.. page.Fields.ShouldNotBeNull().Select(field => (field.Name.ShouldNotBeNull(), field.Value.ShouldNotBeNull()))];

    [Fact]
    public void An_empty_list_is_one_page_showing_none_for_both()
    {
        var page = DiagnosticsCommands.IgnoreCommands.ViewPages([]).ShouldHaveSingleItem().Build();

        page.Title.ShouldBe("Ignored by the diagnostics");
        Fields(page).ShouldBe([("Roles", "None"), ("Members", "None")]);
    }

    [Fact]
    public void A_list_too_long_for_one_embed_is_split_into_pages_of_whole_mentions()
    {
        List<DiagnosticsIgnoredItem> items =
            [.. Enumerable.Range(0, 400).Select(IgnoredRole), .. Enumerable.Range(0, 300).Select(IgnoredMember)];

        var built = DiagnosticsCommands.IgnoreCommands.ViewPages(items).Select(page => page.Build()).ToList();
        var pages = built.Select(Fields).ToList();

        pages.Count.ShouldBeGreaterThan(1);
        foreach (var (page, fields) in built.Zip(pages))
        {
            fields.Count.ShouldBeLessThanOrEqualTo(25);
            fields.ShouldAllBe(field => field.Value.Length <= 1024);
            (page.Title.ShouldNotBeNull().Length + fields.Sum(field => field.Name.Length + field.Value.Length))
                .ShouldBeLessThan(6000 - "Page 10 of 10".Length);
        }

        var shown = pages.SelectMany(fields => fields).SelectMany(field => field.Value.Split(' ')).ToList();
        shown.ShouldBe([
            .. items.OfType<DiagnosticsIgnoredRole>().Select(role => $"<@&{role.RoleId.Value}>"),
            .. items.OfType<DiagnosticsIgnoredMember>().Select(member => $"<@{member.UserId.Value}>")
        ]);
        pages.SelectMany(fields => fields).Select(field => field.Name).Distinct()
            .ShouldBe(["Roles", "Roles (continued)", "Members", "Members (continued)"]);
    }
}
