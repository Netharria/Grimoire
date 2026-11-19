// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Grimoire.Migrations;

/// <summary>Applies the migration chain at startup (spec 2.0).</summary>
public static partial class MigrationRunner
{
    /// <summary>
    ///     How long one migration statement may run. A migration can rewrite a large table in one statement, which
    ///     Npgsql's default of 30 seconds can't fit, and a timeout rolls the migration back (P-3). 2.1 measured the
    ///     longest at about 2 minutes on production.
    /// </summary>
    public static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    ///     Registers the migrations context over the host's <see cref="NpgsqlDataSource" />. No retrying execution
    ///     strategy: a failed migration is rolled back and the host stops, and retrying it would change nothing.
    /// </summary>
    public static IServiceCollection AddMigrations(this IServiceCollection services)
        => services.AddDbContextFactory<GrimoireMigrationsDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()));

    /// <summary>
    ///     Applies every pending migration of both schemas, in id order, and logs how long it took. Does nothing when
    ///     none is pending. A failure propagates, so the host stops before the bot connects.
    /// </summary>
    public static async Task ApplyPendingAsync(IDbContextFactory<GrimoireMigrationsDbContext> contextFactory,
        ILogger logger, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);
        if (!pending.Any())
            return;

        var started = Stopwatch.GetTimestamp();
        context.Database.SetCommandTimeout(CommandTimeout);
        await context.Database.MigrateAsync(cancellationToken);
        LogMigrationDuration(logger, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    [LoggerMessage(LogLevel.Warning, "Applied pending migrations in {time} ms")]
    static partial void LogMigrationDuration(ILogger logger, long time);
}
