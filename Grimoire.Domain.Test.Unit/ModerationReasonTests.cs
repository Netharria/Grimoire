// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain.Test.Unit;

public sealed class ModerationReasonTests
{
    // ── Empty / whitespace ────────────────────────────────────────────────────

    [Fact]
    public void Create_Empty_IsInvalid()
        => ModerationReason.Create(string.Empty)
            .ShouldBeOfType<Validation<ModerationReason>.Invalid>();

    [Fact]
    public void Create_WhitespaceOnly_IsInvalidWithCorrectCode()
    {
        var result = ModerationReason.Create("   ");
        result.ShouldBeOfType<Validation<ModerationReason>.Invalid>()
            .Errors.ShouldContain(e => e.Code == "moderation-reason.invalid");
    }

    // ── Length ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ExactlyMaxLength_IsValid()
        => ModerationReason.Create(new string('a', 1000))
            .ShouldBeOfType<Validation<ModerationReason>.Valid>();

    [Fact]
    public void Create_OneOverMaxLength_IsInvalidWithCorrectCode()
    {
        var result = ModerationReason.Create(new string('a', 1001));
        result.ShouldBeOfType<Validation<ModerationReason>.Invalid>()
            .Errors.ShouldContain(e => e.Code == "moderation-reason.invalid");
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
            .ShouldBeOfType<Validation<ModerationReason>.Invalid>();

    // ── CreateIfNotNull ───────────────────────────────────────────────────────

    [Fact]
    public void CreateIfNotNull_Null_SucceedsWithNullValue()
    {
        var result = ModerationReason.CreateIfNotNull(null);
        result.ShouldSucceed().ShouldBeNull();
    }

    [Fact]
    public void CreateIfNotNull_ValidString_SucceedsWithNonNullReason()
    {
        var result = ModerationReason.CreateIfNotNull("harassment");
        var reason = result.ShouldSucceed();
        reason.ShouldNotBeNull();
        reason.Value.Value.ShouldBe("harassment");
    }

    [Fact]
    public void CreateIfNotNull_InvalidString_IsInvalid()
        => ModerationReason.CreateIfNotNull(new string('a', 1001))
            .ShouldBeOfType<Validation<ModerationReason?>.Invalid>();

    [Fact]
    public void CreateIfNotNull_WhitespaceOnly_IsInvalid()
        => ModerationReason.CreateIfNotNull("   ")
            .ShouldBeOfType<Validation<ModerationReason?>.Invalid>();
}
