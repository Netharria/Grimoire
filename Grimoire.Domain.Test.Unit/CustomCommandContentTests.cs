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
    public void Create_ValidContent_SucceedsWithValuePreservedAsIs()
    {
        // Content is NOT trimmed — stored exactly as provided.
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
}
