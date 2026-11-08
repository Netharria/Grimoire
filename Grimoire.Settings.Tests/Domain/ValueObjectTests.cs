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
    public void RewardMessage_FromDatabase_Null_ReturnsNull()
        => RewardMessage.FromDatabase(null).ShouldBeNull();

    [Fact]
    public void RewardMessage_FromDatabase_ValidString_ReturnsValue()
    {
        var result = RewardMessage.FromDatabase("hello");
        result.ShouldNotBeNull();
        result.Value.Value.ShouldBe("hello");
    }

    // ── FromDatabase (S-1) ───────────────────────────────────────────────────

    /// <summary>S-1: main allowed any positive value, and a guild keeps it. Rejecting it stopped all leveling there.</summary>
    [Fact]
    public void FromDatabase_KeepsALegacyValueOutsideTodaysLimits()
    {
        LevelScalingBase.FromDatabase("1000").Value.ShouldBe(1000);
        LevelScalingModifier.FromDatabase("500").Value.ShouldBe(500);
        XpGainAmount.FromDatabase("250").Value.ShouldBe(250);
        XpTimeoutPeriod.FromDatabase("02:00:00").Value.ShouldBe(TimeSpan.FromHours(2));
    }

    /// <summary>The migration writes total hours, so a timeout of a day or more is past what TimeSpan parses.</summary>
    [Fact]
    public void XpTimeoutPeriod_FromDatabase_ReadsTotalHours()
        => XpTimeoutPeriod.FromDatabase("25:30:00").Value.ShouldBe(TimeSpan.FromHours(25.5));

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    public void FromDatabase_WithNothingReadable_UsesTheDefault(string? stored)
    {
        LevelScalingBase.FromDatabase(stored).ShouldBe(LevelScalingBase.Default);
        LevelScalingModifier.FromDatabase(stored).ShouldBe(LevelScalingModifier.Default);
        XpGainAmount.FromDatabase(stored).ShouldBe(XpGainAmount.Default);
        XpTimeoutPeriod.FromDatabase(stored).ShouldBe(XpTimeoutPeriod.Default);
    }
}
