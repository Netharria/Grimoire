// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Grimoire.Functional.Boundary;
using Grimoire.Functional.Data;
using LanguageExt;

namespace Grimoire.Functional.Telemetry;

// What a span is about, as data. Pure: the live capability turns these into activities and measurements
// (spec 1.6, section 3.1). Nothing here may hold content, a person's name, SQL text or a user id (defect 9).

public enum OperationKind
{
    Command,
    Event,
    WorkerTick,
    WorkerItem,
    GuildLogPost,
    Database,
    Discord,
    PluralKit,
    Settings
}

/// <summary>Tag names and values. Values are ids, numbers, enums or fixed labels: never free text.</summary>
public sealed record Tags(Seq<(string Name, object Value)> Items)
{
    public static readonly Tags Empty = new(Seq<(string, object)>());

    /// <summary>The tags whose value isn't null, in order.</summary>
    public static Tags Of(params (string Name, object? Value)[] tags) =>
        new(toSeq(tags.Where(tag => tag.Value is not null).Select(tag => (tag.Name, tag.Value!))));

    public Tags Add(Tags more) => new(this.Items + more.Items);

    public Tags Add(string name, object? value) => value is null ? this : new Tags(this.Items.Add((name, value)));

    /// <summary>The value of a tag, for tests and the sampler.</summary>
    public Option<object> Find(string name) =>
        this.Items.Find(tag => tag.Name == name).Map(tag => tag.Value);
}

/// <summary>What one span is about. Built by the factories, which fix its name, kind and first tags.</summary>
public sealed record Operation(OperationKind Kind, string Name, Tags Tags)
{
    /// <summary>
    ///     A span this one follows from without being its child: a guild log post links to the span that queued it.
    /// </summary>
    public Option<ActivityContext> LinkedTo { get; init; }

    /// <summary>"command warn", "db Warn.InsertSin". Bounded: never an id or free text.</summary>
    public string SpanName => this.Kind switch
    {
        OperationKind.Command => $"command {this.Name}",
        OperationKind.Event => $"event {this.Name}",
        OperationKind.WorkerTick => $"worker {this.Name}",
        OperationKind.WorkerItem => "worker.item",
        OperationKind.GuildLogPost => "guildlog post",
        OperationKind.Database => $"db {this.Name}",
        OperationKind.Discord => $"discord {this.Name}",
        OperationKind.PluralKit => $"pluralkit {this.Name}",
        OperationKind.Settings => $"settings {this.Name}",
        _ => this.Name
    };

    /// <summary>A boundary's span: it starts a trace, and the sampler keeps it without a parent.</summary>
    public bool IsRoot => this.Kind is OperationKind.Command or OperationKind.Event or OperationKind.WorkerTick
        or OperationKind.GuildLogPost;

    public ActivityKind ActivityKind => this.Kind switch
    {
        OperationKind.Command => ActivityKind.Server,
        OperationKind.Event or OperationKind.GuildLogPost => ActivityKind.Consumer,
        OperationKind.WorkerTick or OperationKind.WorkerItem => ActivityKind.Internal,
        _ => ActivityKind.Client
    };

    /// <summary>The dependency a call goes to, for <c>grimoire.call.duration</c>. None for boundaries and items.</summary>
    public Option<string> Dependency => this.Kind switch
    {
        OperationKind.Database => "db",
        OperationKind.Discord => "discord",
        OperationKind.PluralKit => "pluralkit",
        OperationKind.Settings => "settings",
        _ => None
    };

    /// <summary>The tags the span starts with: the operation's own, plus <c>grimoire.root</c> on a boundary's.</summary>
    public Tags StartTags => this.IsRoot ? this.Tags.Add(TelemetryNames.Root, true) : this.Tags;

    public static Operation Command(Invocation invocation, TimeSpan queueDelay) =>
        new(OperationKind.Command, invocation.Command, Tags.Of(
            (TelemetryNames.Command, invocation.Command),
            (TelemetryNames.Invocation, invocation.Kind is InvocationKind.Slash ? "slash" : "text"),
            (TelemetryNames.GuildId, GuildOf(invocation.Guild)),
            (TelemetryNames.QueueDelayMs, (long)Math.Max(0, queueDelay.TotalMilliseconds))));

    public static Operation Event(string handler, Option<GuildId> guild) =>
        new(OperationKind.Event, handler, Tags.Of(
            (TelemetryNames.Handler, handler),
            (TelemetryNames.GuildId, GuildOf(guild))));

    public static Operation WorkerTick(string worker) =>
        new(OperationKind.WorkerTick, worker, Tags.Of((TelemetryNames.Worker, worker)));

    /// <summary>The key goes in a tag, not the name, so span names stay bounded.</summary>
    public static Operation WorkerItem(string worker, string key) =>
        new(OperationKind.WorkerItem, worker, Tags.Of(
            (TelemetryNames.Worker, worker),
            (TelemetryNames.WorkerItem, key)));

    public static Operation GuildLogPost(GuildId guild, string logType) =>
        new(OperationKind.GuildLogPost, "post", Tags.Of(
            (TelemetryNames.GuildId, guild.Value.ToString()),
            (TelemetryNames.GuildLogType, logType)));

