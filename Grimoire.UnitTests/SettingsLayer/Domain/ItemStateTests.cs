// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.UnitTests.SettingsLayer.Domain;

/// <summary>
///     TD-1: mutes and locks move only forward, Pending → Active, and Pending or Active → Expired or Removed. The
///     tables' check constraints hold the same rules (spec 2.2, section 6.1).
/// </summary>
public sealed class ItemStateTests
{
    private static readonly DateTimeOffset _at = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly ModeratorId _moderator = new(5);

    public static TheoryData<string> AllStates => [nameof(ItemState.Pending), nameof(ItemState.Active),
        nameof(ItemState.Expired), nameof(ItemState.Removed)];

    [Fact]
    public void OnlyPending_CanBeActivated()
    {
        Pending.Activate(_at).ShouldSucceed().ShouldBe(new ItemState.Active(_at));
        Active.Activate(_at).ShouldFail().ShouldBeOfType<Invalid>();
        Expired.Activate(_at).ShouldFail().ShouldBeOfType<Invalid>();
        Removed.Activate(_at).ShouldFail().ShouldBeOfType<Invalid>();
    }

    [Theory]
    [MemberData(nameof(AllStates))]
    public void OnlyAnItemInForce_CanExpireOrBeRemoved(string name)
    {
        var state = StateNamed(name);

        if (state.InForce)
        {
            state.Expire(_at).ShouldSucceed().ShouldBe(new ItemState.Expired(_at));
            state.Remove(_moderator, null, _at).ShouldSucceed().ShouldBe(new ItemState.Removed(_at, _moderator, null));
        }
        else
        {
            state.Expire(_at).ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe($"An item that is {name} can't be expired.");
            state.Remove(_moderator, null, _at).ShouldFail().ShouldBeOfType<Invalid>();
        }
    }

    [Fact]
    public void ARefusal_SaysWhatCannotHappen()
    {
        Active.Activate(_at).ShouldFail().Message.ShouldBe("An item that is Active can't be activated.");
        Expired.Remove(_moderator, null, _at).ShouldFail().Message.ShouldBe("An item that is Expired can't be removed.");
    }

    /// <summary>A lock's set time is part of its key, so it is held to the microsecond, as Postgres stores it.</summary>
    [Fact]
    public void Stored_TruncatesToTheMicrosecond()
    {
        var time = _at.AddTicks(TimeSpan.TicksPerMicrosecond * 3 + 7);

        ItemState.Stored(time).ShouldBe(_at.AddTicks(TimeSpan.TicksPerMicrosecond * 3));
        ItemState.Stored(_at).ShouldBe(_at);
    }

    [Theory]
    [MemberData(nameof(AllStates))]
    public void InForce_IsPendingOrActive(string name)
        => StateNamed(name).InForce.ShouldBe(name is nameof(ItemState.Pending) or nameof(ItemState.Active));

    [Theory]
    [MemberData(nameof(AllStates))]
    public void TheStoredName_IsTheStatesName(string name)
        => StateNamed(name).Name.ShouldBe(name);

    [Fact]
    public void AMute_TakesItsNewState()
    {
        var mute = new Mute(new GuildId(2), new UserId(3), new SinId(4), null, _moderator, _at,
            _at.AddHours(1), Active);

        mute.InForce.ShouldBeTrue();
        var removed = mute.Remove(_moderator, null, _at.AddMinutes(5)).ShouldSucceed();
        removed.State.ShouldBe(new ItemState.Removed(_at.AddMinutes(5), _moderator, null));
        removed.InForce.ShouldBeFalse();
        removed.Expire(_at).ShouldFail().ShouldBeOfType<Invalid>();
    }

    [Fact]
    public void ALock_TakesItsNewState()
    {
        var channelLock = new ChannelLock(new GuildId(2), new ChannelId(3), null, _moderator, _at,
            _at.AddHours(1), new PreviouslyAllowedPermissions(1), new PreviouslyDeniedPermissions(2), Active);
        var threadLock = new ThreadLock(new GuildId(2), new ChannelId(4), null, _moderator, _at,
            _at.AddHours(1), Pending);

        channelLock.Expire(_at).ShouldSucceed().State.ShouldBe(new ItemState.Expired(_at));
        threadLock.Activate(_at).ShouldSucceed().State.ShouldBe(new ItemState.Active(_at));
    }

    private static ItemState Pending => new ItemState.Pending();
    private static ItemState Active => new ItemState.Active(_at.AddMinutes(-10));
    private static ItemState Expired => new ItemState.Expired(_at.AddMinutes(-5));
    private static ItemState Removed => new ItemState.Removed(_at.AddMinutes(-5), _moderator, null);

    private static ItemState StateNamed(string name)
        => name switch
        {
            nameof(ItemState.Pending) => Pending,
            nameof(ItemState.Active) => Active,
            nameof(ItemState.Expired) => Expired,
            _ => Removed
        };
}
