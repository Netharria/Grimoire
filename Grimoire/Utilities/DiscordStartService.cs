// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Grimoire.Utilities;
internal class DiscordStartService (
    DiscordClient discordClient,
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    ILogger<DiscordStartService> logger) : IHostedService
{
    private readonly DiscordClient _discordClient = discordClient;
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<DiscordStartService> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pendingMigrations.Any())
        {
            Stopwatch sw = new();
            sw.Start();
            await context.Database.MigrateAsync(cancellationToken);
            sw.Stop();
            this._logger.LogWarning("Applied pending migrations in {Time} ms", sw.ElapsedMilliseconds);
        }

        //connect client
        await _discordClient.ConnectAsync();
    }
    public Task StopAsync(CancellationToken cancellationToken)
        => _discordClient.DisconnectAsync();
}
