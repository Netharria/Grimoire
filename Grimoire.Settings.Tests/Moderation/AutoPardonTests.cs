// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Tests.Moderation;

[Collection("Settings collection")]
public sealed class AutoPardonTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private static readonly GuildId _guildId = new(1UL);
    private static readonly ModeratorId _modId = new(999UL);
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await factory.ResetDatabase();

    [Fact]
    public async Task NoRow_ReturnsDefault()
    {
        var result = await this._sut.GetAutoPardonDuration(_guildId, TestContext.Current.CancellationToken);

        result.ShouldSucceed().ShouldBe(TimeSpan.FromDays(10950));
    }

    [Fact]
    public async Task CustomDuration_RoundTrips()
    {
        await this._sut.SetAutoPardonDuration(_guildId, _modId, TimeSpan.FromDays(30),
            TestContext.Current.CancellationToken);

        var result = await this._sut.GetAutoPardonDuration(_guildId, TestContext.Current.CancellationToken);

        result.ShouldSucceed().ShouldBe(TimeSpan.FromDays(30));
    }

    [Fact]
    public async Task ZeroDurationInDb_ReturnsDefault()
    {
        // Store a zero duration directly to simulate a corrupt/zero stored value.
        await using var db = factory.CreateDbContext();
        db.GuildSettings.Add(
            GuildSettingCustomValue.Create(
                GuildSettingType.SinAutoPardonDuration, _guildId, _modId, DateTimeOffset.UtcNow,
                TimeSpan.Zero.ToString("c")).ShouldSucceed());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetAutoPardonDuration(_guildId, TestContext.Current.CancellationToken);

        result.ShouldSucceed().ShouldBe(TimeSpan.FromDays(10950));
    }

    [Fact]
    public async Task ResetAutoPardon_WritesDefaultAsCustomValue()
    {
        var result = await this._sut.ResetAutoPardonDuration(_guildId, _modId, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Result<TimeSpan>.Success>();

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var duration = await freshSut.GetAutoPardonDuration(_guildId, TestContext.Current.CancellationToken);
        duration.ShouldSucceed().ShouldBe(TimeSpan.FromDays(10950));
    }

    [Fact]
    public async Task TwoGuilds_AutoPardon_Independent()
    {
        var guildB = new GuildId(2UL);

        await this._sut.SetAutoPardonDuration(_guildId, _modId, TimeSpan.FromDays(30),
            TestContext.Current.CancellationToken);
        await this._sut.SetAutoPardonDuration(guildB, _modId, TimeSpan.FromDays(60),
            TestContext.Current.CancellationToken);

        (await this._sut.GetAutoPardonDuration(_guildId, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBe(TimeSpan.FromDays(30));
        (await this._sut.GetAutoPardonDuration(guildB, TestContext.Current.CancellationToken)).ShouldSucceed()
            .ShouldBe(TimeSpan.FromDays(60));
    }
}
