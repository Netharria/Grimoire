// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using LanguageExt;

namespace Grimoire.UnitTests.Domain;

/// <summary>The error cases and the language-ext behaviour Grimoire relies on (spec 1.1).</summary>
public sealed class ErrorTests
{
    [Fact]
    public void Each_case_keeps_its_message()
    {
        new Invalid("bad input").Message.ShouldBe("bad input");
        new NotFound("gone").Message.ShouldBe("gone");
        new Forbidden("missing Manage Roles").Message.ShouldBe("missing Manage Roles");
        new Unavailable("database down").Message.ShouldBe("database down");
    }

    [Fact]
    public void Unavailable_keeps_the_exception_for_the_log()
    {
        var exception = new TimeoutException("timed out");

        var error = Unavailable.From("Could not load the settings.", exception);

        error.Message.ShouldBe("Could not load the settings.");
        error.Inner.IsSome.ShouldBeTrue();
        // A timeout is the case that matters: Error.New(exception) alone would drop it.
        error.Inner.Bind(inner => inner.Exception).Map(e => ReferenceEquals(e, exception)).ShouldBe(Some(true));
    }

    [Fact]
    public void The_cases_are_expected_errors()
    {
        Error[] errors = [new Invalid("a"), new NotFound("b"), new Forbidden("c"), new Unavailable("d")];
        errors.ShouldAllBe(error => error.IsExpected);
    }

    [Fact]
    public void A_case_converts_to_a_failed_Fin()
    {
        Fin<int> fin = new NotFound("No such sin.");

        fin.ShouldFail().ShouldBeOfType<NotFound>().Message.ShouldBe("No such sin.");
    }

    [Fact]
    public void Validation_collects_every_failed_check()
    {
        var both = (Nickname.Create(""), CustomCommandName.Create("has space"))
            .Apply((nickname, name) => (nickname, name))
            .As();

        var error = both.ShouldFail();
        error.ShouldBeOfType<ManyErrors>();
        error.AsIterable().Select(e => e.Message).ToArray().ShouldBe(
            ["Nickname cannot be empty.", "A command name cannot contain spaces."]);
    }

    [Fact]
    public void ToFin_keeps_every_error_of_a_failed_validation()
    {
        var both = (Nickname.Create(""), CustomCommandName.Create("has space"))
            .Apply((nickname, name) => (nickname, name))
            .As();

        both.ToFin().ShouldFail().Messages
            .ShouldBe("Nickname cannot be empty.; A command name cannot contain spaces.");
    }

    [Fact]
    public void Messages_removes_duplicates()
        => Error.Many(new Invalid("a"), new Invalid("a"), new Invalid("b")).Messages.ShouldBe("a; b");

    [Fact]
    public void A_single_error_has_its_own_message()
        => new Invalid("only").Messages.ShouldBe("only");

    /// <summary>
    ///     Pins that this language-ext version accepts a <c>null</c> success (v4 threw). Grimoire uses
    ///     <c>Option</c> for absent values instead, so a stray <c>null</c> is a bug, but it fails in a known way.
    /// </summary>
    [Fact]
    public void A_null_success_is_accepted()
    {
        Fin.Succ<string?>(null).IsSucc.ShouldBeTrue();
        Success<Error, string?>(null).IsSuccess.ShouldBeTrue();
    }
}
