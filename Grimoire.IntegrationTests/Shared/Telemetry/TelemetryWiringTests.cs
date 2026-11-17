// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DSharpPlus.Entities;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Functional.Data;
using Grimoire.Functional.Effects;
using Grimoire.Functional.Runtime;
using Grimoire.Functional.Telemetry;
using Grimoire.Functional.Workers;
using Grimoire.IntegrationTests.FakeDiscord;
using Grimoire.IntegrationTests.Shared.Boundary;
using Grimoire.Settings.Enums;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Unit = LanguageExt.Unit;

namespace Grimoire.IntegrationTests.Shared.Telemetry;

/// <summary>
///     Spec 1.6, section 8: the telemetry wiring inside the real host, exported to in-memory exporters. Other test
///     classes run their own hosts in parallel and also use the "Grimoire" source, so every assertion picks its spans
///     by trace id, never by "the only span".
/// </summary>
[Collection("Test collection")]
public sealed class TelemetryWiringTests(GrimoireCoreFactory database) : IAsyncLifetime
{
    private readonly ConcurrentQueue<Activity> _spans = new();
    private readonly List<Metric> _metrics = [];
    private GrimoireTestBot _bot = null!;

    private FakeGuild Guild => this._bot.Guild;

    public async ValueTask InitializeAsync()
    {
        await database.ResetDatabase();
        // The host's own pool, built by AddGrimoire as production does, so its name is checked too.
        this._bot = await GrimoireTestBot.StartAsync(database,
            unregisteredCommands: [typeof(BoundaryProbeCommands)],
            connectionString: database.ConnectionString,
            configureHost: builder => builder.Services
                .AddSingleton(new TelemetryProbe(() => new GuildId(this.Guild.Id), () => new ChannelId(this.Guild.ModLog.Id)))
                .ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddProcessor(
                    new SimpleActivityExportProcessor(new QueueExporter(this._spans))))
                .ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddInMemoryExporter(this._metrics)));
    }

    public async ValueTask DisposeAsync() => await this._bot.DisposeAsync();

    private Activity[] Trace(ActivityTraceId id) => this._spans.Where(span => span.TraceId == id).ToArray();

    private Activity Root(string name, ActivityTraceId? trace = null) =>
        this._spans.Last(span => span.DisplayName == name && span.ParentSpanId == default
                                                         && (trace is null || span.TraceId == trace));

    [Fact]
    public async Task AProbeCommandsOutage_IsOneTrace_WhoseIdTheReplyQuotes()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("probe_public", this.Guild.User1,
            SlashOption.String("mode", "unavailable"));

        var errorId = reply.Description!.Split('`')[^2];
        errorId.ShouldMatch("^[0-9a-f]{32}$");
        var root = await WaitFor(() => this._spans.FirstOrDefault(span =>
            span.DisplayName == "command probe_public" && span.TraceId.ToHexString() == errorId));
        root.Kind.ShouldBe(ActivityKind.Server);
        root.Status.ShouldBe(ActivityStatusCode.Error);
        root.GetTagItem(TelemetryNames.Outcome).ShouldBe("Unavailable");
        root.GetTagItem(TelemetryNames.GuildId).ShouldBe(this.Guild.Id.ToString());
    }

    [Fact]
    public async Task AWorkerTick_NestsTheItem_TheDbSpan_AndNpgsqlsSpan()
    {
        await this._bot.RunEffectTickAsync<TelemetryProbeWorker>();

        var tick = await WaitFor(() => this._spans.LastOrDefault(span => span.DisplayName == "worker TelemetryProbeWorker"));
        tick.GetTagItem(TelemetryNames.WorkerItems).ShouldBe(1);
        var spans = this.Trace(tick.TraceId);
        var item = spans.Single(span => span.DisplayName == "worker.item");
        item.ParentSpanId.ShouldBe(tick.SpanId);
        var db = spans.Single(span => span.DisplayName == "db Probe.Select");
        db.ParentSpanId.ShouldBe(item.SpanId);
        // Npgsql 10 traces the command, and a connect when the pool has to open one, under the statement's span. (The
        // guild log gate's settings read, still EF, has its own Npgsql span directly under the item.)
        spans.Where(span => span.Source.Name == "Npgsql" && span.ParentSpanId == db.SpanId)
            .Select(span => span.DisplayName)
            .ShouldContain("postgresql", Describe(spans));
    }

    /// <summary>
    ///     DSharpPlus sends the REST call on our flow, so in production HttpClient's span nests under the Discord span
    ///     (spec 1.6, section 4). Checked by the span current when the fake REST received the request.
    /// </summary>
    [Fact]
    public async Task DiscordsRestCall_IsSentFromInsideTheDiscordSpan()
    {
        await this._bot.RunEffectTickAsync<TelemetryProbeWorker>();

        var tick = await WaitFor(() => this._spans.LastOrDefault(span => span.DisplayName == "worker TelemetryProbeWorker"));
        var send = this.Trace(tick.TraceId).Single(span => span.DisplayName == "discord Discord.SendMessage");
        var request = this._bot.Rest.Requests.Last(this._bot.IsModLogPost);
        request.SentFrom.ShouldNotBeNull().SpanId.ShouldBe(send.SpanId);
    }

    /// <summary>The interaction's own calls (defer, the reply) are sent from inside the command's root span.</summary>
    [Fact]
    public async Task AnInteractionsCalls_AreSentFromInsideTheCommandsTrace()
    {
        var reply = await this._bot.RunSlashCommandAndWaitAsync("probe_public", this.Guild.User1,
            SlashOption.String("mode", "unavailable"));
        var errorId = reply.Description!.Split('`')[^2];

        var interactionCalls = this._bot.Rest.Requests
            .Where(request => request.Path.StartsWith("interactions/", StringComparison.Ordinal)
                              || request.Path.StartsWith("webhooks/", StringComparison.Ordinal))
            .ToArray();
        interactionCalls.ShouldNotBeEmpty();
        interactionCalls.ShouldAllBe(request => request.SentFrom != null
                                                && request.SentFrom.Value.TraceId.ToHexString() == errorId);
    }

    [Fact]
    public async Task NpgsqlsSpans_AreNeverRoots()
    {
        // Startup and the guild sync run unported EF queries with no Grimoire span around them.
        await this._bot.RunEffectTickAsync<TelemetryProbeWorker>();
        await WaitFor(() => this._spans.LastOrDefault(span => span.DisplayName == "worker TelemetryProbeWorker"));

        this._spans.Where(span => span.Source.Name is "Npgsql" or RedactingProcessor.HttpSource)
            .ShouldAllBe(span => span.ParentSpanId != default);
    }

    [Fact]
    public async Task AGuildLogPost_IsARootSpan_LinkedToTheSpanThatQueuedIt()
    {
        await this._bot.ConfigureModerationAsync();

        await this._bot.RunEffectTickAsync<TelemetryProbeWorker>();

        var tick = await WaitFor(() => this._spans.LastOrDefault(span => span.DisplayName == "worker TelemetryProbeWorker"));
        var post = await WaitFor(() => this._spans.LastOrDefault(span =>
            span.DisplayName == "guildlog post" && span.Links.Any(link => link.Context.TraceId == tick.TraceId)));
        post.ParentSpanId.ShouldBe(default);
        post.GetTagItem(TelemetryNames.Outcome).ShouldBe("Success");
        post.GetTagItem(TelemetryNames.GuildLogType).ShouldBe(nameof(GuildLogType.Moderation));
    }

    [Fact]
    public async Task TheMeters_AreExported_AndNpgsqlsPoolIsNamedGrimoire()
    {
        await this._bot.RunSlashCommandAndWaitAsync("probe_public", this.Guild.User1, SlashOption.String("mode", "ok"));
        await this._bot.RunEffectTickAsync<TelemetryProbeWorker>();
        await this._bot.Logs.WaitForAsync(line => line.RenderMessage() is var text && text.Contains("probe_public") && text.Contains("finished with"),
            TimeSpan.FromSeconds(10));

        this._bot.Services.GetRequiredService<MeterProvider>().ForceFlush().ShouldBeTrue();

        var names = this._metrics.Select(metric => metric.Name).ToHashSet();
        names.ShouldContain(TelemetryNames.CommandDuration);
        names.ShouldContain(TelemetryNames.CallDuration);
        names.ShouldContain(TelemetryNames.WorkerTickDuration);
        names.ShouldContain(TelemetryNames.GuildLogQueueDepth);
        var poolNames = this._metrics.Where(metric => metric.MeterName == "Npgsql")
            .SelectMany(PointTags)
            .Where(tag => tag.Key == "db.client.connection.pool.name")
            .Select(tag => tag.Value)
            .Distinct()
            .ToArray();
        poolNames.ShouldContain("grimoire");
        poolNames.ShouldAllBe(name => !(name as string ?? "").Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WithoutAnEndpoint_TheStartupLineSaysExportIsOff()
        => (await this._bot.Logs.WaitForAsync(
                line => line.RenderMessage().StartsWith("Telemetry export is off", StringComparison.Ordinal),
                TimeSpan.FromSeconds(10)))
            .ShouldNotBeNull();

    /// <summary>The trace as text, for a failure message.</summary>
    private static string Describe(IEnumerable<Activity> spans) =>
        string.Join("; ", spans.Select(span => $"{span.Source.Name}:{span.DisplayName} ({span.SpanId} <- {span.ParentSpanId})"))
        + $" | all sources seen: {string.Join(",", AllSources)}";

    private static readonly ConcurrentDictionary<string, byte> SeenSources = new();
    private static IEnumerable<string> AllSources => SeenSources.Keys;

    private static IEnumerable<KeyValuePair<string, object?>> PointTags(Metric metric)
    {
        foreach (ref readonly var point in metric.GetMetricPoints())
        foreach (var tag in point.Tags)
            yield return tag;
    }

    private static async Task<T> WaitFor<T>(Func<T?> find) where T : class
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (find() is { } found)
                return found;
            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        throw new TimeoutException("The span never arrived.");
    }

    /// <summary>The in-memory exporter, into a queue, so the test can read while the host still exports.</summary>
    private sealed class QueueExporter(ConcurrentQueue<Activity> spans) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                SeenSources.TryAdd(activity.Source.Name, 0);
                spans.Enqueue(activity);
            }
            return ExportResult.Success;
        }
    }
}

