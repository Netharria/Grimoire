// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using LanguageExt;

namespace Grimoire.UnitTests.Domain;

public sealed class ModerationReasonTests
{
    // ── Empty / whitespace ────────────────────────────────────────────────────

    [Fact]
    public void Create_Empty_IsInvalid()
        => ModerationReason.Create(string.Empty)
            .ShouldFail().ShouldBeOfType<Invalid>();

    [Fact]
    public void Create_WhitespaceOnly_IsInvalidWithCorrectMessage()
    {
        var result = ModerationReason.Create("   ");
        result.ShouldFail()
            .ShouldBeOfType<Invalid>().Message.ShouldBe("Reason must be 1\u20131000 non-whitespace characters.");
    }

    // ── Length ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ExactlyMaxLength_IsValid()
        => ModerationReason.Create(new string('a', 1000))
            .ShouldBeOfType<Validation<Error, ModerationReason>.Success>();

    [Fact]
    public void Create_OneOverMaxLength_IsInvalidWithCorrectMessage()
    {
        var result = ModerationReason.Create(new string('a', 1001));
        result.ShouldFail()
            .ShouldBeOfType<Invalid>().Message.ShouldBe("Reason must be 1\u20131000 non-whitespace characters.");
    }

    // ── Happy paths ───────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidReason_SucceedsWithTrimmedValue()
    {
        var reason = ModerationReason.Create("  spamming  ").ShouldSucceed();
        reason.Value.ShouldBe("spamming");
    }

    [Fact]
    public void Create_ValidReason_ToStringMatchesValue()
    {
        var reason = ModerationReason.Create("bad behaviour").ShouldSucceed();
        reason.ToString().ShouldBe("bad behaviour");
    }

    // Trimmed length is still checked — 1000 spaces + 1001 real chars should fail.
    [Fact]
    public void Create_PaddedBeyondMaxLength_IsInvalid()
        => ModerationReason.Create("   " + new string('a', 1001) + "   ")
            .ShouldFail().ShouldBeOfType<Invalid>();

    // ── CreateIfNotNull ───────────────────────────────────────────────────────

    [Fact]
    public void CreateIfNotNull_Null_SucceedsWithNoReason()
    {
        var result = ModerationReason.CreateIfNotNull(null);
        result.ShouldSucceed().IsNone.ShouldBeTrue();
    }

    [Fact]
    public void CreateIfNotNull_ValidString_SucceedsWithReason()
    {
        var result = ModerationReason.CreateIfNotNull("harassment");
        result.ShouldSucceed().Map(reason => reason.Value).ShouldBe(Some("harassment"));
    }

    [Fact]
    public void CreateIfNotNull_InvalidString_IsInvalid()
        => ModerationReason.CreateIfNotNull(new string('a', 1001))
            .ShouldFail().ShouldBeOfType<Invalid>();

    [Fact]
    public void CreateIfNotNull_WhitespaceOnly_IsInvalid()
        => ModerationReason.CreateIfNotNull("   ")
            .ShouldFail().ShouldBeOfType<Invalid>();
}
