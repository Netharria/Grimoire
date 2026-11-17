// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Grimoire.Functional.Telemetry;

/// <summary>
///     Says when telemetry is being lost (spec 1.6, section 3.10). The OpenTelemetry SDK reports dropped data and
///     failed exports only through its own EventSources; this listens to their warnings and writes each kind to the
///     log at most once a minute. The console, and so App Platform's runtime logs, always has it, even while the
///     export itself is what's failing.
/// </summary>
internal sealed partial class ExportHealth(ILogger<ExportHealth> logger, TimeProvider time)
    : EventListener, IHostedService
{
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<(string Source, int Id), DateTimeOffset> _lastWritten = [];

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.Dispose();
        return Task.CompletedTask;
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name.StartsWith("OpenTelemetry-", StringComparison.Ordinal))
            this.EnableEvents(eventSource, EventLevel.Warning);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        // The listener can be called before the primary constructor's parameters are usable.
        if (logger is null || time is null || eventData.Level > EventLevel.Warning)
            return;

        var key = (eventData.EventSource.Name, eventData.EventId);
        var now = time.GetUtcNow();
        if (this._lastWritten.TryGetValue(key, out var last) && now - last < Every)
            return;
        this._lastWritten[key] = now;

        var message = eventData.Message is { Length: > 0 } template && eventData.Payload is { Count: > 0 } payload
            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, template, [.. payload])
            : eventData.Message ?? eventData.EventName ?? "an SDK warning";
        LogExportProblem(logger, eventData.EventSource.Name, message);
    }

    [LoggerMessage(LogLevel.Warning, "Telemetry export problem ({Source}, at most once a minute): {Problem}")]
    private static partial void LogExportProblem(ILogger logger, string source, string problem);
}