/// <summary>Where the telemetry probe worker posts, read when it runs. Registered by the test.</summary>
internal sealed record TelemetryProbe(Func<GuildId> Guild, Func<ChannelId> ModLog);

/// <summary>
///     One item that reads through Dapper, posts in the mod log through the Discord edge, and queues a guild log: one of
///     each kind of span (spec 1.6, section 8).
/// </summary>
internal sealed class TelemetryProbeWorker(GrimoireEnv env, TelemetryProbe probe)
    : EffectBackgroundService(env, TimeSpan.FromHours(1))
{
    private static readonly Sql Select = Sql.Read("Probe.Select", "SELECT 1");

    protected override Eff<GrimoireRuntime, Unit> Tick() =>
        this.Items.Item("probe", this.Work());

    private Eff<GrimoireRuntime, Unit> Work() =>
        from _ in Db<GrimoireRuntime>.QuerySingle<int>(Select)
        from __ in Discord<GrimoireRuntime>.SendMessage(probe.ModLog(), new DiscordMessageBuilder().WithContent("probe"))
        from now in Clock<GrimoireRuntime>.UtcNow
        from ___ in GuildLogs<GrimoireRuntime>.Post(
            GuildLogMessage.Text(probe.Guild(), GuildLogType.Moderation, now, "telemetry probe"))
        select Unit.Default;
}
