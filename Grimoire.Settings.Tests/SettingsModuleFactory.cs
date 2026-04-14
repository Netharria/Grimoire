// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using EntityFramework.Exceptions.PostgreSQL;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ZiggyCreatures.Caching.Fusion;

namespace Grimoire.Settings.Tests;

internal static class SettingsModuleFactory
{
    internal static SettingsModule Create(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddFusionCache().AsHybridCache();
        var provider = services.BuildServiceProvider();
        var hybridCache = provider.GetRequiredService<HybridCache>();

        var mockFactory = Substitute.For<IDbContextFactory<SettingsDbContext>>();
        mockFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(CreateDbContext(connectionString)));

        return new SettingsModule(mockFactory, hybridCache);
    }

    private static SettingsDbContext CreateDbContext(string connectionString)
        => new(new DbContextOptionsBuilder<SettingsDbContext>()
            .UseNpgsql(connectionString)
            .UseExceptionProcessor()
            .Options);
}
