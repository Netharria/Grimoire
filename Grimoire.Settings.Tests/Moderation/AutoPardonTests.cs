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

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetDatabase();

    [Fact]
    public async Task NoRow_ReturnsDefault()
    {
        var result = await this._sut.GetAutoPardonDuration(_guildId);

        result.ShouldBe(TimeSpan.FromDays(10950));
    }

    [Fact]
    public async Task CustomDuration_RoundTrips()
    {
        await this._sut.SetAutoPardonDuration(_guildId, _modId, TimeSpan.FromDays(30));

        var result = await this._sut.GetAutoPardonDuration(_guildId);

        result.ShouldBe(TimeSpan.FromDays(30));
    }

    [Fact]
    public async Task ZeroDurationInDb_ReturnsDefault()
    {
        // Store a zero duration directly to simulate a corrupt/zero stored value.
        await using var db = factory.CreateDbContext();
        db.GuildSettings.Add(new GuildSettingCustomValue
        {
            GuildId = _guildId,
            Type = GuildSettingType.SinAutoPardonDuration,
            SetBy = _modId,
            SetAt = DateTimeOffset.UtcNow,
            Value = TimeSpan.Zero.ToString("c")
        });
        await db.SaveChangesAsync();

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var result = await freshSut.GetAutoPardonDuration(_guildId);

        result.ShouldBe(TimeSpan.FromDays(10950));
    }

    [Fact]
    public async Task ResetAutoPardon_WritesDefaultAsCustomValue()
    {
        var result = await this._sut.ResetAutoPardonDuration(_guildId, _modId);

        result.ShouldBeOfType<SettingsWritten>();

        var freshSut = SettingsModuleFactory.Create(factory.ConnectionString);
        var duration = await freshSut.GetAutoPardonDuration(_guildId);
        duration.ShouldBe(TimeSpan.FromDays(10950));
    }
}
