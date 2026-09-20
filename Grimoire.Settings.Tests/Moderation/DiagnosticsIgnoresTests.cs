// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Tests.Moderation;

[Collection("Settings collection")]
public sealed class DiagnosticsIgnoresTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    private static DiagnosticsIgnoredItem Role(ulong id, GuildId? guild = null)
        => DiagnosticsIgnoredRole.Create(new RoleId(id), guild ?? _guildId, _modId, DateTimeOffset.UtcNow)
            .ShouldSucceed();

    private static DiagnosticsIgnoredItem Member(ulong id)
        => DiagnosticsIgnoredMember.Create(new UserId(id), _guildId, _modId, DateTimeOffset.UtcNow).ShouldSucceed();

    private Task<Result<IReadOnlyList<DiagnosticsIgnoredItem>>> Add(params DiagnosticsIgnoredItem[] items)
        => this._sut.AddDiagnosticsIgnoredItemsAsync(_guildId, items, TestContext.Current.CancellationToken);

    private async Task<DiagnosticsIgnoredItem[]> Get(SettingsModule? sut = null)
        => (await (sut ?? this._sut)
            .GetDiagnosticsIgnoredItemsAsync(_guildId, TestContext.Current.CancellationToken)
            .ShouldSucceed()).ToArray();

    [Fact]
    public async Task NoRows_ReturnsEmpty()
        => (await this.Get()).ShouldBeEmpty();

    [Fact]
    public async Task Added_roles_and_members_are_returned_with_their_types()
    {
        await this.Add(Role(10), Member(20)).ShouldSucceed();

        var items = await this.Get();

        items.OfType<DiagnosticsIgnoredRole>().ShouldHaveSingleItem().RoleId.ShouldBe(new RoleId(10));
        items.OfType<DiagnosticsIgnoredMember>().ShouldHaveSingleItem().UserId.ShouldBe(new UserId(20));
    }

    [Fact]
    public async Task Added_items_survive_a_fresh_module_with_an_empty_cache()
    {
        await this.Add(Role(10)).ShouldSucceed();

        (await this.Get(SettingsModuleFactory.Create(factory.ConnectionString))).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Adding_an_already_ignored_item_is_not_modified_and_adds_no_row()
    {
        await this.Add(Role(10)).ShouldSucceed();

        (await this.Add(Role(10))).ShouldBeOfType<Result<IReadOnlyList<DiagnosticsIgnoredItem>>.NotModified>();
        (await this.Get()).Length.ShouldBe(1);
    }

    [Fact]
    public async Task Adding_a_mix_only_reports_the_new_items()
    {
        await this.Add(Role(10)).ShouldSucceed();

        var added = await this.Add(Role(10), Role(11)).ShouldSucceed();

        added.ShouldHaveSingleItem().ShouldBeOfType<DiagnosticsIgnoredRole>().RoleId.ShouldBe(new RoleId(11));
    }

    [Fact]
    public async Task Adding_nothing_is_not_modified()
        => (await this.Add()).ShouldBeOfType<Result<IReadOnlyList<DiagnosticsIgnoredItem>>.NotModified>();

    [Fact]
    public async Task Adding_an_item_for_another_guild_fails()
        => (await this.Add(Role(10, new GuildId(2)))).ShouldBeOfType<Result<IReadOnlyList<DiagnosticsIgnoredItem>>.Invalid>();

    [Fact]
    public async Task Removing_deletes_the_row_and_updates_the_list()
    {
        await this.Add(Role(10), Member(20)).ShouldSucceed();

        var removed = await this._sut
            .RemoveDiagnosticsIgnoredItemsAsync(_guildId, [Role(10)], TestContext.Current.CancellationToken)
            .ShouldSucceed();

        removed.ShouldHaveSingleItem();
        (await this.Get()).ShouldHaveSingleItem().ShouldBeOfType<DiagnosticsIgnoredMember>();
    }

    [Fact]
    public async Task Removing_something_that_is_not_ignored_is_not_found()
        => (await this._sut.RemoveDiagnosticsIgnoredItemsAsync(_guildId, [Role(10)],
                TestContext.Current.CancellationToken))
            .ShouldBeOfType<Result<IReadOnlyList<DiagnosticsIgnoredItem>>.NotFound>();

    [Fact]
    public void Invalid_ids_are_rejected_by_the_domain_types()
    {
        DiagnosticsIgnoredRole.Create(new RoleId(0), _guildId, _modId, DateTimeOffset.UtcNow)
            .ShouldBeOfType<Validation<DiagnosticsIgnoredRole>.Invalid>();
        DiagnosticsIgnoredMember.Create(new UserId(1), new GuildId(0), _modId, DateTimeOffset.UtcNow)
            .ShouldBeOfType<Validation<DiagnosticsIgnoredMember>.Invalid>();
    }
}
