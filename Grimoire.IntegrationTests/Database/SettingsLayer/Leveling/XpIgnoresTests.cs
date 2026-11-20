// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using Grimoire.Settings.Domain.Documents;
using LanguageExt;

namespace Grimoire.IntegrationTests.Database.SettingsLayer.Leveling;

/// <summary>
///     The XP exclusions through the settings module, stored as one versioned document per guild (spec 2.5). The
///     matching itself is unit-tested in <c>CollectionChangeTests</c>; these pin the storage, the cache and the race.
/// </summary>
[Collection("Settings collection")]
public sealed class XpIgnoresTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private static readonly UserId _userId = new(100UL);
    private static readonly ChannelId _channelId = new(200UL);
    private static readonly RoleId _roleId = new(300UL);
    private static readonly IReadOnlySet<RoleId> _noRoles = FrozenSet<RoleId>.Empty;
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public async ValueTask InitializeAsync()
        => await this._sut.SetModuleState(Module.Leveling, _guildId, _modId, true);

    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    private Task<Fin<Write<IReadOnlySet<XpTarget>>>> Ignore(params XpTarget[] targets)
        => this._sut.IgnoreXpAsync(_guildId, _modId, targets.ToFrozenSet(), TestContext.Current.CancellationToken);

    private Task<Fin<Write<IReadOnlySet<XpTarget>>>> Unignore(params XpTarget[] targets)
        => this._sut.UnignoreXpAsync(_guildId, _modId, targets.ToFrozenSet(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task UserIgnored_IsMessageIgnored_ReturnsTrue()
    {
        await Ignore(new XpTarget.Member(_userId));

        (await this._sut.IsMessageIgnored(_guildId, _userId, _noRoles, _channelId,
            cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeTrue();
    }

    [Fact]
    public async Task ChannelIgnored_IsMessageIgnored_ReturnsTrue()
    {
        await Ignore(new XpTarget.Channel(_channelId));

        (await this._sut.IsMessageIgnored(_guildId, _userId, _noRoles, _channelId,
            cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeTrue();
    }

    [Fact]
    public async Task ChannelIgnored_CoversItsThreads()
    {
        await Ignore(new XpTarget.Channel(_channelId));

        (await this._sut.IsMessageIgnored(_guildId, _userId, _noRoles, new ChannelId(201UL), _channelId,
            TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeTrue();
    }

    [Fact]
    public async Task RoleIgnored_IsMessageIgnored_ReturnsTrue()
    {
        await Ignore(new XpTarget.Role(_roleId));

        (await this._sut.IsMessageIgnored(_guildId, _userId, new System.Collections.Generic.HashSet<RoleId> { _roleId },
            _channelId, cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeTrue();
    }

    [Fact]
    public async Task NoneIgnored_IsMessageIgnored_ReturnsFalse()
        => (await this._sut.IsMessageIgnored(_guildId, _userId, _noRoles, _channelId,
            cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeFalse();

    [Fact]
    public async Task ChannelIgnored_IsMemberIgnored_ReturnsFalse()
    {
        await Ignore(new XpTarget.Channel(_channelId));

        (await this._sut.IsMemberIgnored(_guildId, _userId, _noRoles, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeFalse();
    }

    [Fact]
    public async Task UserIgnored_IsMemberIgnored_ReturnsTrue()
    {
        await Ignore(new XpTarget.Member(_userId));

        (await this._sut.IsMemberIgnored(_guildId, _userId, _noRoles, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeTrue();
        (await this._sut.IsMemberIgnored(_guildId, new UserId(101UL), _noRoles, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeFalse();
    }

    [Fact]
    public async Task IgnoringNothing_IsUnchanged()
        => (await Ignore()).ShouldBeUnchanged().ShouldBe("No items were provided to ignore.");

    [Fact]
    public async Task IgnoringWhatIsAlreadyIgnored_IsUnchanged_AndStoresNothing()
    {
        await Ignore(new XpTarget.Member(_userId));

        (await Ignore(new XpTarget.Member(_userId))).ShouldBeUnchanged()
            .ShouldBe("Nothing changed: every item was already in that state.");
        (await Unignore(new XpTarget.Role(_roleId))).ShouldBeUnchanged();

        await using var db = factory.CreateDbContext();
        (await SettingsRows.LatestDocument(db, _guildId, SettingsDocument.XpExclusions,
            TestContext.Current.CancellationToken))!.Version.ShouldBe(1);
    }

    /// <summary>X-12: only what changed is returned, so the reply and the mod log name only that.</summary>
    [Fact]
    public async Task AMixedWrite_ReturnsOnlyTheChanges()
    {
        await Ignore(new XpTarget.Member(_userId));

        (await Ignore(new XpTarget.Member(_userId), new XpTarget.Channel(_channelId))).ShouldBeSaved()
            .ShouldBe([new XpTarget.Channel(_channelId)]);
        (await Unignore(new XpTarget.Member(_userId), new XpTarget.Role(_roleId))).ShouldBeSaved()
            .ShouldBe([new XpTarget.Member(_userId)]);
    }

    [Fact]
    public async Task Unignoring_RemovesTheTarget()
    {
        await Ignore(new XpTarget.Channel(_channelId));
        await Unignore(new XpTarget.Channel(_channelId));

        var fresh = SettingsModuleFactory.Create(factory.ConnectionString);
        (await fresh.GetAllIgnoredItems(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task ReIgnoring_AppearsAgain()
    {
        await Ignore(new XpTarget.Channel(_channelId));
        await Unignore(new XpTarget.Channel(_channelId));
        await Ignore(new XpTarget.Channel(_channelId));

        var fresh = SettingsModuleFactory.Create(factory.ConnectionString);
        (await fresh.GetAllIgnoredItems(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBe([new XpTarget.Channel(_channelId)]);
    }

    /// <summary>Spec 2.5: history is the document's versions, each with who and when.</summary>
    [Fact]
    public async Task EachChange_IsAVersion_WithWhoMadeIt()
    {
        await Ignore(new XpTarget.Channel(_channelId));
        await this._sut.UnignoreXpAsync(_guildId, new ModeratorId(5UL),
            new[] { new XpTarget.Channel(_channelId) }.ToFrozenSet<XpTarget>(), TestContext.Current.CancellationToken);

        await using var db = factory.CreateDbContext();
        var versions = await db.GuildSettings.AsNoTracking()
            .Where(row => row.GuildId == _guildId && row.Document == SettingsDocument.XpExclusions)
            .OrderBy(row => row.Version)
            .ToListAsync(TestContext.Current.CancellationToken);
        versions.Select(row => (row.Version, row.SetBy, row.Value)).ShouldBe([
            (1, _modId, """{"Channels": ["200"]}"""),
            (2, new ModeratorId(5UL), "{}")
        ]);
    }

    [Fact]
    public async Task AnotherGuild_IsNotAffected()
    {
        var guildB = new GuildId(2UL);
        await Ignore(new XpTarget.Member(_userId), new XpTarget.Channel(_channelId));

        var fresh = SettingsModuleFactory.Create(factory.ConnectionString);
        (await fresh.IsMemberIgnored(guildB, _userId, _noRoles, TestContext.Current.CancellationToken))
            .ShouldSucceed().ShouldBeFalse();
        (await fresh.IsMessageIgnored(guildB, new UserId(101UL), _noRoles, _channelId,
            cancellationToken: TestContext.Current.CancellationToken)).ShouldSucceed().ShouldBeFalse();
    }

    [Fact]
    public async Task TheCache_IsClearedByAWrite()
    {
        (await this._sut.GetAllIgnoredItems(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBeEmpty();

        await Ignore(new XpTarget.Channel(_channelId));

        (await this._sut.GetAllIgnoredItems(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldHaveSingleItem();
    }

    /// <summary>
    ///     Spec 2.5: two moderators ignoring different members at once both land, and each write returns only its own
    ///     member, since what changed is decided against the version it replaced.
    /// </summary>
    [Fact]
    public async Task ConcurrentIgnores_BothLand_AndEachReportsOnlyItsOwn()
    {
        var members = Enumerable.Range(0, 2).Select(index => new XpTarget.Member(new UserId(500UL + (ulong)index)))
            .ToArray();

        var results = await Task.WhenAll(members.Select(member =>
            SettingsModuleFactory.Create(factory.ConnectionString).IgnoreXpAsync(_guildId, _modId,
                new XpTarget[] { member }.ToFrozenSet(), TestContext.Current.CancellationToken)));

        results.Select(result => result.ShouldBeSaved().ShouldHaveSingleItem()).ShouldBe(members, ignoreOrder: true);
        var fresh = SettingsModuleFactory.Create(factory.ConnectionString);
        (await fresh.GetAllIgnoredItems(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBe(members, ignoreOrder: true);
    }
}
