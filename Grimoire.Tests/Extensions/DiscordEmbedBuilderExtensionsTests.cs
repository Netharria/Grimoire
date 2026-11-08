// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Extensions;

namespace Grimoire.Tests.Extensions;

/// <summary><c>AddMessageTextToFields</c> splits message text into embed fields of at most 1024 characters.</summary>
public sealed class DiscordEmbedBuilderExtensionsTests
{
    private static IReadOnlyList<DiscordEmbedField> Split(string content)
        => new DiscordEmbedBuilder().AddMessageTextToFields("Content", content).Fields;

    [Fact]
    public void LongText_IsSplitIntoFieldsOfAtMost1024()
    {
        var fields = Split(string.Join(' ', Enumerable.Repeat("word", 600)));

        fields.Count.ShouldBeGreaterThan(1);
        fields.ShouldAllBe(field => field.Value!.Length <= 1024);
        fields[0].Name.ShouldBe("Content");
        fields.Skip(1).ShouldAllBe(field => field.Name == "**Continued**");
    }

    /// <summary>
    ///     I-17: a run of more than 1024 spaces used to become a field of nothing once trimmed, and AddField throws on
    ///     that, losing the log.
    /// </summary>
    [Fact]
    public void ALongRunOfSpaces_DoesNotMakeAnEmptyField()
    {
        var fields = Split("start" + new string(' ', 3000) + "end");

        fields.ShouldAllBe(field => !string.IsNullOrWhiteSpace(field.Value));
        fields[0].Value!.ShouldStartWith("start");
        fields[^1].Value!.ShouldEndWith("end");
    }

    [Fact]
    public void BlankText_GetsABlankMarker()
        => Split("   ").ShouldHaveSingleItem().Value.ShouldBe("`blank`");
}
