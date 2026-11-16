// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.Effects;
using Grimoire.Functional.PluralKit;
using LanguageExt;
using LanguageExt.Traits;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.PluralKit;

/// <summary>
///     The delete log's use of PluralKit (spec 1.5, section 2.2). The lookup is optional: whatever goes wrong, the
///     delete is still logged (I-3), so a failure is logged by its problem and answered as
///     <see cref="ProxyLookup.NotProxied" />. Only cancellation gets through.
/// </summary>
public static class DeletedMessageLookup<RT>
    where RT : Has<Eff<RT>, IPluralKitIO>, Has<Eff<RT>, ITimeIO>, Has<Eff<RT>, ILogIO>, Has<Eff<RT>, IAlertIO>
{
    private const string Category = "Grimoire.PluralKit";

    public static Eff<RT, ProxyLookup> Run(MessageId deleted, DateTimeOffset createdAt) =>
        PluralKit<RT>.Lookup(deleted, createdAt)
            .Bind(result => result is ProxyLookup.ReplacedByProxyOfDeletedMember gone
                ? Log<RT>.Debug(Category,
                        "Message {MessageId} was replaced by PluralKit proxy {ProxyId} of a deleted member, so it isn't logged or linked",
                        deleted.Value, gone.Proxy.Value)
                    .Map(_ => result)
                : Eff<RT, ProxyLookup>.Pure(result))
            .Catch(error => !error.Is(Errors.Cancelled), error => Failed(deleted, error)).As();

    private static Eff<RT, ProxyLookup> Failed(MessageId deleted, Error error)
    {
        var problem = PluralKitErrors.ProblemOf(error);
        var exception = PluralKitErrors.ExceptionOf(error);
        var level = problem.Match(
            found => found switch
            {
                PluralKitProblem.Outage or PluralKitProblem.TooSlow => LogLevel.Information,
                PluralKitProblem.Unexpected => LogLevel.Error,
                _ => LogLevel.Warning
            },
            () => LogLevel.Error);
        // A refusal or a bug won't fix itself, so someone is told; the daily warning report collects repeats.
        var alert = problem.Match(found => found is PluralKitProblem.Refused or PluralKitProblem.Unexpected, true);

        return from _ in Log<RT>.Write(new LogEntry(level, Category,
                "PluralKit lookup for message {MessageId} failed ({Problem}), so it's handled as an ordinary message: {Reason}",
                [deleted.Value, problem.Map(found => found.ToString()).IfNone("Unexpected"), ReasonOf(error, exception)],
                exception))
            from __ in alert
                ? Alerts<RT>.Send(new Alert
                {
                    Severity = AlertSeverity.Warning,
                    Type = "PluralKitLookupFailed",
                    Message = $"PluralKit lookups are failing: {ReasonOf(error, exception)}",
                    Exception = exception.OrNull()
                })
                : Eff<RT, Unit>.Pure(unit)
            select (ProxyLookup)new ProxyLookup.NotProxied();
    }

    private static string ReasonOf(Error error, Option<Exception> exception) =>
        exception.Case is PluralKitCallException call ? call.Message : error.Message;
}
