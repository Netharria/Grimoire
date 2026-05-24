// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain.Test.Unit;

public sealed class CustomCommandNameTests
{
    // ── Null / empty / whitespace ─────────────────────────────────────────────

    [Fact]
    public void Create_Null_IsInvalidWithCorrectCode()
    {
        var result = CustomCommandName.Create(null);
        result.ShouldBeOfType<Validation<CustomCommandName>.Invalid>()
            .Errors.ShouldContain(e => e.Code == "custom-command-name.validation.is-null-or-empty");
    }

    [Fact]
    public void Create_Empty_IsInvalid()
        => CustomCommandName.Create(string.Empty)
            .ShouldBeOfType<Validation<CustomCommandName>.Invalid>();

    [Fact]
    public void Create_WhitespaceOnly_IsInvalid()
        => CustomCommandName.Create("   ")
            .ShouldBeOfType<Validation<CustomCommandName>.Invalid>();

    // ── Internal spaces ───────────────────────────────────────────────────────

    [Fact]
    public void Create_ContainsInternalSpace_IsInvalidWithCorrectCode()
    {
        var result = CustomCommandName.Create("hello world");
        result.ShouldBeOfType<Validation<CustomCommandName>.Invalid>()
            .Errors.ShouldContain(e => e.Code == "custom-command-name.validation.contains-space");
    }

    [Fact]
    public void Create_LeadingAndTrailingSpacesAroundSpacedName_IsInvalidAfterTrim()
        // After trimming "  a b  " becomes "a b", which still has an internal space.
        => CustomCommandName.Create("  a b  ")
            .ShouldBeOfType<Validation<CustomCommandName>.Invalid>();

    // ── Length ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ExactlyMaxLength_IsValid()
        => CustomCommandName.Create(new string('x', 24))
            .ShouldBeOfType<Validation<CustomCommandName>.Valid>();

    [Fact]
    public void Create_OneOverMaxLength_IsInvalidWithCorrectCode()
    {
        var result = CustomCommandName.Create(new string('x', 25));
        result.ShouldBeOfType<Validation<CustomCommandName>.Invalid>()
            .Errors.ShouldContain(e => e.Code == "custom-command-name.validation.length");
    }

    // ── Happy paths ───────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidName_SucceedsWithTrimmedValue()
    {
        var result = CustomCommandName.Create("  mycommand  ");
        var name = result.ShouldSucceed();
        name.Value.ShouldBe("mycommand");
    }

    [Fact]
    public void Create_ValidName_ToStringMatchesValue()
    {
        var name = CustomCommandName.Create("ping").ShouldSucceed();
        name.ToString().ShouldBe("ping");
    }

    [Fact]
    public void Create_SingleCharacter_IsValid()
        => CustomCommandName.Create("a").ShouldBeOfType<Validation<CustomCommandName>.Valid>();
}
