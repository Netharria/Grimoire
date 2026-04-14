// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Grimoire.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Grimoire.Utilities;

internal sealed partial class DiscordStartService(
    DiscordClient discordClient,
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    IDbContextFactory<SettingsDbContext> settingsDbContextFactory,
    ILogger<DiscordStartService> logger) : IHostedService
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly DiscordClient _discordClient = discordClient;
    private readonly ILogger<DiscordStartService> _logger = logger;
    private readonly IDbContextFactory<SettingsDbContext> _settingsDbContextFactory = settingsDbContextFactory;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ApplyDatabaseMigrations(cancellationToken);
        //connect client
        await this._discordClient.ConnectAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => this._discordClient.DisconnectAsync();

    private async Task ApplyDatabaseMigrations(CancellationToken cancellationToken)
    {
        await using var context = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pendingMigrations.Any())
        {
            Stopwatch sw = new();
            sw.Start();
            await context.Database.MigrateAsync(cancellationToken);
            sw.Stop();
            LogMigrationDuration(this._logger, sw.ElapsedMilliseconds);
        }

        await SettingsServiceRegistration.MigrateSettingsDb(this._settingsDbContextFactory, this._logger,
            cancellationToken);
    }

    [LoggerMessage(LogLevel.Warning, "Applied pending migrations in {time} ms")]
    static partial void LogMigrationDuration(ILogger<DiscordStartService> logger, long time);
}
