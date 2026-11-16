// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.Channels.GuildLog;
using System.Data;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.Data;
using Grimoire.Functional.Discord;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Settings.Enums;
using LanguageExt;
using LanguageExt.Traits;
using Microsoft.Extensions.Logging;
using Unit = LanguageExt.Unit;

namespace Grimoire.Functional.Effects;

// The functions feature code calls. Each module is generic in the runtime and names the one capability it needs, so a
// function's where-clause lists exactly the effects it can have: code constrained only by Has<Eff<RT>, ITimeIO> can't
// reply to a command, because that call wouldn't compile.

public static class Clock<RT> where RT : Has<Eff<RT>, ITimeIO>
{
    private static Eff<RT, ITimeIO> Trait => Has<Eff<RT>, RT, ITimeIO>.ask.As();

    public static Eff<RT, DateTimeOffset> UtcNow => Trait.Bind(time => time.UtcNow);
    public static Eff<RT, long> Timestamp => Trait.Bind(time => time.Timestamp);
    public static Eff<RT, TimeSpan> ElapsedSince(long timestamp) => Trait.Bind(time => time.ElapsedSince(timestamp));
}

public static class Log<RT> where RT : Has<Eff<RT>, ILogIO>
{
    private static Eff<RT, ILogIO> Trait => Has<Eff<RT>, RT, ILogIO>.ask.As();

    public static Eff<RT, Unit> Write(LogEntry entry) => Trait.Bind(log => log.Write(entry));

    public static Eff<RT, Unit> Debug(string category, string template, params object?[] args) =>
        Write(new LogEntry(LogLevel.Debug, category, template, toSeq(args), None));

    public static Eff<RT, Unit> Information(string category, string template, params object?[] args) =>
        Write(new LogEntry(LogLevel.Information, category, template, toSeq(args), None));

    public static Eff<RT, Unit> Warning(string category, Option<Exception> exception, string template,
        params object?[] args) =>
        Write(new LogEntry(LogLevel.Warning, category, template, toSeq(args), exception));

    public static Eff<RT, Unit> Error(string category, Option<Exception> exception, string template,
        params object?[] args) =>
        Write(new LogEntry(LogLevel.Error, category, template, toSeq(args), exception));
}

public static class Alerts<RT> where RT : Has<Eff<RT>, IAlertIO>
{
    public static Eff<RT, Unit> Send(Alert alert) =>
        Has<Eff<RT>, RT, IAlertIO>.ask.As().Bind(alerts => alerts.Send(alert));
}

public static class GuildLogs<RT> where RT : Has<Eff<RT>, IGuildLogIO>
{
    private static Eff<RT, IGuildLogIO> Trait => Has<Eff<RT>, RT, IGuildLogIO>.ask.As();

    /// <summary>
    ///     Runs <paramref name="build" /> only when someone will see the log (Q26b, LOG-2), then queues it. The only way
    ///     ported code posts a log. A failing build fails the post with its case; the caller decides what that means.
    /// </summary>
    public static Eff<RT, Unit> Post(GuildId guild, GuildLogType type, Eff<RT, GuildLogMessage> build) =>
        from logged in Trait.Bind(log => log.IsLogged(guild, type))
        from _ in logged
            ? build.Bind(message => Trait.Bind(log => log.Enqueue(message)))
            : Effect.Done<RT>()
        select unit;

    /// <summary>A message that's already built.</summary>
    public static Eff<RT, Unit> Post(GuildLogMessage message) =>
        Post(message.Guild, message.Type, Eff<RT, GuildLogMessage>.Pure(message));
}

public static class GuildIssues<RT> where RT : Has<Eff<RT>, IGuildIssueIO>
{
    public static Eff<RT, Unit> Report(GuildIssue issue) =>
        Has<Eff<RT>, RT, IGuildIssueIO>.ask.As().Bind(issues => issues.Report(issue));
}

/// <summary>
///     Discord calls (spec 1.4). Lookups and idempotent calls are retried while the failure is transient; calls that
///     create something never are (Q25). The choice is made here, per call, so unit tests see it.
/// </summary>
public static class Discord<RT> where RT : Has<Eff<RT>, IDiscordIO>
{
    private static Eff<RT, IDiscordIO> Trait => Has<Eff<RT>, RT, IDiscordIO>.ask.As();

    public static Eff<RT, ChannelSnapshot> GetChannel(ChannelId channel) =>
        Retried(discord => discord.GetChannel(channel));

    public static Eff<RT, UserSnapshot> GetUser(UserId user) => Retried(discord => discord.GetUser(user));

    /// <summary><c>NotFound</c> when the member left: only that means "gone" (TD-12).</summary>
    public static Eff<RT, MemberSnapshot> GetMember(GuildId guild, UserId user) =>
        Retried(discord => discord.GetMember(guild, user));

    public static Eff<RT, RoleSnapshot> GetRole(GuildId guild, RoleId role) =>
        Retried(discord => discord.GetRole(guild, role));

    public static Eff<RT, MessageSnapshot> GetMessage(ChannelId channel, MessageId message) =>
        Retried(discord => discord.GetMessage(channel, message));

    /// <summary>Never retried: a 502 may come after Discord already posted it, and a retry would post twice (Q25).</summary>
    public static Eff<RT, MessageId> SendMessage(ChannelId channel, DiscordMessageBuilder message) =>
        Trait.Bind(discord => discord.SendMessage(channel, message));

