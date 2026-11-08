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

    // ── UserId validation ─────────────────────────────────────────────────────

    [Fact]
    public void ForWarn_UserIdZero_IsInvalidWithCorrectCode()
    {
        var result = Sin.ForWarn(_moderator, new UserId(0), _guildId, _now);
        result.ShouldBeOfType<Validation<Sin>.Invalid>()
            .Errors.ShouldContain(e => e.Code == "sin.user-id.invalid");
    }

    [Fact]
    public void ForMute_UserIdZero_IsInvalid()
        => Sin.ForMute(_moderator, new UserId(0), _guildId, _now)
            .ShouldBeOfType<Validation<Sin>.Invalid>();

    [Fact]
    public void ForBan_UserIdZero_IsInvalid()
        => Sin.ForBan(_moderator, new UserId(0), _guildId, _now)
            .ShouldBeOfType<Validation<Sin>.Invalid>();

    [Fact]
    public void ForKick_UserIdZero_IsInvalid()
        => Sin.ForKick(_moderator, new UserId(0), _guildId, _now)
            .ShouldBeOfType<Validation<Sin>.Invalid>();

    // ── GuildId validation ────────────────────────────────────────────────────

    [Fact]
    public void ForWarn_GuildIdZero_IsInvalidWithCorrectCode()
    {
        var result = Sin.ForWarn(_moderator, _userId, new GuildId(0), _now);
        result.ShouldBeOfType<Validation<Sin>.Invalid>()
            .Errors.ShouldContain(e => e.Code == "sin.guild-id.invalid");
    }

    // ── ModeratorId validation ────────────────────────────────────────────────

    [Fact]
    public void ForWarn_ModeratorIdZero_IsInvalidWithCorrectCode()
    {
        var zeroModerator = new ModerationActor.Moderator(new ModeratorId(0));
        var result = Sin.ForWarn(zeroModerator, _userId, _guildId, _now);
        result.ShouldBeOfType<Validation<Sin>.Invalid>()
            .Errors.ShouldContain(e => e.Code == "sin.moderator-id.invalid");
    }

    [Fact]
    public void ForWarn_SystemActor_SkipsModeratorIdCheck_IsValid()
        // System actor has no moderator ID — no ID check should apply.
        => Sin.ForWarn(_system, _userId, _guildId, _now)
            .ShouldBeOfType<Validation<Sin>.Valid>();

    // Only the first failing guard is returned (sequential early-exit, not accumulated).
    [Fact]
    public void ForWarn_BothUserIdAndGuildIdZero_ReturnsOnlyFirstError()
    {
        var result = Sin.ForWarn(_moderator, new UserId(0), new GuildId(0), _now);
        var invalid = result.ShouldBeOfType<Validation<Sin>.Invalid>();
        invalid.Errors.ShouldHaveSingleItem().Code.ShouldBe("sin.user-id.invalid");
    }

    // ── Sin type assignment ───────────────────────────────────────────────────

    [Fact]
    public void ForWarn_ValidArgs_CreatesSinWithWarnType()
        => Sin.ForWarn(_moderator, _userId, _guildId, _now)
            .ShouldSucceed().SinType.ShouldBe(SinType.Warn);

    [Fact]
    public void ForMute_ValidArgs_CreatesSinWithMuteType()
        => Sin.ForMute(_moderator, _userId, _guildId, _now)
            .ShouldSucceed().SinType.ShouldBe(SinType.Mute);

    [Fact]
    public void ForBan_ValidArgs_CreatesSinWithBanType()
        => Sin.ForBan(_moderator, _userId, _guildId, _now)
            .ShouldSucceed().SinType.ShouldBe(SinType.Ban);

    [Fact]
    public void ForKick_ValidArgs_CreatesSinWithKickType()
        => Sin.ForKick(_moderator, _userId, _guildId, _now)
            .ShouldSucceed().SinType.ShouldBe(SinType.Kick);

    // ── Property assignment ───────────────────────────────────────────────────

    [Fact]
    public void ForWarn_ValidArgs_SetsPropertiesCorrectly()
    {
        var sin = Sin.ForWarn(_moderator, _userId, _guildId, _now).ShouldSucceed();
        sin.UserId.ShouldBe(_userId);
        sin.GuildId.ShouldBe(_guildId);
        sin.Actor.ShouldBe(_moderator);
        sin.SinOn.ShouldBe(_now);
    }

    [Fact]
    public void ForWarn_ValidArgs_CollectionsInitializedEmpty()
    {
        var sin = Sin.ForWarn(_moderator, _userId, _guildId, _now).ShouldSucceed();
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

        var history = Sin.ForBan(_moderator, _userId, _guildId, _now, reason).ShouldSucceed().ReasonHistory.ShouldHaveSingleItem();

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
