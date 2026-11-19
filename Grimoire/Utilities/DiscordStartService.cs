// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Migrations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Grimoire.Utilities;

internal sealed class DiscordStartService(
    DiscordClient discordClient,
    IDbContextFactory<GrimoireMigrationsDbContext> migrationsContextFactory,
    ILogger<DiscordStartService> logger) : IHostedService
{
    private readonly ILogger<DiscordStartService> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ApplyDatabaseMigrations(cancellationToken);
        //connect client
        await discordClient.ConnectAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => discordClient.DisconnectAsync();

    /// <summary>Applies the one migration chain of both schemas before the bot connects (spec 2.0).</summary>
    private Task ApplyDatabaseMigrations(CancellationToken cancellationToken)
        => MigrationRunner.ApplyPendingAsync(migrationsContextFactory, this._logger, cancellationToken);
}
