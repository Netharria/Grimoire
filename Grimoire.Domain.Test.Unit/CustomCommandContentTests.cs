// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain.Test.Unit;

public sealed class CustomCommandContentTests
{
    // ── Null / empty / whitespace ─────────────────────────────────────────────

    [Fact]
    public void Create_Null_IsInvalidWithCorrectCode()
    {
        var result = CustomCommandContent.Create(null);
        result.ShouldBeOfType<Validation<CustomCommandContent>.Invalid>()
            .Errors.ShouldContain(e => e.Code == "custom-command-content.empty");
    }

    [Fact]
    public void Create_Empty_IsInvalid()
        => CustomCommandContent.Create(string.Empty)
            .ShouldBeOfType<Validation<CustomCommandContent>.Invalid>();

    [Fact]
    public void Create_WhitespaceOnly_IsInvalid()
        => CustomCommandContent.Create("   ")
            .ShouldBeOfType<Validation<CustomCommandContent>.Invalid>();

    // ── Length ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ExactlyMaxLength_IsValid()
        => CustomCommandContent.Create(new string('a', 2000))
            .ShouldBeOfType<Validation<CustomCommandContent>.Valid>();

    [Fact]
    public void Create_OneOverMaxLength_IsInvalidWithCorrectCode()
    {
        var result = CustomCommandContent.Create(new string('a', 2001));
        result.ShouldBeOfType<Validation<CustomCommandContent>.Invalid>()
            .Errors.ShouldContain(e => e.Code == "custom-command-content.too-long");
    }

    // ── Happy paths ───────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidContent_NoEscapes_ValuePreservedAsIs()
    {
        // Plain content without escape sequences is stored exactly as provided
        // (not trimmed, not otherwise modified).
        const string input = "  hello world  ";
        var content = CustomCommandContent.Create(input).ShouldSucceed();
        content.Value.ShouldBe(input);
    }

    [Fact]
    public void Create_ValidContent_ToStringMatchesValue()
    {
        const string text = "Hello, Discord!";
        var content = CustomCommandContent.Create(text).ShouldSucceed();
        content.ToString().ShouldBe(text);
    }

    [Fact]
    public void Create_SingleCharacter_IsValid()
        => CustomCommandContent.Create("x").ShouldBeOfType<Validation<CustomCommandContent>.Valid>();

    // ── Escape sequences ──────────────────────────────────────────────────────

    [Fact]
    public void Create_BackslashN_UnescapesToNewline()
    {
        var content = CustomCommandContent.Create(@"line1\nline2").ShouldSucceed();
        content.Value.ShouldBe("line1\nline2");
    }

    [Fact]
    public void Create_BackslashT_UnescapesToTab()
    {
        var content = CustomCommandContent.Create(@"col1\tcol2").ShouldSucceed();
        content.Value.ShouldBe("col1\tcol2");
    }

    [Fact]
    public void Create_MixedEscapes_AllUnescaped()
    {
        var content = CustomCommandContent.Create(@"a\nb\tc").ShouldSucceed();
        content.Value.ShouldBe("a\nb\tc");
    }

    [Fact]
    public void Create_MultipleEscapesWithText_AllUnescaped()
    {
        var content = CustomCommandContent.Create(@"a\n\nb\tc").ShouldSucceed();
        content.Value.ShouldBe("a\n\nb\tc");
    }

    [Fact]
    public void Create_OnlyNewlineEscape_IsInvalid()
        => CustomCommandContent.Create(@"\n")
            .ShouldBeOfType<Validation<CustomCommandContent>.Invalid>()
            .Errors.ShouldContain(e => e.Code == "custom-command-content.empty");

    [Fact]
    public void Create_OnlyTabEscape_IsInvalid()
        => CustomCommandContent.Create(@"\t")
            .ShouldBeOfType<Validation<CustomCommandContent>.Invalid>()
            .Errors.ShouldContain(e => e.Code == "custom-command-content.empty");

    [Fact]
    public void Create_LengthCheckedAfterUnescaping()
    {
        // 1999 'a's + "\n" = 2001 raw chars, but 2000 after unescaping — should pass.
        var input = new string('a', 1999) + @"\n";
        var content = CustomCommandContent.Create(input).ShouldSucceed();
        content.Value.Length.ShouldBe(2000);
    }
}
