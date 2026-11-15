// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using LanguageExt;

namespace Grimoire.UnitTests.Domain;

public sealed class CustomCommandNameTests
{
    // ── Null / empty / whitespace ─────────────────────────────────────────────

    [Fact]
    public void Create_Null_IsInvalidWithCorrectMessage()
    {
        var result = CustomCommandName.Create(null);
        result.ShouldFail()
            .ShouldBeOfType<Invalid>().Message.ShouldBe("A value for command name must be provided.");
    }

    [Fact]
    public void Create_Empty_IsInvalid()
        => CustomCommandName.Create(string.Empty)
            .ShouldFail().ShouldBeOfType<Invalid>();

    [Fact]
    public void Create_WhitespaceOnly_IsInvalid()
        => CustomCommandName.Create("   ")
            .ShouldFail().ShouldBeOfType<Invalid>();

    // ── Internal spaces ───────────────────────────────────────────────────────

    [Fact]
    public void Create_ContainsInternalSpace_IsInvalidWithCorrectMessage()
    {
        var result = CustomCommandName.Create("hello world");
        result.ShouldFail()
            .ShouldBeOfType<Invalid>().Message.ShouldBe("A command name cannot contain spaces.");
    }

    [Fact]
    public void Create_LeadingAndTrailingSpacesAroundSpacedName_IsInvalidAfterTrim()
        // After trimming "  a b  " becomes "a b", which still has an internal space.
        => CustomCommandName.Create("  a b  ")
            .ShouldFail().ShouldBeOfType<Invalid>();

    // ── Length ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ExactlyMaxLength_IsValid()
        => CustomCommandName.Create(new string('x', 24))
            .ShouldBeOfType<Validation<Error, CustomCommandName>.Success>();

    [Fact]
    public void Create_OneOverMaxLength_IsInvalidWithCorrectMessage()
    {
        var result = CustomCommandName.Create(new string('x', 25));
        result.ShouldFail()
            .ShouldBeOfType<Invalid>().Message.ShouldBe("A command name cannot be more than 24 characters in length.");
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
        => CustomCommandName.Create("a").ShouldBeOfType<Validation<Error, CustomCommandName>.Success>();
}
