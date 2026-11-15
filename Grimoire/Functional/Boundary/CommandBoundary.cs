// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.Effects;
using LanguageExt;
using LanguageExt.Traits;
using Unit = LanguageExt.Unit;

namespace Grimoire.Functional.Boundary;

/// <summary>
///     The command boundary (spec 1.2, section 2.1): privacy, then validate, then defer, then act, then render. Every
///     failure ends here as a reply, a log line and, for outages and defects, an alert. The effect never fails with a
///     Grimoire case; only a cancelled run can make it fail, which the runner handles.
/// </summary>
public static class CommandBoundary<RT>
    where RT : Has<Eff<RT>, ICommandIO>,
    Has<Eff<RT>, ILogIO>,
    Has<Eff<RT>, IAlertIO>,
    Has<Eff<RT>, IGuildLogIO>,
    Has<Eff<RT>, ITraceIO>
{
    private const string Category = "Grimoire.Commands";

    public static Eff<RT, CommandOutcome> Run<TIn>(
        Invocation invocation,
        Eff<RT, Privacy> privacy,
        Func<Invocation, Validation<Error, TIn>> input,
        Func<TIn, Eff<RT, Reply>> act) =>
        Boundary<RT>.Attempt(privacy).Bind(read => read.Match(
            // Nothing is known about the privacy yet: answer publicly. On slash, RefusalDelivery makes it ephemeral.
            Fail: error => Failed(invocation, Delivery.Public, error),
            Succ: chosen => DeliveryRules.For(chosen, invocation.Kind).Match(
                None: () => PrivateOnText(invocation),
                Some: delivery => Validate(invocation, delivery, input, act))));

    private static Eff<RT, CommandOutcome> PrivateOnText(Invocation invocation) =>
        from _ in Log<RT>.Debug(Category, "Command {Command} is private; reacted to its text use in guild {GuildId}",
            invocation.Command, GuildOf(invocation))
        from __ in Boundary<RT>.Quietly(Command<RT>.React(DeliveryRules.PrivateOnTextEmoji), Category,
            "Could not react to the text use of {Command}", invocation.Command)
        select CommandOutcome.PrivateOnText;

    private static Eff<RT, CommandOutcome> Validate<TIn>(
        Invocation invocation,
        Delivery delivery,
        Func<Invocation, Validation<Error, TIn>> input,
        Func<TIn, Eff<RT, Reply>> act) =>
        // Lifted, so a throw while building the input is a defect like any other.
        Boundary<RT>.Attempt(Eff<RT, Validation<Error, TIn>>.Lift(() => input(invocation))
                .Bind(Effect.Validated<RT, TIn>))
            .Bind(valid => valid.Match(
                Fail: error => Failed(invocation, delivery, error),
                Succ: value => DeferAndAct(invocation, delivery, value, act)));

    private static Eff<RT, CommandOutcome> DeferAndAct<TIn>(
        Invocation invocation,
        Delivery delivery,
        TIn value,
        Func<TIn, Eff<RT, Reply>> act) =>
        Boundary<RT>.Attempt(Command<RT>.Defer(delivery)).Bind(deferred => deferred.Match(
            Fail: error => DeferFailed(invocation, delivery, error),
            Succ: _ => Boundary<RT>.Attempt(Eff<RT, Unit>.Pure(unit).Bind(_ => act(value))).Bind(acted => acted.Match(
                Fail: error => Failed(invocation, delivery, error),
                Succ: reply => Render(invocation, delivery, reply)))));

    /// <summary>A failed defer leaves nothing to reply to: it's logged and alerted, and the act doesn't run.</summary>
    private static Eff<RT, CommandOutcome> DeferFailed(Invocation invocation, Delivery delivery, Error error) =>
        IsDirectMessageClosed(delivery, error)
            ? DirectMessageClosed(invocation, error)
            : from shuttingDown in Boundary<RT>.ShuttingDown
              let outcome = Outcome.Classify(error, shuttingDown)
              from _ in ReportReplyFailure(invocation, "defer", error)
              select outcome;

    /// <summary>
    ///     Discord refuses a DM only when the bot sends it, so a DM-only text command learns that the member's DMs
    ///     are closed at the defer or at the first reply. Either way it answers with the 🔒 reaction instead.
    /// </summary>
    private static bool IsDirectMessageClosed(Delivery delivery, Error error) =>
        delivery is Delivery.DirectMessage && Outcome.Classify(error, false) is CommandOutcome.Forbidden;

    private static Eff<RT, CommandOutcome> DirectMessageClosed(Invocation invocation, Error error) =>
        from _ in Log<RT>.Debug(Category, "Could not DM the invoker of {Command}: {Reason}",
            invocation.Command, error.Message)
        from __ in Boundary<RT>.Quietly(Command<RT>.React(DeliveryRules.PrivateOnTextEmoji), Category,
            "Could not react to the text use of {Command}", invocation.Command)
        select CommandOutcome.Forbidden;

    /// <summary>Sends a refusal or the generic reply. Its failure is only logged: the alert, if any, already went.</summary>
    private static Eff<RT, Unit> SendRefusal(Invocation invocation, Delivery delivery, DiscordEmbed embed) =>
        Boundary<RT>.Attempt(Command<RT>.Refuse(delivery, embed)).Bind(sent => sent.Match(
            Succ: Eff<RT, Unit>.Pure,
            Fail: error => IsDirectMessageClosed(delivery, error)
                ? DirectMessageClosed(invocation, error).Map(_ => unit)
                : Log<RT>.Warning(Category, Boundary<RT>.ExceptionOf(error),
                    "Could not send the error reply for {Command}: {Reason}", invocation.Command, error.Message)));

    private static Eff<RT, CommandOutcome> Render(Invocation invocation, Delivery delivery, Reply reply) =>
        Boundary<RT>.Attempt(Command<RT>.Reply(delivery, reply.Embed, reply.FollowUps)).Bind(sent => sent.Match(
            // The act's work is done; nothing is retried (Q25).
            Fail: error => IsDirectMessageClosed(delivery, error)
                ? DirectMessageClosed(invocation, error)
                : ReportReplyFailure(invocation, "reply", error).Map(_ => CommandOutcome.Success),
            Succ: _ => reply.Log.Match(
                log => Boundary<RT>.Attempt(GuildLogs<RT>.Send(log)).Bind(posted => posted.Match(
                    Fail: error => ReportReplyFailure(invocation, "guild log post", error)
                        .Map(_ => CommandOutcome.Success),
                    Succ: _ => Eff<RT, CommandOutcome>.Pure(CommandOutcome.Success))),
                () => Eff<RT, CommandOutcome>.Pure(CommandOutcome.Success))));

    private static Eff<RT, CommandOutcome> Failed(Invocation invocation, Delivery delivery, Error error) =>
        from shuttingDown in Boundary<RT>.ShuttingDown
        let outcome = Outcome.Classify(error, shuttingDown)
        let refuseVia = RefusalDelivery(invocation.Kind, delivery)
        from result in outcome switch
        {
            _ when Outcome.IsRefusal(outcome) => Refused(invocation, refuseVia, error),
            CommandOutcome.Cancelled => Log<RT>.Information(Category,
                "Command {Command} was cancelled by shutdown", invocation.Command),
            _ => Crashed(invocation, refuseVia, outcome, error)
        }
        select outcome;

    /// <summary>
    ///     Every refusal and the generic reply are ephemeral on slash, even for a public command (Q3). A text command
    ///     has no private reply, so it answers the way its replies go: publicly, or by DM.
    /// </summary>
    private static Delivery RefusalDelivery(InvocationKind kind, Delivery delivery) =>
        kind is InvocationKind.Slash ? Delivery.Ephemeral : delivery;

    private static Eff<RT, Unit> Refused(Invocation invocation, Delivery delivery, Error error) =>
        from _ in Log<RT>.Information(Category, "Command {Command} rejected in guild {GuildId}: {Reason}",
            invocation.Command, GuildOf(invocation), error.Messages)
        from __ in SendRefusal(invocation, delivery, Replies.Refusal(error))
        select unit;

    /// <summary>An outage or a defect: logged and alerted first, because the reply is the step most likely to fail.</summary>
    private static Eff<RT, Unit> Crashed(Invocation invocation, Delivery delivery, CommandOutcome outcome,
        Error error) =>
        from errorId in Trace<RT>.ErrorId
        from _ in Log<RT>.Error(Category, Boundary<RT>.ExceptionOf(error),
            "Error on Command: [ID {ErrorId}] {InteractionName} ({Outcome}) in guild {GuildId}: {Reason}",
            errorId, invocation.Command, outcome, GuildOf(invocation), error.Message)
        from __ in Alerts<RT>.Send(new Alert
        {
            Severity = AlertSeverity.Urgent,
            Type = outcome is CommandOutcome.Unavailable ? "Unavailable" : "UnhandledException",
            Discriminator = invocation.Command,
            Message = outcome is CommandOutcome.Unavailable
                ? $"{invocation.Command} could not reach a dependency [Id `{errorId}`]: {error.Message}"
                : $"Encountered exception while executing {invocation.Command} [Id `{errorId}`]",
            Exception = Boundary<RT>.ExceptionOf(error).OrNull(),
            GuildId = invocation.Guild.ToNullable()
        })
        from ___ in SendRefusal(invocation, delivery, Replies.Generic(invocation.Command, errorId))
        select unit;

    private static Eff<RT, Unit> ReportReplyFailure(Invocation invocation, string step, Error error) =>
        from _ in Log<RT>.Warning(Category, Boundary<RT>.ExceptionOf(error),
            "Command {Command}: the {Step} failed: {Reason}", invocation.Command, step, error.Message)
        from __ in Alerts<RT>.Send(new Alert
        {
            Severity = AlertSeverity.Warning,
            Type = "CommandReplyFailure",
            Discriminator = invocation.Command,
            Message = $"{invocation.Command}: the {step} failed: {error.Message}",
            Exception = Boundary<RT>.ExceptionOf(error).OrNull(),
            GuildId = invocation.Guild.ToNullable()
        })
        select unit;

    private static ulong? GuildOf(Invocation invocation) => invocation.Guild.Map(id => id.Value).ToNullable();
}
