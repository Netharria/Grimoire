// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Values;
using LanguageExt;

namespace Grimoire.UnitTests.SettingsLayer.Domain;

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

    // ── MuteAdded ─────────────────────────────────────────────────────────────

    private static readonly UserId _userId = new(100UL);
    private static readonly SinId _sinId = new(1L);

    // ── RewardAdded ───────────────────────────────────────────────────────────

    private static readonly RoleId _roleId = new(300UL);

    // ── ChannelLocked ─────────────────────────────────────────────────────────

    [Fact]
    public void ChannelLocked_EndTimeEqualsSetAt_IsInvalidWithCorrectMessage()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = ChannelLocked.Create(_modId, reason, _channelId, _guildId, _now, _prevAllowed, _prevDenied, _now);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("End time must be after the lock's set time.");
    }

    [Fact]
    public void ChannelLocked_EndTimeBeforeSetAt_IsInvalid()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = ChannelLocked.Create(_modId, reason, _channelId, _guildId, _now, _prevAllowed, _prevDenied, _past);
        result.ShouldFail().ShouldBeOfType<Invalid>();
    }

    [Fact]
    public void ChannelLocked_EndTimeAfterSetAt_IsValid()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = ChannelLocked.Create(_modId, reason, _channelId, _guildId, _now, _prevAllowed, _prevDenied,
            _future);
        result.ShouldBeOfType<Validation<Error, ChannelLocked>.Success>();
    }

    // ── ThreadLocked ──────────────────────────────────────────────────────────

    [Fact]
    public void ThreadLocked_EndTimeEqualsSetAt_IsInvalidWithCorrectMessage()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = ThreadLocked.Create(_modId, reason, _channelId, _guildId, _now, _now);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("End time must be after the lock's set time.");
    }

    [Fact]
    public void ThreadLocked_EndTimeBeforeSetAt_IsInvalid()
    {
        var reason = ModerationReason.FromDatabase("reason");
        ThreadLocked.Create(_modId, reason, _channelId, _guildId, _now, _past)
            .ShouldFail().ShouldBeOfType<Invalid>();
    }

    [Fact]
    public void ThreadLocked_EndTimeAfterSetAt_IsValid()
    {
        var reason = ModerationReason.FromDatabase("reason");
        ThreadLocked.Create(_modId, reason, _channelId, _guildId, _now, _future)
            .ShouldBeOfType<Validation<Error, ThreadLocked>.Success>();
    }

    [Fact]
    public void MuteAdded_EndTimeEqualsSetAt_IsInvalidWithMessage()
    {
        var result = MuteAdded.Create(_userId, _guildId, _modId, _sinId, _now, _now);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("End time must be after the mute's set time.");
    }

    [Fact]
    public void MuteAdded_EndTimeBeforeSetAt_IsInvalid()
        => MuteAdded.Create(_userId, _guildId, _modId, _sinId, _now, _past)
            .ShouldFail().ShouldBeOfType<Invalid>();

    [Fact]
    public void MuteAdded_SelfMute_IsInvalidWithMessage()
    {
        var selfId = new UserId(999UL);
        var result = MuteAdded.Create(selfId, _guildId, new ModeratorId(999UL), _sinId, _past, _future);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("A moderator cannot mute themselves.");
    }

    [Fact]
    public void MuteAdded_SinIdZero_IsInvalidWithMessage()
    {
        var result = MuteAdded.Create(_userId, _guildId, _modId, new SinId(0L), _past, _future);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("Sin Id must be a positive number.");
    }

    [Fact]
    public void MuteAdded_SinIdNegative_IsInvalid()
        => MuteAdded.Create(_userId, _guildId, _modId, new SinId(-1L), _past, _future)
            .ShouldFail().ShouldBeOfType<Invalid>();

    [Fact]
    public void MuteAdded_AllValid_IsValid()
        => MuteAdded.Create(_userId, _guildId, _modId, _sinId, _past, _future)
            .ShouldBeOfType<Validation<Error, MuteAdded>.Success>();

    // ── ChannelLocked / ThreadLocked error messages ───────────────────────────

    [Fact]
    public void ChannelLocked_InvalidEndTime_ErrorMessageIsNotEmpty()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = ChannelLocked.Create(_modId, reason, _channelId, _guildId, _now, _prevAllowed, _prevDenied, _now);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("End time must be after the lock's set time.");
    }

    [Fact]
    public void ThreadLocked_InvalidEndTime_ErrorMessageIsNotEmpty()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = ThreadLocked.Create(_modId, reason, _channelId, _guildId, _now, _now);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("End time must be after the lock's set time.");
    }

    // ── ModerationReason ──────────────────────────────────────────────────────

    [Fact]
    public void ModerationReason_WhitespaceInput_IsInvalidWithErrorCode()
    {
        var result = ModerationReason.Create("   ");
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("Reason must be 1\u20131000 non-whitespace characters.");
    }

    [Fact]
    public void RewardAdded_NegativeLevel_IsInvalidWithMessage()
    {
        var result = RewardAdded.Create(_roleId, _guildId, -1, null, _modId, _now);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("Reward Level must be greater than or equal to zero.");
    }

    [Fact]
    public void RewardAdded_ZeroLevel_IsValid()
        => RewardAdded.Create(_roleId, _guildId, 0, null, _modId, _now)
            .ShouldBeOfType<Validation<Error, RewardAdded>.Success>();

    [Fact]
    public void RewardAdded_WithMessage_IsValid()
    {
        var msg = RewardMessage.Create("Congratulations!").ShouldSucceed();
        RewardAdded.Create(_roleId, _guildId, 10, msg, _modId, _now)
            .ShouldBeOfType<Validation<Error, RewardAdded>.Success>();
    }
}
