// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Documents;
using LanguageExt;

namespace Grimoire.IntegrationTests.Database.SettingsLayer.Moderation;

/// <remarks>Cases: XC-103 (the ignore list's cache).</remarks>
[Collection("Settings collection")]
public sealed class DiagnosticsIgnoresTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    private static DiagnosticsTarget Role(ulong id) => new DiagnosticsTarget.Role(new RoleId(id));

    private static DiagnosticsTarget Member(ulong id) => new DiagnosticsTarget.Member(new UserId(id));

    private Task<Fin<Write<IReadOnlyList<DiagnosticsTarget>>>> Add(params DiagnosticsTarget[] items)
        => this._sut.AddDiagnosticsIgnoredItemsAsync(_guildId, _modId, items, TestContext.Current.CancellationToken);

    private async Task<DiagnosticsTarget[]> Get(SettingsModule? sut = null)
        => (await (sut ?? this._sut)
            .GetDiagnosticsIgnoredItemsAsync(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed()).ToArray();

    [Fact]
    public async Task NoRows_ReturnsEmpty()
        => (await this.Get()).ShouldBeEmpty();

    [Fact]
    public async Task Added_roles_and_members_are_returned_with_their_types()
    {
        await this.Add(Role(10), Member(20)).ShouldBeSaved();

        var items = await this.Get();

        items.OfType<DiagnosticsTarget.Role>().ShouldHaveSingleItem().Id.ShouldBe(new RoleId(10));
        items.OfType<DiagnosticsTarget.Member>().ShouldHaveSingleItem().Id.ShouldBe(new UserId(20));
    }

    [Fact]
    public async Task Added_items_survive_a_fresh_module_with_an_empty_cache()
    {
        await this.Add(Role(10)).ShouldBeSaved();

        (await this.Get(SettingsModuleFactory.Create(factory.ConnectionString))).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Adding_an_already_ignored_item_is_not_modified_and_adds_no_version()
    {
        await this.Add(Role(10)).ShouldBeSaved();

        (await this.Add(Role(10))).ShouldBeUnchanged();
        (await this.Get()).Length.ShouldBe(1);
        await using var db = factory.CreateDbContext();
        (await SettingsRows.LatestDocument(db, _guildId, SettingsDocument.DiagnosticsIgnores,
            TestContext.Current.CancellationToken))!.Version.ShouldBe(1);
    }

    [Fact]
    public async Task Adding_a_mix_only_reports_the_new_items()
    {
        await this.Add(Role(10)).ShouldBeSaved();

        var added = await this.Add(Role(10), Role(11)).ShouldBeSaved();

        added.ShouldHaveSingleItem().ShouldBe(Role(11));
    }

    [Fact]
    public async Task Adding_nothing_is_not_modified()
        => (await this.Add()).ShouldBeUnchanged();

    [Fact]
    public async Task Removing_updates_the_list()
    {
        await this.Add(Role(10), Member(20)).ShouldBeSaved();

        var removed = await this._sut
            .RemoveDiagnosticsIgnoredItemsAsync(_guildId, _modId, [Role(10)], TestContext.Current.CancellationToken)
            .ShouldSucceed();

        removed.ShouldHaveSingleItem();
        (await this.Get()).ShouldHaveSingleItem().ShouldBe(Member(20));
    }

    [Fact]
    public async Task Removing_something_that_is_not_ignored_is_not_found()
        => (await this._sut.RemoveDiagnosticsIgnoredItemsAsync(_guildId, _modId, [Role(10)],
                TestContext.Current.CancellationToken))
            .ShouldFail().ShouldBeOfType<NotFound>();

    /// <summary>A role Discord deleted is removed by the bot, in a version with the bot as its author (spec 2.5).</summary>
    [Fact]
    public async Task ADeletedRole_IsRemovedByTheBot()
    {
        var bot = new ModeratorId(42UL);
        await this.Add(Role(10)).ShouldBeSaved();

        (await this._sut.RemoveDiagnosticsIgnoredRoleAsync(_guildId, bot, new RoleId(10),
            TestContext.Current.CancellationToken)).ShouldSucceed();
        (await this._sut.RemoveDiagnosticsIgnoredRoleAsync(_guildId, bot, new RoleId(10),
            TestContext.Current.CancellationToken)).ShouldFail().ShouldBeOfType<NotFound>()
            .Message.ShouldBe("That role is not ignored.");

        (await this.Get()).ShouldBeEmpty();
        await using var db = factory.CreateDbContext();
        (await SettingsRows.LatestDocument(db, _guildId, SettingsDocument.DiagnosticsIgnores,
            TestContext.Current.CancellationToken))!.SetBy.ShouldBe(bot);
    }
}
