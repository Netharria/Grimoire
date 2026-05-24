// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Tests.Leveling;

[Collection("Settings collection")]
public sealed class XpIgnoresTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly UserId _userId = new(100UL);
    private static readonly ChannelId _channelId = new(200UL);
    private static readonly RoleId _roleId = new(300UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public async ValueTask InitializeAsync()
        => await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true);

    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    [Fact]
    public async Task UserIgnored_IsMessageIgnored_ReturnsTrue()
    {
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new IgnoredMember
                {
                    UserId = _userId, GuildId = _guildId, SetBy = _modId, SetAt = DateTimeOffset.UtcNow
                }
            }, TestContext.Current.CancellationToken);

        var result = await this._sut.IsMessageIgnored(_guildId, _userId, new HashSet<RoleId>(), _channelId,
            TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ChannelIgnored_IsMessageIgnored_ReturnsTrue()
    {
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new IgnoredChannel
                {
                    ChannelId = _channelId, GuildId = _guildId, SetBy = _modId, SetAt = DateTimeOffset.UtcNow
                }
            }, TestContext.Current.CancellationToken);

        var result = await this._sut.IsMessageIgnored(_guildId, _userId, new HashSet<RoleId>(), _channelId,
            TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task RoleIgnored_IsMessageIgnored_ReturnsTrue()
    {
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new IgnoredRole
                {
                    RoleId = _roleId, GuildId = _guildId, SetBy = _modId, SetAt = DateTimeOffset.UtcNow
                }
            }, TestContext.Current.CancellationToken);

        var result = await this._sut.IsMessageIgnored(_guildId, _userId, new HashSet<RoleId> { _roleId }, _channelId,
            TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task NoneIgnored_IsMessageIgnored_ReturnsFalse()
    {
        var result = await this._sut.IsMessageIgnored(_guildId, _userId, new HashSet<RoleId>(), _channelId,
            TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ChannelIgnored_IsMemberIgnored_ReturnsFalse()
    {
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new IgnoredChannel
                {
                    ChannelId = _channelId, GuildId = _guildId, SetBy = _modId, SetAt = DateTimeOffset.UtcNow
                }
            }, TestContext.Current.CancellationToken);

        var result = await this._sut
            .IsMemberIgnored(_guildId, _userId, new HashSet<RoleId>(), TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task AppendEmpty_ReturnsNotModified()
    {
        var result = await this._sut.AppendIgnoredItemsEvent(_guildId, new HashSet<XpTrackedItem>(),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<IReadOnlySet<XpTrackedItem>>.NotModified>();
    }

    [Fact]
    public async Task AppendMismatchedGuild_ReturnsFail()
    {
        var wrongGuild = new GuildId(2UL);
        var item = new IgnoredChannel
        {
            ChannelId = _channelId, GuildId = wrongGuild, SetBy = _modId, SetAt = DateTimeOffset.UtcNow
        };

        var result = await this._sut.AppendIgnoredItemsEvent(_guildId, new HashSet<XpTrackedItem> { item },
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<IReadOnlySet<XpTrackedItem>>.Invalid>();
    }

    [Fact]
    public async Task DisabledItem_NotInActiveSet()
    {
        var past = DateTimeOffset.UtcNow.AddHours(-1);
        var now = DateTimeOffset.UtcNow;

        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new IgnoredChannel { ChannelId = _channelId, GuildId = _guildId, SetBy = _modId, SetAt = past }
            }, TestContext.Current.CancellationToken);
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new WatchedChannel { ChannelId = _channelId, GuildId = _guildId, SetBy = _modId, SetAt = now }
            }, TestContext.Current.CancellationToken);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var items = await freshSut.GetAllIgnoredItems(_guildId, TestContext.Current.CancellationToken).ShouldSucceed();

        items.OfType<IgnoredChannel>().ShouldNotContain(c => c.ChannelId == _channelId);
    }

    [Fact]
    public async Task ReIgnore_AppearsAgain()
    {
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);
        var t3 = DateTimeOffset.UtcNow;

        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new IgnoredChannel { ChannelId = _channelId, GuildId = _guildId, SetBy = _modId, SetAt = t1 }
            }, TestContext.Current.CancellationToken);
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new WatchedChannel { ChannelId = _channelId, GuildId = _guildId, SetBy = _modId, SetAt = t2 }
            }, TestContext.Current.CancellationToken);
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new IgnoredChannel { ChannelId = _channelId, GuildId = _guildId, SetBy = _modId, SetAt = t3 }
            }, TestContext.Current.CancellationToken);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var items = await freshSut.GetAllIgnoredItems(_guildId, TestContext.Current.CancellationToken).ShouldSucceed();

        items.OfType<IgnoredChannel>().ShouldContain(c => c.ChannelId == _channelId);
    }

    [Fact]
    public async Task AppendEmpty_ErrorCode_IsCorrect()
    {
        var result = await this._sut.AppendIgnoredItemsEvent(_guildId, new HashSet<XpTrackedItem>(),
            TestContext.Current.CancellationToken);

        var notModified = result.ShouldBeOfType<Result<IReadOnlySet<XpTrackedItem>>.NotModified>();
        notModified.Error.Code.ShouldBe("xp-ignored-items.empty");
        notModified.Error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AppendMismatchedGuild_ErrorCode_IsCorrect()
    {
        var wrongGuild = new GuildId(2UL);
        var item = new IgnoredChannel
        {
            ChannelId = _channelId, GuildId = wrongGuild, SetBy = _modId, SetAt = DateTimeOffset.UtcNow
        };

        var result = await this._sut.AppendIgnoredItemsEvent(_guildId, new HashSet<XpTrackedItem> { item },
            TestContext.Current.CancellationToken);

        var invalid = result.ShouldBeOfType<Result<IReadOnlySet<XpTrackedItem>>.Invalid>();
        var error = invalid.Error;
        error.Code.ShouldBe("xp-ignored-items.guild-id.mismatch");
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task IsMemberIgnored_SameUserDifferentGuild_ReturnsFalse()
    {
        var guildB = new GuildId(2UL);
        await this._sut.SetModuleState(Module.Leveling, guildB, _modId, true, TestContext.Current.CancellationToken);

        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new IgnoredMember
                {
                    UserId = _userId, GuildId = _guildId, SetBy = _modId, SetAt = DateTimeOffset.UtcNow
                }
            }, TestContext.Current.CancellationToken);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut
            .IsMemberIgnored(guildB, _userId, new HashSet<RoleId>(), TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsMessageIgnored_SameUserDifferentGuild_ReturnsFalse()
    {
        var guildB = new GuildId(2UL);
        await this._sut.SetModuleState(Module.Leveling, guildB, _modId, true, TestContext.Current.CancellationToken);

        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new IgnoredMember
                {
                    UserId = _userId, GuildId = _guildId, SetBy = _modId, SetAt = DateTimeOffset.UtcNow
                }
            }, TestContext.Current.CancellationToken);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.IsMessageIgnored(guildB, _userId, new HashSet<RoleId>(), _channelId,
            TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsMessageIgnored_SameChannelDifferentGuild_ReturnsFalse()
    {
        var guildB = new GuildId(2UL);
        await this._sut.SetModuleState(Module.Leveling, guildB, _modId, true, TestContext.Current.CancellationToken);

        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new IgnoredChannel
                {
                    ChannelId = _channelId, GuildId = _guildId, SetBy = _modId, SetAt = DateTimeOffset.UtcNow
                }
            }, TestContext.Current.CancellationToken);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.IsMessageIgnored(guildB, _userId, new HashSet<RoleId>(), _channelId,
            TestContext.Current.CancellationToken).ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task UserIgnored_IsMemberIgnored_ReturnsTrue()
    {
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new IgnoredMember
                {
                    UserId = _userId, GuildId = _guildId, SetBy = _modId, SetAt = DateTimeOffset.UtcNow
                }
            }, TestContext.Current.CancellationToken);

        var result = await this._sut
            .IsMemberIgnored(_guildId, _userId, new HashSet<RoleId>(), TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task DifferentUserInSameGuild_IsMemberIgnored_ReturnsFalse()
    {
        var otherUser = new UserId(101UL);
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new IgnoredMember
                {
                    UserId = _userId, GuildId = _guildId, SetBy = _modId, SetAt = DateTimeOffset.UtcNow
                }
            }, TestContext.Current.CancellationToken);

        var result = await this._sut
            .IsMemberIgnored(_guildId, otherUser, new HashSet<RoleId>(), TestContext.Current.CancellationToken)
            .ShouldSucceed();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task AppendIgnoredItems_MixedGuildItems_ReturnsInvalid()
    {
        var wrongGuild = new GuildId(2UL);
        var items = new HashSet<XpTrackedItem>
        {
            new IgnoredChannel
            {
                ChannelId = _channelId, GuildId = _guildId, SetBy = _modId, SetAt = DateTimeOffset.UtcNow
            },
            new IgnoredMember
            {
                UserId = _userId, GuildId = wrongGuild, SetBy = _modId, SetAt = DateTimeOffset.UtcNow
            }
        };

        var result = await this._sut.AppendIgnoredItemsEvent(_guildId, items, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<IReadOnlySet<XpTrackedItem>>.Invalid>();
    }

    [Fact]
    public async Task CacheInvalidated_AfterAppendIgnoredItems()
    {
        (await this._sut.GetAllIgnoredItems(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeEmpty();

        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpTrackedItem>
            {
                new IgnoredChannel
                {
                    ChannelId = _channelId, GuildId = _guildId, SetBy = _modId, SetAt = DateTimeOffset.UtcNow
                }
            }, TestContext.Current.CancellationToken);

        (await this._sut.GetAllIgnoredItems(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldHaveSingleItem();
    }
}
