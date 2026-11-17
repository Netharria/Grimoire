// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.Runtime;
using Grimoire.Functional.Telemetry;
using LanguageExt;
using Unit = LanguageExt.Unit;

namespace Grimoire.UnitTests.Functional.Fakes;

// Recording fakes for every capability. Tests build the production runtime records (GrimoireRuntime, CommandRuntime)
// from these, so the code under test is the same code production runs.

/// <summary>A clock that only moves when a test moves it.</summary>
public sealed class FakeClock(DateTimeOffset start) : ITimeIO
{
    private readonly Atom<DateTimeOffset> _now = Atom(start);

    public DateTimeOffset Now => this._now.Value;

    /// <summary>How long each tick takes, as measured by <see cref="ElapsedSince" />.</summary>
    public TimeSpan Elapsed { get; set; } = TimeSpan.FromMilliseconds(10);

    public IO<DateTimeOffset> UtcNow => IO.lift(() => this._now.Value);
    public IO<long> Timestamp => IO.lift(() => this._now.Value.UtcTicks);
    public IO<TimeSpan> ElapsedSince(long timestamp) => IO.lift(() => this.Elapsed);

    public void Advance(TimeSpan by) => this._now.Swap(now => now + by);
}

public sealed class FakeLog : ILogIO
{
    private readonly Atom<Seq<LogEntry>> _entries = Atom(Seq<LogEntry>());

    public Seq<LogEntry> Entries => this._entries.Value;

    public IO<Unit> Write(LogEntry entry) => IO.lift(() =>
    {
        this._entries.Swap(entries => entries.Add(entry));
        return unit;
    });
}

/// <summary>The order of calls across fakes, for tests that check one step comes before another.</summary>
public sealed class Journal
{
    private readonly Atom<Seq<string>> _entries = Atom(Seq<string>());

    public Seq<string> Entries => this._entries.Value;

    public void Add(string entry) => this._entries.Swap(entries => entries.Add(entry));
}

public sealed class FakeAlerts(Journal journal) : IAlertIO
{
    private readonly Atom<Seq<Alert>> _sent = Atom(Seq<Alert>());

    public Seq<Alert> Sent => this._sent.Value;

    public IO<Unit> Send(Alert alert) => IO.lift(() =>
    {
        journal.Add($"alert {alert.Type}");
        this._sent.Swap(sent => sent.Add(alert));
        return unit;
    });
}

public sealed class FakeGuildLog(Journal journal) : IGuildLogIO
{
    private readonly Atom<Seq<GuildLogMessage>> _sent = Atom(Seq<GuildLogMessage>());

    public Seq<GuildLogMessage> Sent => this._sent.Value;

    /// <summary>When set, every post fails with this error.</summary>
    public Option<Error> Fails { get; set; }

    /// <summary>The gate's answer: whether the log's module is on and it has a channel. On by default.</summary>
    public bool Logged { get; set; } = true;

    /// <summary>When set, the gate's settings lookup fails with this error.</summary>
    public Option<Error> GateFails { get; set; }

    public IO<bool> IsLogged(GuildId guild, GuildLogType type) =>
        this.GateFails.Match(IO.fail<bool>, () => IO.lift(() =>
        {
            journal.Add($"guild log gate {type}");
            return this.Logged;
        }));

    public IO<Unit> Enqueue(GuildLogMessage message) =>
        this.Fails.Match(IO.fail<Unit>, () => IO.lift(() =>
        {
            journal.Add("guild log");
            this._sent.Swap(sent => sent.Add(message));
            return unit;
        }));
}

/// <summary>Records each guild issue reported, in the shared journal too.</summary>
public sealed class FakeGuildIssues(Journal journal) : IGuildIssueIO
{
    private readonly Atom<Seq<GuildIssue>> _reported = Atom(Seq<GuildIssue>());

    public Seq<GuildIssue> Reported => this._reported.Value;

    /// <summary>When set, every report fails with this error.</summary>
    public Option<Error> Fails { get; set; }

    public IO<Unit> Report(GuildIssue issue) =>
        this.Fails.Match(IO.fail<Unit>, () => IO.lift(() =>
        {
            journal.Add($"guild issue {issue.Code}");
            this._reported.Swap(reported => reported.Add(issue));
            return unit;
        }));
}

/// <summary>A span the fake recorded: what it was, how it ended, its parent, and its events and late tags.</summary>
public sealed record RecordedSpan(
    Operation Operation,
    SpanEnd End,
    Option<string> Parent,
    Seq<(string Name, Tags Tags)> Events,
    Tags Tagged)
{
    public string Name => this.Operation.SpanName;
    public string Outcome => this.End.Outcome;
}

/// <summary>
///     Records spans, events and counters in its own lists, not the <see cref="Journal" />, so tests that check the
///     order of calls aren't affected. Nesting is tracked with an <c>AsyncLocal</c>, as <c>Activity.Current</c> is.
/// </summary>
public sealed class FakeTelemetry : ITelemetryIO
{
    public const string Id = "abc123def0";

    private readonly AsyncLocal<OpenSpan?> _current = new();
    private readonly Atom<Seq<RecordedSpan>> _spans = Atom(Seq<RecordedSpan>());
    private readonly Atom<Seq<(Counter Counter, Tags Tags)>> _counts = Atom(Seq<(Counter, Tags)>());

    /// <summary>Finished spans, in the order they ended: children before their parent.</summary>
    public Seq<RecordedSpan> Spans => this._spans.Value;

    public Seq<(Counter Counter, Tags Tags)> Counts => this._counts.Value;

    public IO<string> ErrorId => IO.pure(Id);

    public RecordedSpan Single(string name) => this.Spans.Filter(span => span.Name == name).Single();

