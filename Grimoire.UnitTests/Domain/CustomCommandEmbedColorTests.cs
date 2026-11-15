// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.UnitTests.Domain;

public sealed class CustomCommandEmbedColorTests
{
    // ── Null / empty / whitespace ─────────────────────────────────────────────

    [Fact]
    public void Create_Null_IsInvalidWithCorrectMessage()
    {
        var result = CustomCommandEmbedColor.Create(null);
        result.ShouldFail()
            .ShouldBeOfType<Invalid>().Message.ShouldBe("A value for command embed color must be provided.");
    }

    [Fact]
    public void Create_Empty_IsInvalid()
        => CustomCommandEmbedColor.Create(string.Empty)
            .ShouldFail().ShouldBeOfType<Invalid>();

    [Fact]
    public void Create_WhitespaceOnly_IsInvalid()
        => CustomCommandEmbedColor.Create("   ")
            .ShouldFail().ShouldBeOfType<Invalid>();

    // ── Invalid hex ───────────────────────────────────────────────────────────

    [Fact]
    public void Create_NonHexCharacters_IsInvalidWithCorrectMessage()
    {
        var result = CustomCommandEmbedColor.Create("GGGGGG");
        result.ShouldFail()
            .ShouldBeOfType<Invalid>().Message.ShouldBe("A command embed color must be a 6 character hex color code.");
    }

    [Fact]
    public void Create_FiveHexChars_IsInvalid()
        => CustomCommandEmbedColor.Create("abc12")
            .ShouldFail().ShouldBeOfType<Invalid>();

    [Fact]
    public void Create_SevenHexChars_IsInvalid()
        => CustomCommandEmbedColor.Create("abc1234")
            .ShouldFail().ShouldBeOfType<Invalid>();

    // ── Happy paths ───────────────────────────────────────────────────────────

    [Fact]
    public void Create_LowercaseHex_SucceedsNormalizedToUppercase()
    {
        var color = CustomCommandEmbedColor.Create("abc123").ShouldSucceed();
        color.Value.ShouldBe("ABC123");
    }

    [Fact]
    public void Create_UppercaseHex_SucceedsPreservedAsUppercase()
    {
        var color = CustomCommandEmbedColor.Create("ABCDEF").ShouldSucceed();
        color.Value.ShouldBe("ABCDEF");
    }

    [Fact]
    public void Create_MixedCaseHex_SucceedsNormalizedToUppercase()
    {
        var color = CustomCommandEmbedColor.Create("aAbBcC").ShouldSucceed();
        color.Value.ShouldBe("AABBCC");
    }

    [Fact]
    public void Create_WithHashPrefix_StripsHashAndSucceeds()
    {
        var color = CustomCommandEmbedColor.Create("#abc123").ShouldSucceed();
        color.Value.ShouldBe("ABC123");
    }

    [Fact]
    public void Create_WithHashPrefixAndUppercase_StripsHashAndSucceeds()
    {
        var color = CustomCommandEmbedColor.Create("#ABCDEF").ShouldSucceed();
        color.Value.ShouldBe("ABCDEF");
    }

    [Fact]
    public void Create_WithSurroundingWhitespace_TrimsAndSucceeds()
    {
        var color = CustomCommandEmbedColor.Create("  abc123  ").ShouldSucceed();
        color.Value.ShouldBe("ABC123");
    }

    [Fact]
    public void Create_WithWhitespaceAndHash_TrimsAndStripsHashAndSucceeds()
    {
        var color = CustomCommandEmbedColor.Create("  #abc123  ").ShouldSucceed();
        color.Value.ShouldBe("ABC123");
    }

    [Fact]
    public void Create_ValidColor_ToStringMatchesValue()
    {
        var color = CustomCommandEmbedColor.Create("123456").ShouldSucceed();
        color.ToString().ShouldBe("123456");
    }
}
