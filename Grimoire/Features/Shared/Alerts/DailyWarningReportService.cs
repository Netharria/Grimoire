// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.Alerts;

/// <summary>Posts the open warnings to the warning channel once a day and prunes old rows.</summary>
public sealed class DailyWarningReportService(
    IServiceProvider serviceProvider,
    ILogger<GenericBackgroundService> logger,
    AlertChannels channels,
    IConfiguration configuration,
    TimeProvider timeProvider)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromMinutes(1))
{
    private static readonly TimeOnly DefaultReportTime = new(9, 0);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    private DateOnly? _lastReportDate;

    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var reportTime = TimeOnly.TryParse(configuration["warningReportTimeUtc"], out var configured)
            ? configured
            : DefaultReportTime;

        if (this._lastReportDate == today || TimeOnly.FromDateTime(now.UtcDateTime) < reportTime) return;

        await using var db = await serviceProvider.GetRequiredService<IDbContextFactory<GrimoireDbContext>>()
            .CreateDbContextAsync(cancellationToken);

        await AlertOccurrenceStore.DeleteOlderThanAsync(db, now - Retention, cancellationToken);

        if (channels.ConfiguredId(AlertSeverity.Warning) is null)
        {
            this._lastReportDate = today;
            return;
        }

        var open = await AlertOccurrenceStore.GetOpenAsync(db, cancellationToken);
        if (open.Count > 0)
        {
            // A missing channel is retried next tick and the rows stay open.
            if (await channels.GetAsync(AlertSeverity.Warning, cancellationToken) is not { } channel) return;

            await channel.SendMessageAsync(WarningReportFormatter.Build(open));
            await AlertOccurrenceStore.MarkReportedAsync(db, open.Select(x => x.Id).ToList(), now,
                cancellationToken);
        }

        this._lastReportDate = today;
    }
}
