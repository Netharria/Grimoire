// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.Alerts;

/// <summary>Persists buffered warnings so the daily report survives restarts.</summary>
public sealed partial class WarningFlushService(
    IServiceProvider serviceProvider,
    ILogger<GenericBackgroundService> baseLogger,
    ILogger<WarningFlushService> logger,
    WarningBuffer warningBuffer)
    : GenericBackgroundService(serviceProvider, baseLogger, TimeSpan.FromSeconds(30))
{
    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var warnings = warningBuffer.Drain();
        if (warnings.Count == 0) return;

        try
        {
            await using var db = await serviceProvider.GetRequiredService<IDbContextFactory<GrimoireDbContext>>()
                .CreateDbContextAsync(cancellationToken);
            await AlertOccurrenceStore.UpsertAsync(db, warnings, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Drop the batch and only log. Raising an alert here would feed the buffer this service is draining.
            LogFlushFailed(logger, ex, warnings.Count);
        }
    }

    [LoggerMessage(LogLevel.Error, "Failed to persist {WarningCount} buffered warnings; the batch was dropped")]
    private static partial void LogFlushFailed(ILogger logger, Exception exception, int warningCount);
}
