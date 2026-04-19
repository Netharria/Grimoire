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

    public async Task InitializeAsync()
        => await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true);

    public Task DisposeAsync() => factory.ResetDatabase();

    [Fact]
    public async Task ModuleDisabled_IsMessageIgnored_ReturnsTrue()
    {
        await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, false);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.IsMessageIgnored(_guildId, _userId, new HashSet<RoleId>(), _channelId);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task UserIgnored_IsMessageIgnored_ReturnsTrue()
    {
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpIgnoredItem>
            {
                new IgnoredMember
                {
                    UserId = _userId,
                    GuildId = _guildId,
                    SetBy = _modId,
                    SetAt = DateTimeOffset.UtcNow,
                    Enabled = true
                }
            });

        var result = await this._sut.IsMessageIgnored(_guildId, _userId, new HashSet<RoleId>(), _channelId);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ChannelIgnored_IsMessageIgnored_ReturnsTrue()
    {
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpIgnoredItem>
            {
                new IgnoredChannel
                {
                    ChannelId = _channelId,
                    GuildId = _guildId,
                    SetBy = _modId,
                    SetAt = DateTimeOffset.UtcNow,
                    Enabled = true
                }
            });

        var result = await this._sut.IsMessageIgnored(_guildId, _userId, new HashSet<RoleId>(), _channelId);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task RoleIgnored_IsMessageIgnored_ReturnsTrue()
    {
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpIgnoredItem>
            {
                new IgnoredRole
                {
                    RoleId = _roleId,
                    GuildId = _guildId,
                    SetBy = _modId,
                    SetAt = DateTimeOffset.UtcNow,
                    Enabled = true
                }
            });

        var result = await this._sut.IsMessageIgnored(_guildId, _userId, new HashSet<RoleId> { _roleId }, _channelId);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task NoneIgnored_IsMessageIgnored_ReturnsFalse()
    {
        var result = await this._sut.IsMessageIgnored(_guildId, _userId, new HashSet<RoleId>(), _channelId);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ChannelIgnored_IsMemberIgnored_ReturnsFalse()
    {
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpIgnoredItem>
            {
                new IgnoredChannel
                {
                    ChannelId = _channelId,
                    GuildId = _guildId,
                    SetBy = _modId,
                    SetAt = DateTimeOffset.UtcNow,
                    Enabled = true
                }
            });

        var result = await this._sut.IsMemberIgnored(_guildId, _userId, new HashSet<RoleId>());

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task AppendEmpty_ReturnsNotModified()
    {
        var result = await this._sut.AppendIgnoredItemsEvent(_guildId, new HashSet<XpIgnoredItem>());

        result.ShouldBeOfType<Result<IReadOnlySet<XpIgnoredItem>>.NotModified>();
    }

    [Fact]
    public async Task AppendMismatchedGuild_ReturnsFail()
    {
        var wrongGuild = new GuildId(2UL);
        var item = new IgnoredChannel
        {
            ChannelId = _channelId,
            GuildId = wrongGuild,
            SetBy = _modId,
            SetAt = DateTimeOffset.UtcNow,
            Enabled = true
        };

        var result = await this._sut.AppendIgnoredItemsEvent(_guildId, new HashSet<XpIgnoredItem> { item });

        result.ShouldBeOfType<Result<IReadOnlySet<XpIgnoredItem>>.Invalid>();
    }

    [Fact]
    public async Task DisabledItem_NotInActiveSet()
    {
        var past = DateTimeOffset.UtcNow.AddHours(-1);
        var now = DateTimeOffset.UtcNow;

        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpIgnoredItem>
            {
                new IgnoredChannel
                {
                    ChannelId = _channelId,
                    GuildId = _guildId,
                    SetBy = _modId,
                    SetAt = past,
                    Enabled = true
                }
            });
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpIgnoredItem>
            {
                new IgnoredChannel
                {
                    ChannelId = _channelId,
                    GuildId = _guildId,
                    SetBy = _modId,
                    SetAt = now,
                    Enabled = false
                }
            });

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var items = await freshSut.GetAllIgnoredItems(_guildId);

        items.OfType<IgnoredChannel>().ShouldNotContain(c => c.ChannelId == _channelId);
    }

    [Fact]
    public async Task ReIgnore_AppearsAgain()
    {
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);
        var t3 = DateTimeOffset.UtcNow;

        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpIgnoredItem>
            {
                new IgnoredChannel { ChannelId = _channelId, GuildId = _guildId, SetBy = _modId, SetAt = t1, Enabled = true }
            });
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpIgnoredItem>
            {
                new IgnoredChannel { ChannelId = _channelId, GuildId = _guildId, SetBy = _modId, SetAt = t2, Enabled = false }
            });
        await this._sut.AppendIgnoredItemsEvent(_guildId,
            new HashSet<XpIgnoredItem>
            {
                new IgnoredChannel { ChannelId = _channelId, GuildId = _guildId, SetBy = _modId, SetAt = t3, Enabled = true }
            });

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var items = await freshSut.GetAllIgnoredItems(_guildId);

        items.OfType<IgnoredChannel>().ShouldContain(c => c.ChannelId == _channelId);
    }
}
