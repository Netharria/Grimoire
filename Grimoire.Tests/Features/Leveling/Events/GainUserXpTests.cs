// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Leveling.Events;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Values;

namespace Grimoire.Tests.Features.Leveling.Events;

public sealed class GainUserXpBuildEarnedXpTests
{
    private static readonly UserId _userId = new(1UL);
    private static readonly GuildId _guildId = new(1UL);

    [Fact]
    public void BuildEarnedXp_UsesConfiguredGainAmount()
    {
        var settings = new LevelingSettingEntry(
            XpTimeoutPeriod.Default,
            LevelScalingModifier.Default,
            LevelScalingBase.Default,
            XpGainAmount.Create(20).ShouldSucceed());

        var earned = GainUserXp.BuildEarnedXp(settings, _userId, _guildId);

        earned.Xp.Value.ShouldBe(20);
        earned.UserId.ShouldBe(_userId);
        earned.GuildId.ShouldBe(_guildId);
    }

    [Fact]
    public void BuildEarnedXp_TimeOutIsNowPlusConfiguredTimeoutPeriod()
    {
        var settings = new LevelingSettingEntry(
            XpTimeoutPeriod.Create(10).ShouldSucceed(),
            LevelScalingModifier.Default,
            LevelScalingBase.Default,
            XpGainAmount.Default);

        var before = DateTimeOffset.UtcNow;
        var earned = GainUserXp.BuildEarnedXp(settings, _userId, _guildId);
        var after = DateTimeOffset.UtcNow;

        earned.TimeOut.ShouldBeInRange(before.AddMinutes(10), after.AddMinutes(10));
    }
}
