// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Documents;
using Grimoire.Settings.Domain.Values;
using LanguageExt;

namespace Grimoire.UnitTests.SettingsLayer.Domain;

/// <summary>
///     Spec 2.5, section 8: what each collection change does and reports. These were the database tests of the
///     newest-row rule, which the documents no longer need.
/// </summary>
public sealed class CollectionChangeTests
{
    private static readonly RoleId _role = new(1);
    private static readonly RewardMessage? _message = RewardMessage.FromDatabase("Well done");

    // ── Rewards ───────────────────────────────────────────────────────────────

    [Fact]
    public void ARewardForANewRole_IsAdded()
    {
        var (rewards, change) = LevelRewards.Empty.Set(new RewardEntry(_role, 5, null));

        change.ShouldBeOfType<RewardChange.Added>();
        rewards.Entries.ShouldBe([new RewardEntry(_role, 5, null)]);
    }

    [Fact]
    public void ARewardAtAnotherLevel_IsMoved_AndSaysFromWhere()
    {
        var start = LevelRewards.Empty.Set(new RewardEntry(_role, 3, null)).Document;

        var (rewards, change) = start.Set(new RewardEntry(_role, 5, _message));

        change.ShouldBe(new RewardChange.Moved(3));
        rewards.Entries.ShouldBe([new RewardEntry(_role, 5, _message)]);
    }

    [Fact]
    public void ARewardWithANewMessage_AtTheSameLevel_IsAMessageChange()
    {
        var start = LevelRewards.Empty.Set(new RewardEntry(_role, 5, null)).Document;

        var (rewards, change) = start.Set(new RewardEntry(_role, 5, _message));

        change.ShouldBeOfType<RewardChange.MessageChanged>();
        rewards.Roles.Find(_role).ShouldBe(Some(new Reward(5, _message)));
    }

    [Fact]
    public void TheRewardAlreadySet_IsUnchanged()
    {
        var start = LevelRewards.Empty.Set(new RewardEntry(_role, 5, _message)).Document;

        var (rewards, change) = start.Set(new RewardEntry(_role, 5, _message));

        change.ShouldBeOfType<RewardChange.Unchanged>();
        rewards.ShouldBe(start);
    }

    [Fact]
    public void RemovingRewards_ReturnsOnlyTheRolesThatHadOne()
    {
        var start = LevelRewards.Empty.Set(new RewardEntry(_role, 5, null)).Document;

        var (rewards, removed) = start.Remove([_role, new RoleId(2)]);

        removed.ShouldBe(toSet([_role]));
        rewards.ShouldBe(LevelRewards.Empty);
        start.Remove([new RoleId(2)]).Removed.ShouldBeEmpty();
    }

    // ── XP exclusions ─────────────────────────────────────────────────────────

    private static readonly UserId _member = new(20);
    private static readonly RoleId _excludedRole = new(30);
    private static readonly ChannelId _channel = new(10);

    private static XpExclusions Excluding(params XpTarget[] targets) => XpExclusions.Empty.Add(targets).Document;

    private static IReadOnlySet<RoleId> Roles(params RoleId[] roles) => new System.Collections.Generic.HashSet<RoleId>(roles);

    [Fact]
    public void AddingExclusions_ReturnsOnlyTheNewOnes()
    {
        var start = Excluding(new XpTarget.Member(_member));

        var (exclusions, added) = start.Add([new XpTarget.Member(_member), new XpTarget.Channel(_channel)]);

        added.ShouldBe([new XpTarget.Channel(_channel)]);
        exclusions.Targets.ShouldBe([new XpTarget.Member(_member), new XpTarget.Channel(_channel)],
            ignoreOrder: true);
    }

    [Fact]
    public void RemovingExclusions_ReturnsOnlyTheOnesThatWereExcluded()
    {
        var start = Excluding(new XpTarget.Member(_member), new XpTarget.Role(_excludedRole));

        var (exclusions, removed) = start.Remove([new XpTarget.Member(_member), new XpTarget.Channel(_channel)]);

        removed.ShouldBe([new XpTarget.Member(_member)]);
        exclusions.Targets.ShouldBe([new XpTarget.Role(_excludedRole)]);
    }

