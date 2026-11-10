// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain.Test.Unit;

public sealed class SinTests
{
    private static readonly UserId _userId = new(100UL);
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModerationActor _moderator = new ModerationActor.Moderator(new ModeratorId(999UL));
    private static readonly ModerationActor _system = new ModerationActor.System();
    private static readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void ForWarn_BySystem_RecordsTheSystemAsTheActor()
        => Sin.ForWarn(_system, _userId, _guildId, _now).Actor.ShouldBe(_system);

    // ── Sin type assignment ───────────────────────────────────────────────────

    [Fact]
    public void ForWarn_ValidArgs_CreatesSinWithWarnType()
        => Sin.ForWarn(_moderator, _userId, _guildId, _now)
            .SinType.ShouldBe(SinType.Warn);

    [Fact]
    public void ForMute_ValidArgs_CreatesSinWithMuteType()
        => Sin.ForMute(_moderator, _userId, _guildId, _now)
            .SinType.ShouldBe(SinType.Mute);

    [Fact]
    public void ForBan_ValidArgs_CreatesSinWithBanType()
        => Sin.ForBan(_moderator, _userId, _guildId, _now)
            .SinType.ShouldBe(SinType.Ban);

    [Fact]
    public void ForKick_ValidArgs_CreatesSinWithKickType()
        => Sin.ForKick(_moderator, _userId, _guildId, _now)
            .SinType.ShouldBe(SinType.Kick);

    // ── Property assignment ───────────────────────────────────────────────────

    [Fact]
    public void ForWarn_ValidArgs_SetsPropertiesCorrectly()
    {
        var sin = Sin.ForWarn(_moderator, _userId, _guildId, _now);
        sin.UserId.ShouldBe(_userId);
        sin.GuildId.ShouldBe(_guildId);
        sin.Actor.ShouldBe(_moderator);
        sin.SinOn.ShouldBe(_now);
    }

    [Fact]
    public void ForWarn_ValidArgs_CollectionsInitializedEmpty()
    {
        var sin = Sin.ForWarn(_moderator, _userId, _guildId, _now);
        sin.Pardons.ShouldBeEmpty();
        sin.PublishMessages.ShouldBeEmpty();
        sin.ReasonHistory.ShouldBeEmpty();
    }

    // ── Reason (D-2) ──────────────────────────────────────────────────────────

    /// <summary>D-2: a sin given a reason starts its history with it, set by the same actor at the same time.</summary>
    [Fact]
    public void ForBan_WithAReason_StartsTheHistoryWithIt()
    {
        var reason = ModerationReason.Create("spam").ShouldSucceed();

        var history = Sin.ForBan(_moderator, _userId, _guildId, _now, reason).ReasonHistory.ShouldHaveSingleItem();

        (history.Reason, history.Actor, history.SetAt).ShouldBe((reason, (ModerationActor)_moderator, _now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void CreateIfNotBlank_TreatsNothingOrWhitespaceAsNoReason(string? input)
        => ModerationReason.CreateIfNotBlank(input).ShouldSucceed().ShouldBeNull();

    [Fact]
    public void CreateIfNotBlank_KeepsARealReason_Trimmed()
        => ModerationReason.CreateIfNotBlank("  rude  ").ShouldSucceed().ShouldBe(ModerationReason.Create("rude").ShouldSucceed());
}
