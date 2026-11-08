// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Extensions;

namespace Grimoire.Tests.Extensions;

/// <summary><c>Paginate</c> packs a list's lines into pages that each fit an embed's description.</summary>
public sealed class PagedReplyExtensionsTests
{
    [Fact]
    public void AShortList_IsOnePage()
        => PagedReplyExtensions.Paginate(["one", "two"]).ShouldBe(["one\ntwo"]);

    [Fact]
    public void NoLines_IsNoPages()
        => PagedReplyExtensions.Paginate([]).ShouldBeEmpty();

    [Fact]
    public void Pages_BreakOnlyBetweenLines_AndKeepEveryLine()
    {
        var lines = Enumerable.Range(0, 500).Select(i => $"line {i:D3} " + new string('x', 40)).ToArray();

        var pages = PagedReplyExtensions.Paginate(lines);

        pages.Count.ShouldBeGreaterThan(1);
        pages.ShouldAllBe(page => page.Length <= PagedReplyExtensions.MaxPageLength);
        pages.SelectMany(page => page.Split('\n')).ShouldBe(lines);
    }

    /// <summary>A line longer than a page, such as a 4,096-character reward message with its prefix, is split, not lost.</summary>
    [Fact]
    public void ALineLongerThanAPage_IsSplitAcrossPages()
    {
        var line = new string('a', 5000);

        var pages = PagedReplyExtensions.Paginate(["before", line, "after"]);

        pages.ShouldAllBe(page => page.Length <= PagedReplyExtensions.MaxPageLength);
        string.Concat(pages).Replace("\n", "").ShouldBe("before" + line + "after");
    }
}
