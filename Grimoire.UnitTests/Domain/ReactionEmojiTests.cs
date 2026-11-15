// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.UnitTests.Domain;

public sealed class ReactionEmojiTests
{
    // ── Custom ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false, "<:pepe:555>")]
    [InlineData(true, "<a:pepe:555>")]
    public void Custom_FormatsAsDiscordMarkup(bool isAnimated, string expected)
        => ReactionEmoji.Custom.Create(new EmojiId(555), "pepe", isAnimated).ShouldSucceed()
            .ToString().ShouldBe(expected);

    [Fact]
    public void Custom_WithoutName_IsValidBecauseDiscordDropsNamesOfDeletedEmoji()
        => ReactionEmoji.Custom.Create(new EmojiId(555), null, false).ShouldSucceed()
            .ToString().ShouldBe("<::555>");

    [Fact]
    public void Custom_WithoutId_IsInvalid()
        => ReactionEmoji.Custom.Create(new EmojiId(0), "pepe", false)
            .ShouldFail()
            .ShouldBeOfType<Invalid>().Message.ShouldBe("A custom emoji must have an id.");

    [Theory]
    [InlineData("a")]
    [InlineData("has space")]
    [InlineData("colon:name")]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234567")]
    public void Custom_WithNameDiscordWouldReject_IsInvalid(string name)
        => ReactionEmoji.Custom.Create(new EmojiId(555), name, false)
            .ShouldFail()
            .ShouldBeOfType<Invalid>().Message.ShouldBe("A custom emoji name must be 2 to 32 letters, digits or underscores.");

    // ── Standard ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("👍")]
    [InlineData("👩‍👩‍👧‍👦")]
    [InlineData("1️⃣")]
    public void Standard_IsStoredAsTheEmojiItself(string emoji)
        => ReactionEmoji.Standard.Create(emoji).ShouldSucceed().ToString().ShouldBe(emoji);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Standard_Empty_IsInvalid(string? emoji)
        => ReactionEmoji.Standard.Create(emoji)
            .ShouldFail()
            .ShouldBeOfType<Invalid>().Message.ShouldBe("A standard emoji cannot be empty.");

    [Fact]
    public void Standard_LongerThanTheColumn_IsInvalid()
        => ReactionEmoji.Standard.Create(new string('x', 65))
            .ShouldFail()
            .ShouldBeOfType<Invalid>().Message
            .ShouldBe($"A standard emoji cannot be longer than {ReactionEmoji.Standard.MaxLength} characters.");

    [Theory]
    [InlineData("<:pepe:555>")]
    [InlineData("👍 👍")]
    public void Standard_ThatCouldBeMistakenForCustomMarkup_IsInvalid(string emoji)
        => ReactionEmoji.Standard.Create(emoji)
            .ShouldFail()
            .ShouldBeOfType<Invalid>().Message.ShouldBe("A standard emoji cannot contain markup or spaces.");

    // ── Equality ──────────────────────────────────────────────────────────────

    [Fact]
    public void SameEmoji_AreEqual_SoTheyMatchAsPartOfTheReactionKey()
        => ReactionEmoji.Custom.Create(new EmojiId(555), "pepe", false).ShouldSucceed()
            .ShouldBe(ReactionEmoji.Custom.Create(new EmojiId(555), "pepe", false).ShouldSucceed());
}