    [Fact]
    public void AMessage_IsExcludedByItsChannel_ItsParent_ItsMember_OrARole()
    {
        var exclusions = Excluding(new XpTarget.Channel(_channel), new XpTarget.Member(_member),
            new XpTarget.Role(_excludedRole));
        IReadOnlySet<RoleId> noRoles = new System.Collections.Generic.HashSet<RoleId>();
        var other = new UserId(99);

        exclusions.ExcludesMessage(other, noRoles, _channel, null).ShouldBeTrue();
        exclusions.ExcludesMessage(other, noRoles, new ChannelId(11), _channel).ShouldBeTrue();
        exclusions.ExcludesMessage(_member, noRoles, new ChannelId(11), null).ShouldBeTrue();
        exclusions.ExcludesMessage(other, Roles(_excludedRole), new ChannelId(11), null).ShouldBeTrue();
        exclusions.ExcludesMessage(other, Roles(new RoleId(31)), new ChannelId(11), new ChannelId(12)).ShouldBeFalse();
    }

    [Fact]
    public void AMember_IsExcludedByThemselvesOrARole_NeverByAChannel()
    {
        var exclusions = Excluding(new XpTarget.Channel(new ChannelId(_member.Value)),
            new XpTarget.Role(_excludedRole));

        exclusions.ExcludesMember(_member, Roles()).ShouldBeFalse();
        exclusions.ExcludesMember(_member, Roles(_excludedRole)).ShouldBeTrue();
        Excluding(new XpTarget.Member(_member)).ExcludesMember(_member, Roles()).ShouldBeTrue();
    }

    [Fact]
    public void Excludes_ChecksEachKindOfTarget()
    {
        var exclusions = Excluding(new XpTarget.Channel(_channel), new XpTarget.Member(_member),
            new XpTarget.Role(_excludedRole));

        exclusions.Excludes(new XpTarget.Channel(_channel)).ShouldBeTrue();
        exclusions.Excludes(new XpTarget.Member(_member)).ShouldBeTrue();
        exclusions.Excludes(new XpTarget.Role(_excludedRole)).ShouldBeTrue();
        // The same number as another kind is a different target.
        exclusions.Excludes(new XpTarget.Role(new RoleId(_member.Value))).ShouldBeFalse();
    }

    // ── Diagnostics ignores ───────────────────────────────────────────────────

    [Fact]
    public void IgnoringDiagnosticsTargets_ReturnsOnlyTheNewOnes_InTheOrderGiven()
    {
        var start = DiagnosticsIgnoreList.Empty.Add([new DiagnosticsTarget.Role(new RoleId(5))]).Document;

        var (ignores, added) = start.Add([
            new DiagnosticsTarget.Member(new UserId(9)), new DiagnosticsTarget.Role(new RoleId(5)),
            new DiagnosticsTarget.Role(new RoleId(1)), new DiagnosticsTarget.Member(new UserId(9))
        ]);

        added.ShouldBe([new DiagnosticsTarget.Member(new UserId(9)), new DiagnosticsTarget.Role(new RoleId(1))]);
        ignores.Ignores(new DiagnosticsTarget.Role(new RoleId(1))).ShouldBeTrue();
        ignores.Targets.Count.ShouldBe(3);
    }

    [Fact]
    public void UnignoringDiagnosticsTargets_ReturnsOnlyTheOnesThatWereIgnored()
    {
        var start = DiagnosticsIgnoreList.Empty.Add([
            new DiagnosticsTarget.Role(new RoleId(5)), new DiagnosticsTarget.Member(new UserId(9))
        ]).Document;

        var (ignores, removed) = start.Remove([
            new DiagnosticsTarget.Member(new UserId(9)), new DiagnosticsTarget.Member(new UserId(10))
        ]);

        removed.ShouldBe([new DiagnosticsTarget.Member(new UserId(9))]);
        ignores.Targets.ShouldBe([new DiagnosticsTarget.Role(new RoleId(5))]);
    }

