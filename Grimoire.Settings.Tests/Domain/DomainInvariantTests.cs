// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Values;

namespace Grimoire.Settings.Tests.Domain;

public sealed class DomainInvariantTests
{
    private static readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private static readonly DateTimeOffset _past = _now.AddHours(-1);
    private static readonly DateTimeOffset _future = _now.AddHours(1);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ChannelId _channelId = new(200UL);
    private static readonly PreviouslyAllowedPermissions _prevAllowed = new(0L);
    private static readonly PreviouslyDeniedPermissions _prevDenied = new(0L);

    // ── ChannelLocked ─────────────────────────────────────────────────────────

    [Fact]
    public void ChannelLocked_EndTimeEqualsSetAt_IsInvalidWithCorrectCode()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = ChannelLocked.Create(_modId, reason, _channelId, _guildId, _now, _prevAllowed, _prevDenied, _now);
        var invalid = result.ShouldBeOfType<Validation<ChannelLocked>.Invalid>();
        invalid.Errors.ShouldContain(e => e.Code == "channel-lock.end-time.invalid");
    }

    [Fact]
    public void ChannelLocked_EndTimeBeforeSetAt_IsInvalid()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = ChannelLocked.Create(_modId, reason, _channelId, _guildId, _now, _prevAllowed, _prevDenied, _past);
        result.ShouldBeOfType<Validation<ChannelLocked>.Invalid>();
    }

    [Fact]
    public void ChannelLocked_EndTimeAfterSetAt_IsValid()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = ChannelLocked.Create(_modId, reason, _channelId, _guildId, _now, _prevAllowed, _prevDenied, _future);
        result.ShouldBeOfType<Validation<ChannelLocked>.Valid>();
    }

    // ── ThreadLocked ──────────────────────────────────────────────────────────

    [Fact]
    public void ThreadLocked_EndTimeEqualsSetAt_IsInvalidWithCorrectCode()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = ThreadLocked.Create(_modId, reason, _channelId, _guildId, _now, _now);
        var invalid = result.ShouldBeOfType<Validation<ThreadLocked>.Invalid>();
        invalid.Errors.ShouldContain(e => e.Code == "thread-lock.end-time.invalid");
    }

    [Fact]
    public void ThreadLocked_EndTimeBeforeSetAt_IsInvalid()
    {
        var reason = ModerationReason.FromDatabase("reason");
        ThreadLocked.Create(_modId, reason, _channelId, _guildId, _now, _past)
            .ShouldBeOfType<Validation<ThreadLocked>.Invalid>();
    }

    [Fact]
    public void ThreadLocked_EndTimeAfterSetAt_IsValid()
    {
        var reason = ModerationReason.FromDatabase("reason");
        ThreadLocked.Create(_modId, reason, _channelId, _guildId, _now, _future)
            .ShouldBeOfType<Validation<ThreadLocked>.Valid>();
    }

    // ── MuteAdded ─────────────────────────────────────────────────────────────

    private static readonly UserId _userId = new(100UL);
    private static readonly SinId _sinId = new(1L);

    [Fact]
    public void MuteAdded_EndTimeEqualsSetAt_IsInvalidWithCodeAndMessage()
    {
        var result = MuteAdded.Create(_userId, _guildId, _modId, _sinId, _now, _now);
        var invalid = result.ShouldBeOfType<Validation<MuteAdded>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("mute.end-time.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void MuteAdded_EndTimeBeforeSetAt_IsInvalid()
        => MuteAdded.Create(_userId, _guildId, _modId, _sinId, _now, _past)
            .ShouldBeOfType<Validation<MuteAdded>.Invalid>();

    [Fact]
    public void MuteAdded_UserIdZero_IsInvalidWithCodeAndMessage()
    {
        var result = MuteAdded.Create(new UserId(0UL), _guildId, _modId, _sinId, _past, _future);
        var invalid = result.ShouldBeOfType<Validation<MuteAdded>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("mute.user-id.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void MuteAdded_ModeratorIdZero_IsInvalidWithCodeAndMessage()
    {
        var result = MuteAdded.Create(_userId, _guildId, new ModeratorId(0UL), _sinId, _past, _future);
        var invalid = result.ShouldBeOfType<Validation<MuteAdded>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("mute.moderator-id.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void MuteAdded_SelfMute_IsInvalidWithCodeAndMessage()
    {
        var selfId = new UserId(999UL);
        var result = MuteAdded.Create(selfId, _guildId, new ModeratorId(999UL), _sinId, _past, _future);
        var invalid = result.ShouldBeOfType<Validation<MuteAdded>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("mute.self-mute.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void MuteAdded_SinIdZero_IsInvalidWithCodeAndMessage()
    {
        var result = MuteAdded.Create(_userId, _guildId, _modId, new SinId(0L), _past, _future);
        var invalid = result.ShouldBeOfType<Validation<MuteAdded>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("mute.sin-id.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void MuteAdded_SinIdNegative_IsInvalid()
        => MuteAdded.Create(_userId, _guildId, _modId, new SinId(-1L), _past, _future)
            .ShouldBeOfType<Validation<MuteAdded>.Invalid>();

    [Fact]
    public void MuteAdded_AllValid_IsValid()
        => MuteAdded.Create(_userId, _guildId, _modId, _sinId, _past, _future)
            .ShouldBeOfType<Validation<MuteAdded>.Valid>();

    // ── ChannelLocked / ThreadLocked error messages ───────────────────────────

    [Fact]
    public void ChannelLocked_InvalidEndTime_ErrorMessageIsNotEmpty()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = ChannelLocked.Create(_modId, reason, _channelId, _guildId, _now, _prevAllowed, _prevDenied, _now);
        var invalid = result.ShouldBeOfType<Validation<ChannelLocked>.Invalid>();
        invalid.Errors.ShouldHaveSingleItem().Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ThreadLocked_InvalidEndTime_ErrorMessageIsNotEmpty()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = ThreadLocked.Create(_modId, reason, _channelId, _guildId, _now, _now);
        var invalid = result.ShouldBeOfType<Validation<ThreadLocked>.Invalid>();
        invalid.Errors.ShouldHaveSingleItem().Message.ShouldNotBeNullOrWhiteSpace();
    }

    // ── RewardAdded ───────────────────────────────────────────────────────────

    private static readonly RoleId _roleId = new(300UL);

    [Fact]
    public void RewardAdded_NegativeLevel_IsInvalidWithCodeAndMessage()
    {
        var result = RewardAdded.Create(_roleId, _guildId, -1, null, _modId, _now);
        var invalid = result.ShouldBeOfType<Validation<RewardAdded>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("reward.reward-level.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RewardAdded_ZeroLevel_IsValid()
        => RewardAdded.Create(_roleId, _guildId, 0, null, _modId, _now)
            .ShouldBeOfType<Validation<RewardAdded>.Valid>();

    [Fact]
    public void RewardAdded_ZeroRoleId_IsInvalidWithCodeAndMessage()
    {
        var result = RewardAdded.Create(new RoleId(0UL), _guildId, 5, null, _modId, _now);
        var invalid = result.ShouldBeOfType<Validation<RewardAdded>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("reward.role-id.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RewardAdded_ZeroModeratorId_IsInvalidWithCodeAndMessage()
    {
        var result = RewardAdded.Create(_roleId, _guildId, 5, null, new ModeratorId(0UL), _now);
        var invalid = result.ShouldBeOfType<Validation<RewardAdded>.Invalid>();
        var error = invalid.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("reward.moderator-id.invalid");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RewardAdded_WithMessage_IsValid()
    {
        var msg = RewardMessage.Create("Congratulations!").OrElse(default);
        RewardAdded.Create(_roleId, _guildId, 10, msg, _modId, _now)
            .ShouldBeOfType<Validation<RewardAdded>.Valid>();
    }
}