    public static Operation Database(Sql sql) =>
        new(OperationKind.Database, sql.Name, Tags.Of(
            (TelemetryNames.DbSystem, "postgresql"),
            (TelemetryNames.SqlRead, sql.IsRead)));

    public static Operation DatabaseTransaction(System.Data.IsolationLevel isolation) =>
        new(OperationKind.Database, "transaction", Tags.Of(
            (TelemetryNames.DbSystem, "postgresql"),
            (TelemetryNames.DbIsolation, isolation.ToString())));

    public static Operation Discord(string operation, Option<GuildId> guild = default) =>
        new(OperationKind.Discord, operation, Tags.Of((TelemetryNames.GuildId, GuildOf(guild))));

    public static Operation PluralKit() => new(OperationKind.PluralKit, "PluralKit.GetMessage", Tags.Empty);

    /// <summary>Ids as text: an exporter would otherwise send a ulong past long's range as a double.</summary>
    private static string? GuildOf(Option<GuildId> guild) => guild.Map(id => id.Value.ToString()).OrNull();
}

/// <summary>
///     The counters <see cref="Capabilities.ITelemetryIO.Count" /> adds to. <c>grimoire.worker.items</c> and
///     <c>grimoire.guildlog.posts</c> aren't here: the item's and the post's span counts them when it ends.
/// </summary>
public enum Counter
{
    /// <summary><c>grimoire.call.retries</c>: a failed attempt that's about to be retried.</summary>
    Retries,

    /// <summary><c>grimoire.guildlog.skipped</c>: the gate said no one would see the log.</summary>
    GuildLogSkipped,

    /// <summary><c>grimoire.guild_issues</c>: a guild issue reported for a refusal.</summary>
    GuildIssues
}

/// <summary>Every span, tag and instrument name, in one place (spec 1.6, section 7).</summary>
public static class TelemetryNames
{
    /// <summary>The <c>ActivitySource</c> and <c>Meter</c> name.</summary>
    public const string Source = "Grimoire";

    // Tags, OpenTelemetry's names where a convention exists.
    public const string Root = "grimoire.root";
    public const string Outcome = "grimoire.outcome";
    public const string ErrorType = "error.type";
    public const string Command = "grimoire.command";
    public const string Invocation = "grimoire.invocation";
    public const string GuildId = "grimoire.guild.id";
    public const string QueueDelayMs = "grimoire.command.queue_delay_ms";
    public const string Handler = "grimoire.handler";
    public const string Worker = "grimoire.worker";
    public const string WorkerItem = "grimoire.worker.item";
    public const string WorkerItems = "grimoire.worker.items";
    public const string GuildLogType = "grimoire.guildlog.type";
    public const string DbSystem = "db.system.name";
    public const string SqlRead = "grimoire.sql.read";
    public const string DbIsolation = "grimoire.db.isolation";
    public const string DbStatus = "db.response.status_code";
    public const string DbTable = "db.collection.name";
    public const string DbConstraint = "grimoire.db.constraint";
    public const string HttpStatus = "http.response.status_code";
    public const string DiscordCode = "grimoire.discord.code";
    public const string PluralKitProblem = "grimoire.pluralkit.problem";
    public const string PluralKitQueueWaitMs = "grimoire.pluralkit.queue_wait_ms";
    public const string Transient = "grimoire.transient";
    public const string Permission = "grimoire.permission";
    public const string Dependency = "grimoire.dependency";
    public const string OperationName = "grimoire.operation";
    public const string Attempt = "attempt";
    public const string IssueCode = "grimoire.issue.code";
    public const string AlertSeverity = "grimoire.alert.severity";
    public const string AlertType = "grimoire.alert.type";
    public const string AlertDropped = "grimoire.alert.dropped";

    // Span events.
    public const string RetryEvent = "retry";
    public const string GuildIssueEvent = "guild-issue";

    // Instruments.
    public const string CommandDuration = "grimoire.command.duration";
    public const string EventDuration = "grimoire.event.duration";
    public const string WorkerTickDuration = "grimoire.worker.tick.duration";
    public const string CallDuration = "grimoire.call.duration";
    public const string Retries = "grimoire.call.retries";
    public const string WorkerItemsCounter = "grimoire.worker.items";
    public const string GuildLogSkipped = "grimoire.guildlog.skipped";
    public const string GuildLogPosts = "grimoire.guildlog.posts";
    public const string GuildIssues = "grimoire.guild_issues";
    public const string GuildLogQueueDepth = "grimoire.guildlog.queue.depth";
    public const string PluralKitQueueDepth = "grimoire.pluralkit.queue.depth";
    public const string PluralKitQueueWait = "grimoire.pluralkit.queue.wait";
    public const string PluralKitQueueFull = "grimoire.pluralkit.queue.full";
    public const string Alerts = "grimoire.alerts";
    public const string GatewayDisconnected = "grimoire.gateway.disconnected";

    /// <summary>Duration buckets in seconds, 5 ms to 30 s (spec 1.6, section 3.10).</summary>
    public static readonly double[] DurationBuckets = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10, 30];
}
