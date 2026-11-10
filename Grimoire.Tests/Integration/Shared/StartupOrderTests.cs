// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Grimoire.Tests.Integration.Shared;

/// <summary>
///     The order hosted services start in (backlog A-50, XC-28). The host starts them one at a time in registration
///     order, and <c>DiscordStartService</c> applies migrations inside its <c>StartAsync</c>. So a service registered
///     before it can run its first database query while a long migration is still going. This reads the order from
///     <c>AddGrimoire</c> instead of running a slow migration.
/// </summary>
public sealed class StartupOrderTests
{
    /// <summary>XC-28: services that query the database should start after migrations are applied.</summary>
    [Fact(Skip = "Fails: WarningFlushService and DailyWarningReportService are registered before DiscordStartService, so they start before migrations finish. KI-106")]
    public void TheWarningServices_StartAfterMigrations()
    {
        var order = HostedServiceOrder(out var startsConcurrently);

        startsConcurrently.ShouldBeFalse();
        var migrations = order.IndexOf(typeof(DiscordStartService));
        migrations.ShouldBeGreaterThanOrEqualTo(0);
        order.IndexOf(typeof(WarningFlushService)).ShouldBeGreaterThan(migrations);
        order.IndexOf(typeof(DailyWarningReportService)).ShouldBeGreaterThan(migrations);
    }

    private static List<Type?> HostedServiceOrder(out bool startsConcurrently)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["token"] = "fake.discord.token",
            ["ConnectionStrings:Grimoire"] = "Host=localhost;Database=unused",
            ["pluralkitApiEndpoint"] = "http://pluralkit.fake.invalid/",
            ["pluralkitUserAgent"] = "Grimoire.Tests"
        }).Build();
        var services = new ServiceCollection().AddGrimoire(configuration);
        using var provider = services.BuildServiceProvider();
        startsConcurrently = provider.GetRequiredService<IOptions<HostOptions>>().Value.ServicesStartConcurrently;
        return [.. services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType)];
    }
}
