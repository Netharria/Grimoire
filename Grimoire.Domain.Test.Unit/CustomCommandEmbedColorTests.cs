// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain.Test.Unit;

public sealed class CustomCommandEmbedColorTests
{
    // ── Null / empty / whitespace ─────────────────────────────────────────────

    [Fact]
    public void Create_Null_IsInvalidWithCorrectCode()
    {
        var result = CustomCommandEmbedColor.Create(null);
        result.ShouldBeOfType<Validation<CustomCommandEmbedColor>.Invalid>()
            .Errors.ShouldContain(e => e.Code == "custom-command-embed-color.validation.is-null-or-empty");
    }

    [Fact]
    public void Create_Empty_IsInvalid()
        => CustomCommandEmbedColor.Create(string.Empty)
            .ShouldBeOfType<Validation<CustomCommandEmbedColor>.Invalid>();

    [Fact]
    public void Create_WhitespaceOnly_IsInvalid()
        => CustomCommandEmbedColor.Create("   ")
            .ShouldBeOfType<Validation<CustomCommandEmbedColor>.Invalid>();

    // ── Invalid hex ───────────────────────────────────────────────────────────

    [Fact]
    public void Create_NonHexCharacters_IsInvalidWithCorrectCode()
    {
        var result = CustomCommandEmbedColor.Create("GGGGGG");
        result.ShouldBeOfType<Validation<CustomCommandEmbedColor>.Invalid>()
            .Errors.ShouldContain(e => e.Code == "custom-command-embed-color.validation.does-not-contain-hex");
    }

    [Fact]
    public void Create_FiveHexChars_IsInvalid()
        => CustomCommandEmbedColor.Create("abc12")
            .ShouldBeOfType<Validation<CustomCommandEmbedColor>.Invalid>();

    [Fact]
    public void Create_SevenHexChars_IsInvalid()
        => CustomCommandEmbedColor.Create("abc1234")
            .ShouldBeOfType<Validation<CustomCommandEmbedColor>.Invalid>();

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
