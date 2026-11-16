// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Settings.Enums;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unit = LanguageExt.Unit;

namespace Grimoire.Functional.Capabilities;

// The production implementations: the imperative shell. Each method only builds an IO; the work happens when the
// effect runs. A thrown exception becomes an error in the effect, with no try/catch here.

internal sealed class LiveTimeIO(TimeProvider timeProvider) : ITimeIO
{
    public IO<DateTimeOffset> UtcNow => IO.lift(timeProvider.GetUtcNow);
    public IO<long> Timestamp => IO.lift(timeProvider.GetTimestamp);
    public IO<TimeSpan> ElapsedSince(long timestamp) => IO.lift(() => timeProvider.GetElapsedTime(timestamp));
}

internal sealed class LiveLogIO(ILoggerFactory loggerFactory) : ILogIO
{
    public IO<Unit> Write(LogEntry entry) =>
        IO.lift(() =>
        {
#pragma warning disable CA2254 // The template is a constant at each call site; it's passed through, not built.
            loggerFactory.CreateLogger(entry.Category)
                .Log(entry.Level, entry.Exception.OrNull(), entry.Template, [..entry.Args]);
#pragma warning restore CA2254
            return unit;
        });
}

/// <summary>
///     Resolves <see cref="IAlertSender" /> on first use: it depends on <c>DiscordClient</c>, which depends on the
///     error handler, so resolving it at construction would be a cycle.
/// </summary>
internal sealed class LiveAlertIO(IServiceProvider services) : IAlertIO
{
    public IO<Unit> Send(Alert alert) =>
        IO.lift(() =>
        {
            services.GetService<IAlertSender>()?.Send(alert);
            return unit;
        });
}

internal sealed class LiveGuildLogIO(GuildLog guildLog) : IGuildLogIO
{
    public IO<bool> IsLogged(GuildId guild, GuildLogType type) =>
        IO.liftAsync(env => guildLog.IsLoggedAsync(guild, type, env.Token));

    public IO<Unit> Enqueue(GuildLogMessage message) =>
        IO.liftAsync(async env =>
        {
            await guildLog.EnqueueAsync(message, env.Token);
            return unit;
        });
}

/// <summary>
///     Starts the report and returns. The reporter posts through the guild log queue, so waiting for it from inside the
///     publisher, the queue's only reader, could wait forever. A failed report is logged by the continuation.
/// </summary>
internal sealed partial class LiveGuildIssueIO(GuildIssueReporter reporter, ILogger<LiveGuildIssueIO> logger)
    : IGuildIssueIO
{
    public IO<Unit> Report(GuildIssue issue) =>
        IO.lift(() =>
        {
            _ = reporter.ReportAsync(issue).ContinueWith(
                task => LogReportFailure(logger, task.Exception!, issue.GuildId, issue.Code),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            return unit;
        });

    [LoggerMessage(LogLevel.Error, "Could not report setup problem {Code} for guild {GuildId}")]
    private static partial void LogReportFailure(ILogger logger, Exception exception, GuildId guildId, string code);
}

/// <summary>10 random hex characters, as the old error handler made. Section 1.6 replaces this with the trace id.</summary>
internal sealed class LiveTraceIO : ITraceIO
{
    public IO<string> ErrorId => IO.lift(() => RandomNumberGenerator.GetHexString(10));
}
