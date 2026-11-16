// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.Effects;
using Grimoire.Functional.Runtime;
using LanguageExt;
using LanguageExt.Traits;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Unit = LanguageExt.Unit;

namespace Grimoire.Functional.Boundary;

/// <summary>
///     The event boundary (spec 1.2, section 2.2): runs a handler's effect and reports a failure by its case. The
///     effect never fails with a Grimoire case, so nothing reaches DSharpPlus's <c>IClientErrorHandler</c>.
/// </summary>
public static class EventBoundary<RT>
    where RT : Has<Eff<RT>, ILogIO>, Has<Eff<RT>, IAlertIO>, Has<Eff<RT>, IGuildIssueIO>
{
    private const string Category = "Grimoire.Events";

    public static Eff<RT, CommandOutcome> Run(string handler, Option<GuildId> guild, Eff<RT, Unit> effect) =>
        Boundary<RT>.Attempt(effect).Bind(result => result.Match(
            Succ: _ => Eff<RT, CommandOutcome>.Pure(CommandOutcome.Success),
            Fail: error => Failed(handler, guild, error)));

    private static Eff<RT, CommandOutcome> Failed(string handler, Option<GuildId> guild, Error error) =>
        from shuttingDown in Boundary<RT>.ShuttingDown
        let outcome = Outcome.Classify(error, shuttingDown)
        from _ in outcome switch
        {
            // The thing was deleted while the handler ran: nothing to do, and nothing to report.
            CommandOutcome.NotFound => Log<RT>.Debug(Category,
                "Event handler {HandlerName} found nothing to act on in guild {GuildId}: {Reason}",
                handler, GuildOf(guild), error.Messages),
            CommandOutcome.Cancelled => Log<RT>.Information(Category,
                "Event handler {HandlerName} was cancelled by shutdown", handler),
            CommandOutcome.Invalid or CommandOutcome.Forbidden => Report(handler, guild, error, outcome,
                LogLevel.Warning, AlertSeverity.Warning),
            _ => Report(handler, guild, error, outcome, LogLevel.Error, AlertSeverity.Urgent)
        }
        from __ in BotRefusals<RT>.Report(error, Category)
        select outcome;

    private static Eff<RT, Unit> Report(string handler, Option<GuildId> guild, Error error, CommandOutcome outcome,
        LogLevel level, AlertSeverity severity) =>
        from _ in Log<RT>.Write(new LogEntry(level, Category,
            "Event handler {HandlerName} failed ({Outcome}) in guild {GuildId}: {Reason}",
            [handler, outcome, GuildOf(guild), error.Messages], Boundary<RT>.ExceptionOf(error)))
        from __ in Alerts<RT>.Send(new Alert
        {
            Severity = severity,
            Type = outcome switch
            {
                CommandOutcome.Defect => "UnhandledException",
                CommandOutcome.Unavailable => "Unavailable",
                _ => "EventHandlerFailure"
            },
            Discriminator = handler,
            Message = $"{handler} failed ({outcome}): {error.Messages}",
            Exception = Boundary<RT>.ExceptionOf(error).OrNull(),
            GuildId = guild.ToNullable()
        })
        select unit;

    private static ulong? GuildOf(Option<GuildId> guild) => guild.Map(id => id.Value).ToNullable();
}

/// <summary>
///     Where a handler's effect meets DSharpPlus. The handler builds its effect from a Grimoire record copied out of the
///     event args (C5) and hands it here; a throw while building it is caught and reported as a defect.
/// </summary>
public sealed partial class EventRunner(GrimoireEnv env, IHostApplicationLifetime lifetime, ILogger<EventRunner> logger)
{
    public async Task Run(string handler, Option<GuildId> guild, Func<Eff<GrimoireRuntime, Unit>> effect)
    {
        var boundary = EventBoundary<GrimoireRuntime>.Run(handler, guild,
            Eff<GrimoireRuntime, Unit>.Pure(unit).Bind(_ => effect()));

        using var envIO = EnvIO.New(token: lifetime.ApplicationStopping);
        var result = await boundary.RunAsync(new GrimoireRuntime(env), envIO);
        if (result is Fin<CommandOutcome>.Fail(var error))
            LogBoundaryFailed(logger, Boundary<GrimoireRuntime>.ExceptionOf(error).OrNull(), handler, error.Message);
    }

    [LoggerMessage(LogLevel.Error, "The event boundary for {HandlerName} failed without reporting: {Reason}")]
    private static partial void LogBoundaryFailed(ILogger logger, Exception? exception, string handlerName,
        string reason);
}
