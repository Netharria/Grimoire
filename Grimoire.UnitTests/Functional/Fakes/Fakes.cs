// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Features.Shared.Alerts;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Functional.Capabilities;
using Grimoire.Functional.Runtime;
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
    private readonly Atom<Seq<GuildLogMessageBase>> _sent = Atom(Seq<GuildLogMessageBase>());

    public Seq<GuildLogMessageBase> Sent => this._sent.Value;

    /// <summary>When set, every post fails with this error.</summary>
    public Option<Error> Fails { get; set; }

    public IO<Unit> Send(GuildLogMessageBase message) =>
        this.Fails.Match(IO.fail<Unit>, () => IO.lift(() =>
        {
            journal.Add("guild log");
            this._sent.Swap(sent => sent.Add(message));
            return unit;
        }));
}

public sealed class FakeTrace : ITraceIO
{
    public const string Id = "abc123def0";
    public IO<string> ErrorId => IO.pure(Id);
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
    }

    public Journal Journal { get; } = new();
    public FakeLog Log { get; } = new();
    public FakeAlerts Alerts { get; }
    public FakeGuildLog GuildLog { get; }
    public FakeTrace Trace { get; } = new();
    public FakeCommandIO Command { get; }

    public GrimoireEnv Env => new(this.Clock, this.Log, this.Alerts, this.GuildLog, this.Trace);
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
