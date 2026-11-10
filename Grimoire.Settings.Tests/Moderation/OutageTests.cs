// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace Grimoire.Settings.Tests.Moderation;

/// <summary>
///     S-4: the methods that return a <see cref="Result{T}" /> report an outage as a failure instead of throwing, so
///     callers' failure paths run. Each call goes through a module whose database is down.
/// </summary>
[Collection("Settings collection")]
public sealed class OutageTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly UserId _userId = new(100UL);
    private static readonly ChannelId _channelId = new(200UL);
    private readonly SettingsModule _down = SettingsModuleFactory.Create(factory.ConnectionString, () => true);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddMute_Fails()
        => (await this._down.AddMute(_userId, _guildId, _modId, new SinId(1L), DateTimeOffset.UtcNow.AddHours(1),
            new RoleId(300UL), Token)).ShouldBeOfType<Result<MuteAdded>.Invalid>();

    [Fact]
    public async Task RemoveMute_Fails()
        => (await this._down.RemoveMute(_userId, _guildId, _modId, Token)).ShouldBeOfType<Result<MuteAdded>.Invalid>();

    [Fact]
    public async Task GetActiveMute_Fails()
        => (await this._down.GetActiveMute(_userId, _guildId, Token)).ShouldBeOfType<Result<MuteAdded>.Invalid>();

    [Fact]
    public async Task GetAllIgnoredItems_Fails()
        => (await this._down.GetAllIgnoredItems(_guildId, Token))
            .ShouldBeOfType<Result<FrozenSet<XpIgnoredItem>>.Invalid>();

    /// <summary>A failure, not "not ignored": the caller decides what an unknown answer means.</summary>
    [Fact]
    public async Task IsMessageIgnored_Fails()
        => (await this._down.IsMessageIgnored(_guildId, _userId, new HashSet<RoleId>(), _channelId, cancellationToken: Token))
            .ShouldBeOfType<Result<bool>.Invalid>();

    [Fact]
    public async Task IsMemberIgnored_Fails()
        => (await this._down.IsMemberIgnored(_guildId, _userId, new HashSet<RoleId>(), Token))
            .ShouldBeOfType<Result<bool>.Invalid>();

    [Fact]
    public async Task AppendIgnoredItemsEvent_Fails()
        => (await this._down.AppendIgnoredItemsEvent(_guildId,
                new HashSet<XpTrackedItem>
                {
                    new IgnoredMember { UserId = _userId, GuildId = _guildId, SetBy = _modId, SetAt = DateTimeOffset.UtcNow }
                }, Token))
            .ShouldBeOfType<Result<IReadOnlySet<XpTrackedItem>>.Invalid>();
}
