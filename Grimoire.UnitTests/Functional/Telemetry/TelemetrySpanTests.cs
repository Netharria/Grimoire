// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.PluralKit;
using Grimoire.Features.Shared.PluralKit.Models;
using Grimoire.Functional.Boundary;
using Grimoire.Functional.Data;
using Grimoire.Functional.Effects;
using Grimoire.Functional.Runtime;
using Grimoire.Functional.Telemetry;
using Grimoire.Settings.Enums;
using Grimoire.Structs;
using Grimoire.UnitTests.Functional.Fakes;
using LanguageExt;
using Unit = LanguageExt.Unit;

namespace Grimoire.UnitTests.Functional.Telemetry;

/// <summary>Spec 1.6, section 3.7: which spans, events and counters each part of the runtime records.</summary>
public sealed class TelemetrySpanTests
{
    private static readonly GuildId Guild = new(10);
    private static readonly Sql Read = Sql.Read("Test.Count", "SELECT count(*) FROM x");
    private static readonly Sql Write = Sql.Write("Test.Update", "UPDATE x SET y = 1");

    private readonly TestEnv _env = new();

    private FakeTelemetry Telemetry => this._env.Telemetry;

    private static Error Transient(Sql sql) =>
        DatabaseErrors.Classify(new TimeoutException(), sql, false, "db:5432", CancellationToken.None);

    private Task<Fin<CommandOutcome>> Command(Func<int, Eff<CommandRuntime, Reply>> act) =>
        this._env.Run(CommandBoundary<CommandRuntime>.Run(Invocations.Slash,
            Eff<CommandRuntime, Privacy>.Pure(Privacy.Public), _ => Success<Error, int>(1), act));

    [Theory]
    [InlineData("success", "Success", false)]
    [InlineData("refusal", "Invalid", false)]
    [InlineData("outage", "Unavailable", true)]
    [InlineData("defect", "Defect", true)]
    public async Task ACommand_IsOneRootSpan_WithItsOutcome(string kind, string outcome, bool isError)
    {
        Error? error = kind switch
        {
            "refusal" => new Invalid("no"),
            "outage" => Transient(Write),
            "defect" => Error.New(new InvalidOperationException("bug")),
            _ => null
        };

        await this.Command(_ => error is null
            ? Eff<CommandRuntime, Reply>.Pure(Reply.Of(GrimoireColor.Purple, "done"))
            : Eff<CommandRuntime, Reply>.Fail(error));

        var root = this.Telemetry.Single("command Probe");
        root.Outcome.ShouldBe(outcome);
        root.End.IsError.ShouldBe(isError);
        root.Parent.IsNone.ShouldBeTrue();
        root.Operation.Tags.Find(TelemetryNames.GuildId).ShouldBe(Some((object)"10"));
    }

    [Fact]
    public async Task ACommandsCalls_NestUnderItsRoot()
    {
        this._env.Database.Returns(Read.Name, 3L);

        await this.Command(_ => Db<CommandRuntime>.QuerySingle<long>(Read)
            .Map(_ => Reply.Of(GrimoireColor.Purple, "done")));

        this.Telemetry.Single("db Test.Count").Parent.ShouldBe(Some("command Probe"));
    }

    [Fact]
    public async Task AnOutagesReply_QuotesTheTraceId()
    {
        await this.Command(_ => Eff<CommandRuntime, Reply>.Fail(Transient(Write)));

        this._env.Alerts.Sent.ShouldHaveSingleItem().Message.ShouldContain(FakeTelemetry.Id);
    }

    [Fact]
    public async Task AnEvent_IsOneRootSpan_AndItsAlertCarriesTheId()
    {
        await this._env.Run(EventBoundary<GrimoireRuntime>.Run("ProbeHandler", Guild,
            Eff<GrimoireRuntime, Unit>.Fail(Transient(Write))));

        var root = this.Telemetry.Single("event ProbeHandler");
        root.Outcome.ShouldBe("Unavailable");
        root.End.IsError.ShouldBeTrue();
        this._env.Alerts.Sent.ShouldHaveSingleItem().Message.ShouldContain($"[Id `{FakeTelemetry.Id}`]");
    }

    [Fact]
    public async Task ARead_RetriedTwice_IsOneSpan_WithTwoRetryEvents()
    {
        this._env.Database.Returns(Read.Name, 7L).Fails(Read.Name, Transient(Read), times: 2);

        (await this._env.Run(Db<GrimoireRuntime>.QuerySingle<long>(Read))).ShouldSucceed();

        var span = this.Telemetry.Single("db Test.Count");
        span.Outcome.ShouldBe("Success");
        span.Events.Map(@event => @event.Tags.Find(TelemetryNames.Attempt)).ShouldBe([Some((object)2), Some((object)3)]);
        span.Events.ShouldAllBe(@event => @event.Name == TelemetryNames.RetryEvent);
        this.Telemetry.Counts.Filter(count => count.Counter == Counter.Retries).Count.ShouldBe(2);
    }