    /// <summary>Never retried (Q25).</summary>
    public static Eff<RT, Unit> SendDirectMessage(UserId user, DiscordMessageBuilder message) =>
        Trait.Bind(discord => discord.SendDirectMessage(user, message));

    /// <summary>Retried: a retry after a delete that went through finds the message gone, which counts as done.</summary>
    public static Eff<RT, Unit> DeleteMessage(ChannelId channel, MessageId message, string reason) =>
        Retried(discord => discord.DeleteMessage(channel, message, reason));

    private static Eff<RT, A> Retried<A>(Func<IDiscordIO, IO<A>> call) =>
        Trait.Bind(discord => call(discord).RetryWhile(discord.ReadRetry, DiscordErrors.IsTransient));
}

public static class Trace<RT> where RT : Has<Eff<RT>, ITraceIO>
{
    public static Eff<RT, string> ErrorId => Has<Eff<RT>, RT, ITraceIO>.ask.As().Bind(trace => trace.ErrorId);
}

public static class Command<RT> where RT : Has<Eff<RT>, ICommandIO>
{
    private static Eff<RT, ICommandIO> Trait => Has<Eff<RT>, RT, ICommandIO>.ask.As();

    public static Eff<RT, Unit> Defer(Delivery delivery) => Trait.Bind(command => command.Defer(delivery));

    public static Eff<RT, Unit> Reply(Delivery delivery, DiscordEmbed embed, Seq<DiscordEmbed> followUps) =>
        Trait.Bind(command => command.Reply(delivery, embed, followUps));

    public static Eff<RT, Unit> Refuse(Delivery delivery, DiscordEmbed embed) =>
        Trait.Bind(command => command.Refuse(delivery, embed));

    public static Eff<RT, Unit> React(string emoji) => Trait.Bind(command => command.React(emoji));
}

/// <summary>
///     The database, as features use it (spec 1.3). Reads (<see cref="Sql.IsRead" />) are retried on the
///     capability's schedule while the failure is transient; writes, transactions and streams never are (Q25).
/// </summary>
public static class Db<RT> where RT : Has<Eff<RT>, IDatabaseIO>
{
    private static Eff<RT, IDatabaseIO> Trait => Has<Eff<RT>, RT, IDatabaseIO>.ask.As();

    public static Eff<RT, Seq<A>> Query<A>(Sql sql) => Retrying(sql, db => db.Query<A>(sql));

    /// <summary>The first row, or None. Bind it through <c>Effect.Require</c> when absence is a <c>NotFound</c>.</summary>
    public static Eff<RT, Option<A>> QueryOptional<A>(Sql sql) => Retrying(sql, db => db.QueryOptional<A>(sql));

    public static Eff<RT, A> QuerySingle<A>(Sql sql) => Retrying(sql, db => db.QuerySingle<A>(sql));

    public static Eff<RT, int> Execute(Sql sql) => Retrying(sql, db => db.Execute(sql));

    /// <summary>See <see cref="IDatabaseIO.Transaction{A}" />: the body runs only SQL and pure code.</summary>
    public static Eff<RT, A> Transaction<A>(Func<IDatabaseSession, IO<A>> body,
        IsolationLevel isolation = IsolationLevel.ReadCommitted) =>
        Trait.Bind(db => db.Transaction(body, isolation));

    /// <summary>See <see cref="IDatabaseIO.Fold{A, S}" />: a pure step over a streamed result.</summary>
    public static Eff<RT, S> Fold<A, S>(Sql sql, S seed, Func<S, A, S> step) =>
        Trait.Bind(db => db.Fold(sql, seed, step));

    private static Eff<RT, A> Retrying<A>(Sql sql, Func<IDatabaseIO, IO<A>> run) =>
        Trait.Bind(db => sql.IsRead ? run(db).RetryWhile(db.ReadRetry, DatabaseRetry.IsTransient) : run(db));
}

/// <summary>Small combinators the language-ext prelude doesn't have in this shape.</summary>
public static class Effect
{
    /// <summary>An effect that does nothing, for the branch that has no work.</summary>
    public static Eff<RT, Unit> Done<RT>() => Eff<RT, Unit>.Pure(unit);

    /// <summary>Runs <paramref name="effect" /> on the value if there is one; otherwise does nothing.</summary>
    public static Eff<RT, Unit> ForSome<RT, A>(Option<A> option, Func<A, Eff<RT, Unit>> effect) =>
        option.Match(effect, Done<RT>);

    /// <summary>Lifts a validation into an effect: valid continues, invalid fails with every collected error.</summary>
    public static Eff<RT, A> Validated<RT, A>(Validation<Error, A> validation) =>
        validation.Match(Succ: Eff<RT, A>.Pure, Fail: Eff<RT, A>.Fail);

    /// <summary>
    ///     Turns an absent value into an error case. Bind an <c>Option</c> through this, never directly in a
    ///     <c>from</c> clause: language-ext would fail with its own <c>Errors.None</c>, which the boundary treats as a
    ///     defect.
    /// </summary>
    public static Eff<RT, A> Require<RT, A>(Option<A> option, Error whenNone) =>
        option.Match(Eff<RT, A>.Pure, () => Eff<RT, A>.Fail(whenNone));
}