    public IO<A> Span<A>(Operation operation, IO<A> body, Func<A, SpanEnd> ended) =>
        IO.liftAsync(async env =>
            {
                var parent = this._current.Value;
                var open = new OpenSpan(operation, operation.IsRoot ? null : parent);
                this._current.Value = open;
                var result = await body.Try().Run().RunAsync(env);
                var end = SpanResult.Of(result, env.Token.IsCancellationRequested, ended);
                this._spans.Swap(spans => spans.Add(open.Finish(end)));
                return result;
            })
            .Bind(result => result.Match(Succ: IO.pure, Fail: IO.fail<A>));

    public IO<Unit> Event(string name, Tags tags) =>
        IO.lift(() =>
        {
            this._current.Value?.AddEvent(name, tags);
            return unit;
        });

    public IO<Unit> Tag(Tags tags) =>
        IO.lift(() =>
        {
            this._current.Value?.AddTags(tags);
            return unit;
        });

    public IO<Unit> Count(Counter counter, Tags tags) =>
        IO.lift(() =>
        {
            this._counts.Swap(counts => counts.Add((counter, tags)));
            return unit;
        });

    private sealed class OpenSpan(Operation operation, OpenSpan? parent)
    {
        private readonly Lock _lock = new();
        private Seq<(string, Tags)> _events;
        private Tags _tags = Tags.Empty;

        public void AddEvent(string name, Tags tags)
        {
            lock (this._lock)
                this._events = this._events.Add((name, tags));
        }

        public void AddTags(Tags tags)
        {
            lock (this._lock)
                this._tags = this._tags.Add(tags);
        }

        public RecordedSpan Finish(SpanEnd end)
        {
            lock (this._lock)
                return new RecordedSpan(operation, end, Optional(parent?.Name), this._events, this._tags);
        }

        private string Name => operation.SpanName;
    }
}

/// <summary>One call on <see cref="FakeCommandIO" />, in the order it happened.</summary>
public abstract record CommandCall
{
    public sealed record Deferred(Delivery Delivery) : CommandCall;
    public sealed record Replied(Delivery Delivery, DiscordEmbed Embed, Seq<DiscordEmbed> FollowUps) : CommandCall;
    public sealed record Refused(Delivery Delivery, DiscordEmbed Embed) : CommandCall;
    public sealed record Reacted(string Emoji) : CommandCall;
}

public sealed class FakeCommandIO(Journal journal) : ICommandIO
{
    private readonly Atom<Seq<CommandCall>> _calls = Atom(Seq<CommandCall>());

    public Seq<CommandCall> Calls => this._calls.Value;

    public Option<Error> DeferFails { get; set; }
    public Option<Error> ReplyFails { get; set; }
    public Option<Error> RefuseFails { get; set; }
    public Option<Error> ReactFails { get; set; }

    public IO<Unit> Defer(Delivery delivery) => this.Record(new CommandCall.Deferred(delivery), this.DeferFails);

    public IO<Unit> Reply(Delivery delivery, DiscordEmbed embed, Seq<DiscordEmbed> followUps) =>
        this.Record(new CommandCall.Replied(delivery, embed, followUps), this.ReplyFails);

    public IO<Unit> Refuse(Delivery delivery, DiscordEmbed embed) =>
        this.Record(new CommandCall.Refused(delivery, embed), this.RefuseFails);

    public IO<Unit> React(string emoji) => this.Record(new CommandCall.Reacted(emoji), this.ReactFails);

    /// <summary>A failed call is recorded too, so a test can see it was attempted.</summary>
    private IO<Unit> Record(CommandCall call, Option<Error> fails) =>
        IO.lift(() =>
        {
            journal.Add(call.GetType().Name.ToLowerInvariant());
            this._calls.Swap(calls => calls.Add(call));
            return unit;
        }).Bind(_ => fails.Match(IO.fail<Unit>, () => IO.pure(unit)));
}

/// <summary>A runtime environment built from fakes, with each fake kept for assertions.</summary>
public sealed class TestEnv
{
    public FakeClock Clock { get; } = new(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));
    public TestEnv()
    {
        this.Alerts = new FakeAlerts(this.Journal);
        this.GuildLog = new FakeGuildLog(this.Journal);
        this.Command = new FakeCommandIO(this.Journal);
        this.Database = new FakeDatabase(this.Journal);
        this.Discord = new FakeDiscord(this.Journal);
        this.GuildIssues = new FakeGuildIssues(this.Journal);
        this.PluralKit = new FakePluralKitIO(this.Journal);
    }

    public Journal Journal { get; } = new();
    public FakeLog Log { get; } = new();
    public FakeAlerts Alerts { get; }
    public FakeGuildLog GuildLog { get; }
    public FakeTelemetry Telemetry { get; } = new();
    public FakeCommandIO Command { get; }
    public FakeDatabase Database { get; }
    public FakeDiscord Discord { get; }
    public FakeGuildIssues GuildIssues { get; }
    public FakePluralKitIO PluralKit { get; }

    public GrimoireEnv Env => new(this.Clock, this.Log, this.Alerts, this.GuildLog, this.Telemetry, this.Database,
        this.Discord, this.GuildIssues, this.PluralKit);
    public GrimoireRuntime Runtime => new(this.Env);
    public CommandRuntime CommandRuntime => new(this.Env, this.Command);

    public async Task<Fin<A>> Run<A>(Eff<CommandRuntime, A> effect)
    {
        using var envIO = EnvIO.New();
        return await effect.RunAsync(this.CommandRuntime, envIO);
    }

    public async Task<Fin<A>> Run<A>(Eff<GrimoireRuntime, A> effect)
    {
        using var envIO = EnvIO.New();
        return await effect.RunAsync(this.Runtime, envIO);
    }
}
