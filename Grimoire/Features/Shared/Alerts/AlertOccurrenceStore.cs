// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Shared.Alerts;

public static class AlertOccurrenceStore
{
    /// <summary>Adds each warning to its open row, creating the row when there is none.</summary>
    public static async Task UpsertAsync(GrimoireDbContext db, IReadOnlyCollection<PendingWarning> warnings,
        CancellationToken cancellationToken)
    {
        var keys = warnings.Select(w => w.Key).ToList();
        var open = await db.AlertOccurrences
            .Where(x => x.ReportedAt == null && keys.Contains(x.AlertKey))
            .ToDictionaryAsync(x => x.AlertKey, cancellationToken);

        foreach (var warning in warnings)
            if (open.TryGetValue(warning.Key, out var existing))
                db.Entry(existing).CurrentValues.SetValues(existing with
                {
                    Count = existing.Count + warning.Count,
                    FirstSeen = existing.FirstSeen < warning.FirstSeen ? existing.FirstSeen : warning.FirstSeen,
                    LastSeen = existing.LastSeen > warning.LastSeen ? existing.LastSeen : warning.LastSeen,
                    GuildIds = existing.GuildIds.Union(warning.GuildIds).Take(PendingWarning.MaxGuildIds).ToArray()
                });
            else
                db.AlertOccurrences.Add(new AlertOccurrence
                {
                    AlertKey = warning.Key,
                    AlertType = warning.Type,
                    ExceptionType = warning.ExceptionType,
                    Count = warning.Count,
                    FirstSeen = warning.FirstSeen,
                    LastSeen = warning.LastSeen,
                    SampleMessage = warning.SampleMessage,
                    GuildIds = warning.GuildIds.ToArray()
                });

        await db.SaveChangesAsync(cancellationToken);
    }

    public static Task<List<AlertOccurrence>> GetOpenAsync(GrimoireDbContext db, CancellationToken cancellationToken)
        => db.AlertOccurrences
            .Where(x => x.ReportedAt == null)
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

    public static Task<int> MarkReportedAsync(GrimoireDbContext db, IReadOnlyCollection<long> ids,
        DateTimeOffset reportedAt, CancellationToken cancellationToken)
        => db.AlertOccurrences
            .Where(x => x.ReportedAt == null && ids.Contains(x.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReportedAt, reportedAt), cancellationToken);

    public static Task<int> DeleteOlderThanAsync(GrimoireDbContext db, DateTimeOffset cutoff,
        CancellationToken cancellationToken)
        => db.AlertOccurrences
            .Where(x => x.LastSeen < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
}
