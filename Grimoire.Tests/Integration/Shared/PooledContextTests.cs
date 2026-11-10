// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     Review P-8: both contexts come from pooled factories. A pooled context is reused, so state set on one lease must
///     not reach the next. The only state the bot sets is the long command timeout for startup migrations.
/// </summary>
[Collection("Test collection")]
public sealed class PooledContextTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private readonly GrimoireCoreFactory _database = database;
    private GrimoireTestBot _bot = null!;

    public async ValueTask InitializeAsync()
    {
        await this._database.ResetDatabase();
        this._bot = await GrimoireTestBot.StartAsync(this._database);
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    [Fact]
    public Task TheMainContext_IsPooled_AndTheMigrationTimeoutDoesNotCarryOver()
        => AssertPooledWithoutTimeoutLeakAsync(this._bot.Services.GetRequiredService<IDbContextFactory<GrimoireDbContext>>());

    [Fact]
    public Task TheSettingsContext_IsPooled_AndTheMigrationTimeoutDoesNotCarryOver()
        => AssertPooledWithoutTimeoutLeakAsync(this._bot.Services.GetRequiredService<IDbContextFactory<SettingsDbContext>>());

    private static async Task AssertPooledWithoutTimeoutLeakAsync<TContext>(IDbContextFactory<TContext> factory)
        where TContext : DbContext
    {
        var token = TestContext.Current.CancellationToken;
        TContext first;
        int? defaultTimeout;
        await using (first = await factory.CreateDbContextAsync(token))
        {
            defaultTimeout = first.Database.GetCommandTimeout();
            first.Database.SetCommandTimeout(SettingsServiceRegistration.MigrationCommandTimeout);
        }

        await using var second = await factory.CreateDbContextAsync(token);

        // The same instance back from the pool shows pooling is on; otherwise the timeout check proves nothing.
        second.ShouldBeSameAs(first);
        second.Database.GetCommandTimeout().ShouldBe(defaultTimeout);
    }
}
