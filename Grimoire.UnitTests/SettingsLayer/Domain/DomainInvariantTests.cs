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

    // ── NewMute ───────────────────────────────────────────────────────────────

    private static readonly UserId _userId = new(100UL);
    private static readonly SinId _sinId = new(1L);

    // ── RewardEntry ───────────────────────────────────────────────────────────

    private static readonly RoleId _roleId = new(300UL);

    // ── NewChannelLock ────────────────────────────────────────────────────────

    [Fact]
    public void NewChannelLock_EndTimeEqualsSetAt_IsInvalidWithCorrectMessage()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = NewChannelLock.Create(_modId, reason, _channelId, _guildId, _now, _prevAllowed, _prevDenied, _now);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("End time must be after the lock's set time.");
    }

    [Fact]
    public void NewChannelLock_EndTimeBeforeSetAt_IsInvalid()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = NewChannelLock.Create(_modId, reason, _channelId, _guildId, _now, _prevAllowed, _prevDenied, _past);
        result.ShouldFail().ShouldBeOfType<Invalid>();
    }

    [Fact]
    public void NewChannelLock_EndTimeAfterSetAt_IsValid()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = NewChannelLock.Create(_modId, reason, _channelId, _guildId, _now, _prevAllowed, _prevDenied,
            _future);
        result.ShouldBeOfType<Validation<Error, NewChannelLock>.Success>();
    }

    // ── NewThreadLock ─────────────────────────────────────────────────────────

    [Fact]
    public void NewThreadLock_EndTimeEqualsSetAt_IsInvalidWithCorrectMessage()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = NewThreadLock.Create(_modId, reason, _channelId, _guildId, _now, _now);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("End time must be after the lock's set time.");
    }

    [Fact]
    public void NewThreadLock_EndTimeBeforeSetAt_IsInvalid()
    {
        var reason = ModerationReason.FromDatabase("reason");
        NewThreadLock.Create(_modId, reason, _channelId, _guildId, _now, _past)
            .ShouldFail().ShouldBeOfType<Invalid>();
    }

    [Fact]
    public void NewThreadLock_EndTimeAfterSetAt_IsValid()
    {
        var reason = ModerationReason.FromDatabase("reason");
        NewThreadLock.Create(_modId, reason, _channelId, _guildId, _now, _future)
            .ShouldBeOfType<Validation<Error, NewThreadLock>.Success>();
    }

    [Fact]
    public void NewMute_EndTimeEqualsSetAt_IsInvalidWithMessage()
    {
        var result = NewMute.Create(_userId, _guildId, _modId, _sinId, _now, _now);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("End time must be after the mute's set time.");
    }

    [Fact]
    public void NewMute_EndTimeBeforeSetAt_IsInvalid()
        => NewMute.Create(_userId, _guildId, _modId, _sinId, _now, _past)
            .ShouldFail().ShouldBeOfType<Invalid>();

    [Fact]
    public void NewMute_SelfMute_IsInvalidWithMessage()
    {
        var selfId = new UserId(999UL);
        var result = NewMute.Create(selfId, _guildId, new ModeratorId(999UL), _sinId, _past, _future);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("A moderator cannot mute themselves.");
    }

    [Fact]
    public void NewMute_SinIdZero_IsInvalidWithMessage()
    {
        var result = NewMute.Create(_userId, _guildId, _modId, new SinId(0L), _past, _future);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("Sin Id must be a positive number.");
    }

    [Fact]
    public void NewMute_SinIdNegative_IsInvalid()
        => NewMute.Create(_userId, _guildId, _modId, new SinId(-1L), _past, _future)
            .ShouldFail().ShouldBeOfType<Invalid>();

    [Fact]
    public void NewMute_AllValid_IsValid()
        => NewMute.Create(_userId, _guildId, _modId, _sinId, _past, _future)
            .ShouldBeOfType<Validation<Error, NewMute>.Success>();

    // ── NewChannelLock / NewThreadLock error messages ───────────────────────────

    [Fact]
    public void NewChannelLock_InvalidEndTime_ErrorMessageIsNotEmpty()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = NewChannelLock.Create(_modId, reason, _channelId, _guildId, _now, _prevAllowed, _prevDenied, _now);
        result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe("End time must be after the lock's set time.");
    }

    [Fact]
    public void NewThreadLock_InvalidEndTime_ErrorMessageIsNotEmpty()
    {
        var reason = ModerationReason.FromDatabase("reason");
        var result = NewThreadLock.Create(_modId, reason, _channelId, _guildId, _now, _now);
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
    public void ARewardBelowLevelOne_IsInvalidWithMessage()
    {
        RewardEntry.Create(_roleId, 0, null).ShouldFail().ShouldBeOfType<Invalid>().Message
            .ShouldBe("Reward Level must be at least 1.");
        RewardEntry.Create(_roleId, -1, null).ShouldFail().ShouldBeOfType<Invalid>();
    }

    /// <summary>Q41b (spec 2.5, OQ-3): the lowest reward level is 1, since every member is at least level 1.</summary>
    [Fact]
    public void ARewardAtLevelOne_IsValid()
        => RewardEntry.Create(_roleId, 1, null).ShouldSucceed().ShouldBe(new RewardEntry(_roleId, 1, null));

    [Fact]
    public void ARewardWithAMessage_IsValid()
    {
        var msg = RewardMessage.Create("Congratulations!").ShouldSucceed();
        RewardEntry.Create(_roleId, 10, msg).ShouldSucceed().RewardMessage.ShouldBe(msg);
    }

    // ── What the factories hold ───────────────────────────────────────────────

    [Fact]
    public void TheNewItems_HoldWhatTheyWereGiven()
    {
        var reason = ModerationReason.FromDatabase("raid");
        var role = new RoleId(7UL);

        var mute = NewMute.Create(_userId, _guildId, _modId, _sinId, _past, _future, role).ShouldSucceed();
        (mute.UserId, mute.GuildId, mute.ModeratorId, mute.SinId, mute.EndTime, mute.MuteRoleId)
            .ShouldBe((_userId, _guildId, _modId, _sinId, _future, (RoleId?)role));
        mute.SetAt.ShouldBe(ItemState.Stored(_past));

        var channelLock = NewChannelLock.Create(_modId, reason, _channelId, _guildId, _past,
            new PreviouslyAllowedPermissions(1), new PreviouslyDeniedPermissions(2), _future).ShouldSucceed();
        (channelLock.ModeratorId, channelLock.Reason, channelLock.ChannelId, channelLock.GuildId, channelLock.EndTime)
            .ShouldBe((_modId, (ModerationReason?)reason, _channelId, _guildId, _future));
        (channelLock.PreviouslyAllowed.Permissions, channelLock.PreviouslyDenied.Permissions).ShouldBe((1L, 2L));
        channelLock.SetAt.ShouldBe(ItemState.Stored(_past));

        var threadLock = NewThreadLock.Create(_modId, reason, _channelId, _guildId, _past, _future).ShouldSucceed();
        (threadLock.ModeratorId, threadLock.Reason, threadLock.ChannelId, threadLock.GuildId, threadLock.EndTime)
            .ShouldBe((_modId, (ModerationReason?)reason, _channelId, _guildId, _future));
        threadLock.SetAt.ShouldBe(ItemState.Stored(_past));
    }
}