    [Fact]
    public async Task AWrite_HasNoRetryEvents()
    {
        this._env.Database.Affects(Write.Name, 1).Fails(Write.Name, Transient(Write), times: 1);

        (await this._env.Run(Db<GrimoireRuntime>.Execute(Write))).ShouldFail();

        var span = this.Telemetry.Single("db Test.Update");
        span.Outcome.ShouldBe("Unavailable");
        span.Events.ShouldBeEmpty();
        this.Telemetry.Counts.ShouldBeEmpty();
    }

    [Fact]
    public async Task ATransaction_IsOneSpan()
    {
        await this._env.Run(Db<GrimoireRuntime>.Transaction(_ => IO.pure(unit)));

        this.Telemetry.Single("db transaction").Outcome.ShouldBe("Success");
    }

    [Fact]
    public async Task ADiscordCall_IsASpanNamedByItsOperation()
    {
        this._env.Discord.Fails("Discord.SendMessage", FakeDiscord.Outage("Discord.SendMessage"), times: 1);

        await this._env.Run(Discord<GrimoireRuntime>.SendMessage(new ChannelId(20), new DiscordMessageBuilder()));

        var span = this.Telemetry.Single("discord Discord.SendMessage");
        span.Outcome.ShouldBe("Unavailable");
        span.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task ADiscordLookup_IsTaggedWithItsGuild()
    {
        await this._env.Run(Discord<GrimoireRuntime>.GetMember(Guild, new UserId(30)));

        var span = this.Telemetry.Single("discord Discord.GetMember");
        span.Outcome.ShouldBe("NotFound");
        span.Operation.Tags.Find(TelemetryNames.GuildId).ShouldBe(Some((object)"10"));
    }

    [Fact]
    public async Task APluralKitLookup_IsLabelledByWhatPluralKitSaid()
    {
        this._env.PluralKit.Knows(new PluralKitMessage("200", "100", new PluralKitMessage.Entity("abcde"),
            new PluralKitMessage.Entity("fghij")));

        await this._env.Run(PluralKit<GrimoireRuntime>.Lookup(new MessageId(100), this._env.Clock.Now));

        this.Telemetry.Single("pluralkit PluralKit.GetMessage").Outcome.ShouldBe(nameof(ProxyLookup.ReplacedByProxy));
    }

    [Fact]
    public async Task APluralKitLookupThatIsNotAsked_HasNoSpan()
    {
        await this._env.Run(PluralKit<GrimoireRuntime>.Lookup(new MessageId(100), this._env.Clock.Now.AddHours(-1)));

        this.Telemetry.Spans.ShouldBeEmpty();
    }

    [Fact]
    public async Task APluralKitOutage_RetriedOnce_HasOneRetryEvent()
    {
        this._env.PluralKit.Fails(FakePluralKitIO.Outage(), times: 1);

        await this._env.Run(PluralKit<GrimoireRuntime>.Lookup(new MessageId(100), this._env.Clock.Now));

        var span = this.Telemetry.Single("pluralkit PluralKit.GetMessage");
        span.Outcome.ShouldBe(nameof(ProxyLookup.NotProxied));
        span.Events.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ARefusedPermission_IsAGuildIssueEvent_AndCounted()
    {
        await this._env.Run(EventBoundary<GrimoireRuntime>.Run("ProbeHandler", Guild,
            Eff<GrimoireRuntime, Unit>.Fail(FakeDiscord.MissingPermission(Guild, "Manage Messages"))));

        var root = this.Telemetry.Single("event ProbeHandler");
        root.Events.ShouldHaveSingleItem().Name.ShouldBe(TelemetryNames.GuildIssueEvent);
        this.Telemetry.Counts.ShouldHaveSingleItem().Counter.ShouldBe(Counter.GuildIssues);
    }

    [Fact]
    public async Task AGuildLogNobodyWouldSee_IsCountedAsSkipped()
    {
        this._env.GuildLog.Logged = false;

        await this._env.Run(GuildLogs<GrimoireRuntime>.Post(
            GuildLogMessage.Text(Guild, GuildLogType.Moderation, this._env.Clock.Now, "text")));

        var (counter, tags) = this.Telemetry.Counts.ShouldHaveSingleItem();
        counter.ShouldBe(Counter.GuildLogSkipped);
        tags.Find(TelemetryNames.GuildLogType).ShouldBe(Some((object)nameof(GuildLogType.Moderation)));
    }
}
