// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Values;

namespace Grimoire.Settings.Tests.Domain;

public sealed class ValueObjectTests
{
    // ── LevelScalingBase ──────────────────────────────────────────────────────

    [Fact]
    public void LevelScalingBase_MinExact_IsValid()
        => LevelScalingBase.Create(1).ShouldBeOfType<Validation<LevelScalingBase>.Valid>();

    [Fact]
    public void LevelScalingBase_MaxExact_IsValid()
        => LevelScalingBase.Create(500).ShouldBeOfType<Validation<LevelScalingBase>.Valid>();

    [Fact]
    public void LevelScalingBase_BelowMin_IsInvalidWithCodeAndMessage()
    {
        var result = LevelScalingBase.Create(0);
        var invalid = result.ShouldBeOfType<Validation<LevelScalingBase>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("level-scaling-base.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void LevelScalingBase_AboveMax_IsInvalidWithCode()
    {
        var result = LevelScalingBase.Create(501);
        var invalid = result.ShouldBeOfType<Validation<LevelScalingBase>.Invalid>();
        invalid.Errors.ShouldContain(e => e.Code == "level-scaling-base.invalid");
    }

    [Fact]
    public void LevelScalingBase_Null_IsInvalid()
        => LevelScalingBase.Create((int?)null).ShouldBeOfType<Validation<LevelScalingBase>.Invalid>();

    [Fact]
    public void LevelScalingBase_StringMin_IsValid()
        => LevelScalingBase.Create("1").ShouldBeOfType<Validation<LevelScalingBase>.Valid>();

    [Fact]
    public void LevelScalingBase_StringMax_IsValid()
        => LevelScalingBase.Create("500").ShouldBeOfType<Validation<LevelScalingBase>.Valid>();

    [Fact]
    public void LevelScalingBase_StringBelowMin_IsInvalidWithCodeAndMessage()
    {
        var result = LevelScalingBase.Create("0");
        var invalid = result.ShouldBeOfType<Validation<LevelScalingBase>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("level-scaling-base.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void LevelScalingBase_StringNull_IsInvalid()
        => LevelScalingBase.Create((string?)null).ShouldBeOfType<Validation<LevelScalingBase>.Invalid>();

    [Fact]
    public void LevelScalingBase_StringNonNumeric_IsInvalid()
        => LevelScalingBase.Create("abc").ShouldBeOfType<Validation<LevelScalingBase>.Invalid>();

    // ── LevelScalingModifier ─────────────────────────────────────────────────

    [Fact]
    public void LevelScalingModifier_MinExact_IsValid()
        => LevelScalingModifier.Create(1).ShouldBeOfType<Validation<LevelScalingModifier>.Valid>();

    [Fact]
    public void LevelScalingModifier_MaxExact_IsValid()
        => LevelScalingModifier.Create(200).ShouldBeOfType<Validation<LevelScalingModifier>.Valid>();

    [Fact]
    public void LevelScalingModifier_BelowMin_IsInvalidWithCodeAndMessage()
    {
        var result = LevelScalingModifier.Create(0);
        var invalid = result.ShouldBeOfType<Validation<LevelScalingModifier>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("level-scaling-modifier.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void LevelScalingModifier_AboveMax_IsInvalidWithCode()
    {
        var result = LevelScalingModifier.Create(201);
        var invalid = result.ShouldBeOfType<Validation<LevelScalingModifier>.Invalid>();
        invalid.Errors.ShouldContain(e => e.Code == "level-scaling-modifier.invalid");
    }

    [Fact]
    public void LevelScalingModifier_Null_IsInvalid()
        => LevelScalingModifier.Create((int?)null).ShouldBeOfType<Validation<LevelScalingModifier>.Invalid>();

    [Fact]
    public void LevelScalingModifier_StringMin_IsValid()
        => LevelScalingModifier.Create("1").ShouldBeOfType<Validation<LevelScalingModifier>.Valid>();

    [Fact]
    public void LevelScalingModifier_StringMax_IsValid()
        => LevelScalingModifier.Create("200").ShouldBeOfType<Validation<LevelScalingModifier>.Valid>();

    [Fact]
    public void LevelScalingModifier_StringBelowMin_IsInvalidWithCodeAndMessage()
    {
        var result = LevelScalingModifier.Create("0");
        var invalid = result.ShouldBeOfType<Validation<LevelScalingModifier>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("level-scaling-modifier.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    // ── XpGainAmount ─────────────────────────────────────────────────────────

    [Fact]
    public void XpGainAmount_MinExact_IsValid()
        => XpGainAmount.Create(1).ShouldBeOfType<Validation<XpGainAmount>.Valid>();

    [Fact]
    public void XpGainAmount_MaxExact_IsValid()
        => XpGainAmount.Create(100).ShouldBeOfType<Validation<XpGainAmount>.Valid>();

    [Fact]
    public void XpGainAmount_BelowMin_IsInvalidWithCodeAndMessage()
    {
        var result = XpGainAmount.Create(0);
        var invalid = result.ShouldBeOfType<Validation<XpGainAmount>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("xp-gain-amount.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void XpGainAmount_AboveMax_IsInvalidWithCode()
    {
        var result = XpGainAmount.Create(101);
        var invalid = result.ShouldBeOfType<Validation<XpGainAmount>.Invalid>();
        invalid.Errors.ShouldContain(e => e.Code == "xp-gain-amount.invalid");
    }

    [Fact]
    public void XpGainAmount_Null_IsInvalid()
        => XpGainAmount.Create((int?)null).ShouldBeOfType<Validation<XpGainAmount>.Invalid>();

    [Fact]
    public void XpGainAmount_StringMin_IsValid()
        => XpGainAmount.Create("1").ShouldBeOfType<Validation<XpGainAmount>.Valid>();

    [Fact]
    public void XpGainAmount_StringMax_IsValid()
        => XpGainAmount.Create("100").ShouldBeOfType<Validation<XpGainAmount>.Valid>();

    [Fact]
    public void XpGainAmount_StringBelowMin_IsInvalidWithCodeAndMessage()
    {
        var result = XpGainAmount.Create("0");
        var invalid = result.ShouldBeOfType<Validation<XpGainAmount>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("xp-gain-amount.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    // ── XpTimeoutPeriod ──────────────────────────────────────────────────────

    [Fact]
    public void XpTimeoutPeriod_MinExact_IsValid()
        => XpTimeoutPeriod.Create(1).ShouldBeOfType<Validation<XpTimeoutPeriod>.Valid>();

    [Fact]
    public void XpTimeoutPeriod_MaxExact_IsValid()
        => XpTimeoutPeriod.Create(60).ShouldBeOfType<Validation<XpTimeoutPeriod>.Valid>();

    [Fact]
    public void XpTimeoutPeriod_BelowMin_IsInvalidWithCodeAndMessage()
    {
        var result = XpTimeoutPeriod.Create(0);
        var invalid = result.ShouldBeOfType<Validation<XpTimeoutPeriod>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("xp-timeout-period.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void XpTimeoutPeriod_AboveMax_IsInvalidWithCode()
    {
        var result = XpTimeoutPeriod.Create(61);
        var invalid = result.ShouldBeOfType<Validation<XpTimeoutPeriod>.Invalid>();
        invalid.Errors.ShouldContain(e => e.Code == "xp-timeout-period.invalid");
    }

    [Fact]
    public void XpTimeoutPeriod_Null_IsInvalid()
        => XpTimeoutPeriod.Create((int?)null).ShouldBeOfType<Validation<XpTimeoutPeriod>.Invalid>();

    [Fact]
    public void XpTimeoutPeriod_StringOneMinute_IsValid()
        => XpTimeoutPeriod.Create("00:01:00").ShouldBeOfType<Validation<XpTimeoutPeriod>.Valid>();

    [Fact]
    public void XpTimeoutPeriod_StringSixtyMinutes_IsValid()
        => XpTimeoutPeriod.Create("01:00:00").ShouldBeOfType<Validation<XpTimeoutPeriod>.Valid>();

    [Fact]
    public void XpTimeoutPeriod_StringZeroMinutes_IsInvalidWithCodeAndMessage()
    {
        var result = XpTimeoutPeriod.Create("00:00:00");
        var invalid = result.ShouldBeOfType<Validation<XpTimeoutPeriod>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("xp-timeout-period.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void XpTimeoutPeriod_StringAboveMax_IsInvalidWithCodeAndMessage()
    {
        var result = XpTimeoutPeriod.Create("01:01:00");
        var invalid = result.ShouldBeOfType<Validation<XpTimeoutPeriod>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("xp-timeout-period.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void XpTimeoutPeriod_StringNull_IsInvalid()
        => XpTimeoutPeriod.Create((string?)null).ShouldBeOfType<Validation<XpTimeoutPeriod>.Invalid>();

    [Fact]
    public void XpTimeoutPeriod_StringWhitespace_IsInvalid()
        => XpTimeoutPeriod.Create("   ").ShouldBeOfType<Validation<XpTimeoutPeriod>.Invalid>();

    [Fact]
    public void XpTimeoutPeriod_StringNonParseable_IsInvalid()
        => XpTimeoutPeriod.Create("not-a-timespan").ShouldBeOfType<Validation<XpTimeoutPeriod>.Invalid>();

    // ── RewardMessage ────────────────────────────────────────────────────────

    [Fact]
    public void RewardMessage_ExactMaxLength_IsValid()
        => RewardMessage.Create(new string('a', 4096)).ShouldBeOfType<Validation<RewardMessage>.Valid>();

    [Fact]
    public void RewardMessage_ExceedsMaxLength_IsInvalidWithCodeAndMessage()
    {
        var result = RewardMessage.Create(new string('a', 4097));
        var invalid = result.ShouldBeOfType<Validation<RewardMessage>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("reward-message.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RewardMessage_Null_IsInvalidWithCodeAndMessage()
    {
        var result = RewardMessage.Create(null);
        var invalid = result.ShouldBeOfType<Validation<RewardMessage>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("reward-message.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RewardMessage_Empty_IsInvalid()
        => RewardMessage.Create(string.Empty).ShouldBeOfType<Validation<RewardMessage>.Invalid>();

    [Fact]
    public void RewardMessage_Whitespace_IsInvalid()
        => RewardMessage.Create("   ").ShouldBeOfType<Validation<RewardMessage>.Invalid>();

    [Fact]
    public void RewardMessage_ValidText_TrimsWhitespace()
    {
        var result = RewardMessage.Create("  hello  ");
        var valid = result.ShouldBeOfType<Validation<RewardMessage>.Valid>();
        valid.Value.Value.ShouldBe("hello");
    }

    [Fact]
    public void RewardMessage_FromDatabase_Null_ReturnsNull()
        => RewardMessage.FromDatabase(null).ShouldBeNull();

    [Fact]
    public void RewardMessage_FromDatabase_ValidString_ReturnsValue()
    {
        var result = RewardMessage.FromDatabase("hello");
        result.ShouldNotBeNull();
        result.Value.Value.ShouldBe("hello");
    }
}
