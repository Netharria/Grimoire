// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Values;
using LanguageExt;

namespace Grimoire.UnitTests.SettingsLayer.Domain;

public sealed class ValueObjectTests
{
    // ── LevelScalingBase ──────────────────────────────────────────────────────

    [Fact]
    public void LevelScalingBase_MinExact_IsValid()
        => LevelScalingBase.Create(1).ShouldBeOfType<Validation<Error, LevelScalingBase>.Success>();

    [Fact]
    public void LevelScalingBase_MaxExact_IsValid()
        => LevelScalingBase.Create(500).ShouldBeOfType<Validation<Error, LevelScalingBase>.Success>();

    [Fact]
    public void LevelScalingBase_BelowMin_IsInvalidWithMessage()
    {
        var result = LevelScalingBase.Create(0);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("Level scaling base must be a value between 1 and 500.");
    }

    [Fact]
    public void LevelScalingBase_AboveMax_IsInvalidWithMessage()
    {
        var result = LevelScalingBase.Create(501);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("Level scaling base must be a value between 1 and 500.");
    }

    // ── LevelScalingModifier ─────────────────────────────────────────────────

    [Fact]
    public void LevelScalingModifier_MinExact_IsValid()
        => LevelScalingModifier.Create(1).ShouldBeOfType<Validation<Error, LevelScalingModifier>.Success>();

    [Fact]
    public void LevelScalingModifier_MaxExact_IsValid()
        => LevelScalingModifier.Create(200).ShouldBeOfType<Validation<Error, LevelScalingModifier>.Success>();

    [Fact]
    public void LevelScalingModifier_BelowMin_IsInvalidWithMessage()
    {
        var result = LevelScalingModifier.Create(0);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("Level scaling modifier must be a value between 1 and 200.");
    }

    [Fact]
    public void LevelScalingModifier_AboveMax_IsInvalidWithMessage()
    {
        var result = LevelScalingModifier.Create(201);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("Level scaling modifier must be a value between 1 and 200.");
    }

    // ── XpGainAmount ─────────────────────────────────────────────────────────

    [Fact]
    public void XpGainAmount_MinExact_IsValid()
        => XpGainAmount.Create(1).ShouldBeOfType<Validation<Error, XpGainAmount>.Success>();

    [Fact]
    public void XpGainAmount_MaxExact_IsValid()
        => XpGainAmount.Create(100).ShouldBeOfType<Validation<Error, XpGainAmount>.Success>();

    [Fact]
    public void XpGainAmount_BelowMin_IsInvalidWithMessage()
    {
        var result = XpGainAmount.Create(0);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("Xp gain amount must be a value between 1 and 100.");
    }

    [Fact]
    public void XpGainAmount_AboveMax_IsInvalidWithMessage()
    {
        var result = XpGainAmount.Create(101);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("Xp gain amount must be a value between 1 and 100.");
    }

    // ── XpTimeoutPeriod ──────────────────────────────────────────────────────

    [Fact]
    public void XpTimeoutPeriod_MinExact_IsValid()
        => XpTimeoutPeriod.Create(1).ShouldBeOfType<Validation<Error, XpTimeoutPeriod>.Success>();

    [Fact]
    public void XpTimeoutPeriod_MaxExact_IsValid()
        => XpTimeoutPeriod.Create(60).ShouldBeOfType<Validation<Error, XpTimeoutPeriod>.Success>();

    [Fact]
    public void XpTimeoutPeriod_BelowMin_IsInvalidWithMessage()
    {
        var result = XpTimeoutPeriod.Create(0);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("Xp timeout period must be a value between 1 and 60 in minutes.");
    }

    [Fact]
    public void XpTimeoutPeriod_AboveMax_IsInvalidWithMessage()
    {
        var result = XpTimeoutPeriod.Create(61);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("Xp timeout period must be a value between 1 and 60 in minutes.");
    }

    // ── RewardMessage ────────────────────────────────────────────────────────

    [Fact]
    public void RewardMessage_ExactMaxLength_IsValid()
        => RewardMessage.Create(new string('a', 4096)).ShouldBeOfType<Validation<Error, RewardMessage>.Success>();

    [Fact]
    public void RewardMessage_ExceedsMaxLength_IsInvalidWithMessage()
    {
        var result = RewardMessage.Create(new string('a', 4097));
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("Reward message must be 1\u20134096 non-whitespace characters.");
    }

    [Fact]
    public void RewardMessage_Null_IsInvalidWithMessage()
    {
        var result = RewardMessage.Create(null);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("Reward message must be 1\u20134096 non-whitespace characters.");
    }

    [Fact]
    public void RewardMessage_Empty_IsInvalid()
        => RewardMessage.Create(string.Empty).ShouldFail().ShouldBeOfType<Invalid>();

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
