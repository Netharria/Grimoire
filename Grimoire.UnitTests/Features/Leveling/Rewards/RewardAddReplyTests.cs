// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Leveling.Rewards;
using Grimoire.Settings.Domain.Documents;
using Grimoire.Structs;

namespace Grimoire.UnitTests.Features.Leveling.Rewards;

/// <summary>Spec 2.5, UX change 1: <c>/Rewards Add</c> says what it did, and logs only a real change.</summary>
public sealed class RewardAddReplyTests
{
    private static RewardCommandGroup.AddReply Describe(Write<RewardChange> write)
        => RewardCommandGroup.DescribeAdd(write, "@role", "@mod", 5);

    [Fact]
    public void ANewReward_IsAdded()
        => Describe(new Write<RewardChange>.Saved(new RewardChange.Added()))
            .ShouldBe(new RewardCommandGroup.AddReply(GrimoireColor.Green,
                "@role is now a reward at level 5.",
                "@mod added @role as a reward at level 5."));

    /// <summary>Rewards are only ever granted, so the reply says members who have the role keep it.</summary>
    [Fact]
    public void AMovedReward_SaysFromWhere_AndThatMembersKeepIt()
        => Describe(new Write<RewardChange>.Saved(new RewardChange.Moved(3)))
            .ShouldBe(new RewardCommandGroup.AddReply(GrimoireColor.Green,
                "@role moved from level 3 to level 5. Members who already have it keep it.",
                "@mod moved the @role reward from level 3 to level 5."));

    [Fact]
    public void ANewMessage_IsAMessageChange()
        => Describe(new Write<RewardChange>.Saved(new RewardChange.MessageChanged()))
            .ShouldBe(new RewardCommandGroup.AddReply(GrimoireColor.Green,
                "Updated the message for the @role reward (level 5).",
                "@mod updated the message for the @role reward (level 5)."));

    [Fact]
    public void TheRewardAlreadySet_IsYellow_AndNotLogged()
    {
        var expected = new RewardCommandGroup.AddReply(GrimoireColor.Yellow,
            "@role is already a reward at level 5.", null);

        Describe(new Write<RewardChange>.Unchanged("That reward is already set.")).ShouldBe(expected);
        Describe(new Write<RewardChange>.Saved(new RewardChange.Unchanged())).ShouldBe(expected);
    }
}
