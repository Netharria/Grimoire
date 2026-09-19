// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Grimoire.Features.Shared.Alerts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Grimoire.Utilities;

public abstract partial class GenericBackgroundService(
    IServiceProvider serviceProvider,
    ILogger<GenericBackgroundService> logger,
    TimeSpan timeSpan) : BackgroundService
{
    private const int UrgentAfterConsecutiveFailures = 3;
    private static readonly TimeSpan SummaryInterval = TimeSpan.FromMinutes(5);

    private readonly PeriodicTimer _timer = new(timeSpan);

    private long _runs;
    private long _failures;
    private long _totalMs;
    private long _maxMs;
    private int _consecutiveFailures;
    private DateTimeOffset _nextSummary = DateTimeOffset.UtcNow + SummaryInterval;

    private string TaskName => GetType().Name;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var randomTicks = Random.Shared.NextInt64(0, timeSpan.Ticks);
        var timeSpanDelay = TimeSpan.FromTicks(randomTicks);
        LogBackgroundTaskStart(logger, this.TaskName, timeSpanDelay);

        await Task.Delay(timeSpanDelay, cancellationToken);

        while (await this._timer.WaitForNextTickAsync(cancellationToken))
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                await RunTask(scope.ServiceProvider, cancellationToken);
                this._consecutiveFailures = 0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this._failures++;
                this._consecutiveFailures++;
                LogBackgroundTaskError(logger, ex, this.TaskName, this._consecutiveFailures, ex.Message);
                this.RaiseFailureAlert(ex);
            }

            this.RecordRun((long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private void RaiseFailureAlert(Exception ex)
        => serviceProvider.GetService<IAlertSender>()?.Send(new Alert
        {
            // A single failure is a warning; repeated failures mean the task is effectively down.
            Severity = this._consecutiveFailures >= UrgentAfterConsecutiveFailures
                ? AlertSeverity.Urgent
                : AlertSeverity.Warning,
            Type = "BackgroundTaskFailure",
            Discriminator = this.TaskName,
            Message = $"{this.TaskName} failed {this._consecutiveFailures} time(s) in a row: {ex.Message}",
            Exception = ex
        });

    private void RecordRun(long elapsedMs)
    {
        this._runs++;
        this._totalMs += elapsedMs;
        this._maxMs = Math.Max(this._maxMs, elapsedMs);

        if (DateTimeOffset.UtcNow < this._nextSummary) return;

        LogBackgroundTaskSummary(logger,
            this.TaskName,
            this._runs,
            this._failures,
            this._totalMs / this._runs,
            this._maxMs,
            Environment.WorkingSet / (1024 * 1024),
            GC.GetTotalMemory(false) / (1024 * 1024),
            GC.CollectionCount(2));

        this._runs = this._failures = this._totalMs = this._maxMs = 0;
        this._nextSummary = DateTimeOffset.UtcNow + SummaryInterval;
    }

    [LoggerMessage(LogLevel.Information, "Starting Background task {TaskName} with delay {TimeSpan}")]
    static partial void LogBackgroundTaskStart(ILogger logger, string taskName, TimeSpan timeSpan);

    [LoggerMessage(LogLevel.Error,
        "Exception was thrown when running background task {TaskName} (consecutive failures: {ConsecutiveFailures}). Message: ({Message})")]
    static partial void LogBackgroundTaskError(ILogger logger, Exception ex, string taskName, int consecutiveFailures,
        string message);

    [LoggerMessage(LogLevel.Information,
        "Background task {TaskName} summary: {Runs} runs, {Failures} failures, avg {AvgMs}ms, max {MaxMs}ms, working set {WorkingSetMb}MB, managed heap {HeapMb}MB, gen2 collections {Gen2Collections}")]
    static partial void LogBackgroundTaskSummary(ILogger logger, string taskName, long runs, long failures,
        long avgMs, long maxMs, long workingSetMb, long heapMb, int gen2Collections);

    protected abstract Task RunTask(IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