    [Fact]
    public void UnignoringADiagnosticsRole_RemovesOnlyThatRole()
    {
        var start = DiagnosticsIgnoreList.Empty.Add([
            new DiagnosticsTarget.Role(new RoleId(5)), new DiagnosticsTarget.Role(new RoleId(6))
        ]).Document;

        var (ignores, removed) = start.Remove([new DiagnosticsTarget.Role(new RoleId(5))]);

        removed.ShouldBe([new DiagnosticsTarget.Role(new RoleId(5))]);
        ignores.Targets.ShouldBe([new DiagnosticsTarget.Role(new RoleId(6))]);
    }

    // ── Channel overrides ─────────────────────────────────────────────────────

    private static readonly ChannelId _category = new(1);
    private static readonly ChannelId _text = new(2);
    private static readonly ChannelId _thread = new(3);

    private static readonly Dictionary<ChannelId, ChannelId?> _parents = new()
    {
        [_category] = null, [_text] = _category, [_thread] = _text
    };

    [Fact]
    public void WithNone_ClearsTheOverride_AndSettingTheSameValueChangesNothing()
    {
        var overrides = MessageLogOverrides.Empty.With(_text, MessageLogOverrideOption.NeverLog);

        overrides.For(_text).ShouldBe(Some(MessageLogOverrideOption.NeverLog));
        overrides.With(_text, None).ShouldBe(MessageLogOverrides.Empty);
        overrides.With(_text, MessageLogOverrideOption.NeverLog).ShouldBe(overrides);
        SpamFilterOverrides.Empty.With(_text, SpamFilterOverrideOption.NeverFilter).With(_text, None)
            .ShouldBe(SpamFilterOverrides.Empty);
    }

    [Fact]
    public void WithNoOverrideAnywhere_AMessageIsLogged()
        => MessageLogOverrides.Empty.ShouldLog(_thread, _parents).ShouldBeTrue();

    [Theory]
    [InlineData(1UL, false)] // on the category, two levels up
    [InlineData(2UL, false)] // on the parent channel
    [InlineData(3UL, false)] // on the thread itself
    public void AnOverride_OnTheChannelOrAnyParent_Decides(ulong where, bool expected)
        => MessageLogOverrides.Empty.With(new ChannelId(where), MessageLogOverrideOption.NeverLog)
            .ShouldLog(_thread, _parents).ShouldBe(expected);

    [Fact]
    public void TheNearestOverride_Wins()
    {
        var overrides = MessageLogOverrides.Empty
            .With(_category, MessageLogOverrideOption.NeverLog)
            .With(_text, MessageLogOverrideOption.AlwaysLog);

        overrides.ShouldLog(_thread, _parents).ShouldBeTrue();
        overrides.With(_thread, MessageLogOverrideOption.NeverLog).ShouldLog(_thread, _parents).ShouldBeFalse();
    }

    [Fact]
    public void AChannelMissingFromTheMap_IsItsOwnRoot()
        => MessageLogOverrides.Empty.With(_category, MessageLogOverrideOption.NeverLog)
            .ShouldLog(new ChannelId(50), _parents).ShouldBeTrue();

    /// <summary>The walk reaches the last parent in the map: one step per entry, plus the channel itself.</summary>
    [Fact]
    public void AnOverride_OnTheLastParent_IsReached()
    {
        Dictionary<ChannelId, ChannelId?> parents = new() { [_thread] = _text };

        MessageLogOverrides.Empty.With(_text, MessageLogOverrideOption.NeverLog)
            .ShouldLog(_thread, parents).ShouldBeFalse();
    }

    /// <summary>Discord can't make a loop, but the walk is total: it stops, then logs as with no override.</summary>
    [Fact]
    public void ALoopInTheParents_Stops_AndLogs()
    {
        Dictionary<ChannelId, ChannelId?> loop = new() { [_text] = _thread, [_thread] = _text };

        MessageLogOverrides.Empty.With(_category, MessageLogOverrideOption.NeverLog)
            .ShouldLog(_thread, loop).ShouldBeTrue();
    }

    // ── Ids ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Ids_OrderByTheirValue()
    {
        new ChannelId(9).CompareTo(new ChannelId(10)).ShouldBeLessThan(0);
        new RoleId(10).CompareTo(new RoleId(9)).ShouldBeGreaterThan(0);
        new UserId(5).CompareTo(new UserId(5)).ShouldBe(0);
        new GuildId(ulong.MaxValue).CompareTo(new GuildId(1)).ShouldBeGreaterThan(0);
    }
}
