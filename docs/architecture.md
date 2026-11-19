# Architecture

The current description of how Grimoire's code is put together: the layers, effects and runtimes, the error model,
the boundaries, and how each is tested. It's kept up to date as the language-ext migration
([`langext-migration/plan.md`](langext-migration/plan.md)) lands. Code that hasn't been ported yet still works the
old way (EF Core queries in command bodies, exceptions to `CommandHandler`); the plan lists which section ports what.

## 1. Layers

```
DSharpPlus ──► shell: [Command] method, IEventHandler, EffectBackgroundService
                 │ copies what the core needs into Grimoire records (Invocation, event records)
                 ▼
               core: records, smart constructors, pure decisions, embed builders
                 │
                 ▼
               Eff<RT, A> pipelines over the capabilities (built, not run)
                 │
                 ▼
               boundary: CommandRunner / EventRunner / the worker's schedule runs it with a runtime
                 │
                 ▼
               capabilities (IO<A>): the only code that touches DSharpPlus, the database, the cache
```

- **Shell.** Thin classes DSharpPlus calls. They copy the arguments into Grimoire records and hand an effect to a
  runner. Discord types a test can't construct (`DiscordMember`, `DiscordGuild`, `DiscordChannel`, `DiscordMessage`)
  stay here (plan C5).
- **Core.** Pure code: records, `Create` smart constructors returning `Validation<Error, T>` when they can fail
  (TD-5), decisions, and embeds. Builder types such as `DiscordEmbedBuilder` are allowed (C5).
- **Effects.** A feature's work is an `Eff<RT, A>`: a description that runs only when a boundary runs it.
- **Capabilities.** One interface per kind of side effect, each method returning `IO<A>`. Live implementations
  are the imperative shell; unit tests use recording fakes.

Code lives in `Grimoire/Functional/`: `Capabilities/`, `Runtime/`, `Effects/`, `Boundary/`, `Workers/` and
`Telemetry/`.

## 2. Effects and runtimes

| Capability | Effect module | What it does | Section |
|---|---|---|---|
| `ITimeIO` | `Clock<RT>` | Wall-clock time and monotonic timestamps | 1.2 |
| `ILogIO` | `Log<RT>` | Structured log lines (message templates, so Serilog keeps the properties) | 1.2 |
| `IAlertIO` | `Alerts<RT>` | Alerts, through `IAlertSender` | 1.2 |
| `IGuildLogIO` | `GuildLogs<RT>` | Guild log posts, through the `GuildLog` queue and its gate (1.4) | 1.2 |
| `ITelemetryIO` | `Telemetry<RT>` | Spans, span events and counters; the error id quoted in the generic reply is the trace id | 1.2, 1.6 |
| `ICommandIO` | `Command<RT>` | Defer, reply, refuse and react, for one invocation | 1.2 |
| `IDatabaseIO` | `Db<RT>` | Dapper over Npgsql | 1.3 |
| `IDiscordIO` | `Discord<RT>` | Discord lookups and actions: ids in, snapshots out, the error cases on failure | 1.4 |
| `IGuildIssueIO` | `GuildIssues<RT>` | Setup problems shown to a guild's moderators, through `GuildIssueReporter` | 1.4 |
| `IPluralKitIO` | `PluralKit<RT>` | PluralKit's message lookup, rate limited, with the error cases on failure | 1.5 |
| `ISettingsIO` | `Settings<RT>` | Typed guild configuration | 3.1 |

Two runtimes hold the capabilities. **`GrimoireRuntime`** is for events and workers. **`CommandRuntime`** adds
`ICommandIO`, so only a command can reply: an event handler that tries doesn't compile. Each runtime implements
`Has<Eff<RT>, Capability>` once per capability, and a function's `where RT : Has<…>` clause lists exactly the effects
it can have. Both are built from one `GrimoireEnv`; `CommandRuntime` is built per invocation.

Helpers in `Effect`: `Done`, `ForSome`, `Validated` (a `Validation` into an effect) and `Require`, which turns an
absent `Option` into an error case. Never bind an `Option` directly in an `Eff` query: language-ext fails it with
`Errors.None`, which the boundary treats as a defect.

## 3. Error model

From spec 1.1, section 3.

- **Results** are `Fin<T>`. **Input checks** are `Validation<Error, T>`, which collects every failure; a command
  converts it once, before any I/O (Q2a). **Absent values** are `Option<T>`, never `null`.
- **An error is its case plus a message** (Q2b). Four cases, all `Expected`, in `Grimoire.Domain/Errors.cs`:

  | Case | Means | Shown to the user | Colour |
  |---|---|---|---|
  | `Invalid` | Bad input, or an action the rules don't allow | its message | red |
  | `NotFound` | The thing asked for doesn't exist | its message | yellow |
  | `Forbidden` | Discord or a permission check refused | its message | red |
  | `Unavailable` | A dependency is down (Q1) | the generic reply only | yellow (generic) |

- **Any other `Error` is a defect**: an exception, `Errors.None`, or a language-ext error that isn't one of the four.
  Timeouts and cancellations that aren't shutdown count as `Unavailable`.
- **Several errors** (a `ManyErrors`) take their worst case: defect, then `Unavailable`, `Forbidden`, `Invalid`,
  `NotFound`. A refusal shows each distinct message on its own line.
- **A settings write that changed nothing** is a success: `Write<T>.Unchanged`.
- **Exceptions** are only for defects and cancellation. An outage becomes `Unavailable` at the one IO edge that
  catches it.

## 4. Boundaries

The boundaries are the only places a failure turns into a reply, a log line or an alert. None of them throws to
DSharpPlus (plan C2): `CommandHandler` and `IClientErrorHandler` only see failures that happen before our code runs
(checks, converters) and failures of code that hasn't been ported yet.

### 4.1 Commands

```csharp
[Command("Warn")]
public Task WarnAsync(CommandContext ctx, DiscordUser user, string reason)
    => runner.Run(ctx, invocation => WarnRequest.Validate(…), WarnUser<CommandRuntime>.Run);
```

1. **Privacy** (TD-11): `[PrivateReply]` on the method, `[PrivateReply(DirectMessageOnText = true)]`, or a privacy
   function passed to the runner when it depends on data.
2. **Delivery**: slash public, slash ephemeral, or for text: public, DM, or a 🔒 reaction and nothing else (C11).
3. **Validate**: pure. A failure is refused at once, with no defer.
4. **Defer** with the delivery.
5. **Act**: the command's `Eff`, returning a `Reply` (an embed, follow-up pages, and an optional guild log post).
6. **Render**: the reply, then the log post; or the refusal; or, for an outage or a defect, a log line and an
   Urgent alert first, then the generic "[ID …]" reply.

| Outcome | Slash | Text |
|---|---|---|
| Success | the deferred response is edited; follow-ups keep its visibility | public reply, or DM |
| Refusal before the defer | ephemeral initial response | public reply, or DM |
| Refusal after the defer | deferred response deleted, ephemeral follow-up (KI-21) | public reply, or DM |
| Outage or defect | Urgent alert, then the generic reply, ephemeral | the same, public or DM |
| Private command run as text | — | 🔒 reaction, nothing runs |
| DM-only command, DMs closed | — | 🔒 reaction |

Text custom commands aren't commands to DSharpPlus. Their handler reacts ❓ for both "not found" and "not allowed",
so a restricted command's existence isn't revealed (Q44, section 3.17).

The runner records each run's outcome, and `CommandHandler`'s "Command … finished with {Outcome}" line reports it.

A `Forbidden` that carries a `BotRefusal` (Discord refused the bot a permission, section 6) also becomes a guild
issue for the moderators, in all three boundaries: one report per distinct issue, throttled to once a day by
`GuildIssueReporter`. A refusal by our own rules, or a member's closed DMs, carries none.

### 4.2 Events

`EventRunner.Run(handlerName, guildId, () => effect)`. `NotFound` is a debug line (the thing was deleted while the
handler ran). `Invalid` and `Forbidden` are warnings and a Warning alert. `Unavailable` and defects are errors and an
Urgent alert.

### 4.3 Workers

A worker derives from `EffectBackgroundService` and returns one `Eff` tick, which runs on `Schedule.spaced`. Each
item goes through `Items.Item(key, work)`, with its own backoff (30 s doubling to 15 minutes). A failed item is
logged, alerted (except `Forbidden`) and backed off, and the next item still runs (F-9). An outage fails the tick
without using up the item's backoff (Q38). Three failed ticks in a row are Urgent, unless the cause is `NotFound`. A
summary line is logged every five minutes. All the state is one immutable `WorkerState` in an `Atom` (C6).

## 5. Data access

From spec 1.3. Runtime code reads and writes Postgres through Dapper; EF Core stays for migrations and the model
snapshot (plan C3). Since spec 2.0, one migrations-only context (`GrimoireMigrationsDbContext`, in
`Grimoire.Migrations`) migrates both schemas as one ordered chain, so a data copy always runs before the change that
depends on it; its model is the two runtime models together, built by the same `Configure` methods. Unported code still
uses the two runtime EF contexts, which share the one `NpgsqlDataSource` with Dapper, so
there is one connection pool.

- **A statement is a value.** `Sql.Read(name, text, parameters)` or `Sql.Write(…)`, built by pure functions in the
  feature's SQL module (`WarnSql`, `NicknameSql`). The name ("Warn.InsertSin") is what the fake database answers by,
  what error logs show, and (from 1.6) the span name. Quote identifiers as EF created them (`"Sins"."GuildId"`).
- **`Db<RT>`** has `Query`, `QueryOptional`, `QuerySingle`, `Execute`, `Transaction` and `Fold`. Bind an absent row
  through `Effect.Require(…, new NotFound(…))`; `QuerySingle` on zero rows is a defect.
- **Only reads are retried**: three times, under a second, and only while the failure is transient. A write,
  including an `INSERT … RETURNING` run through `Query`, is never retried (Q25), nor is anything in a transaction.
- **A transaction's body is `IO`, not `Eff`**: only SQL (through the session) and pure code run inside, so nothing
  holds a transaction open across a Discord call. It commits on success and rolls back on failure, keeping the
  body's error case.
- **Streaming** (`Fold`) runs a pure step over an unbuffered reader. Work that calls Discord per row reads bounded
  pages with `Query` instead, so no reader stays open across it (P-15).
- **Never catch a constraint exception.** Write the race into the statement (`ON CONFLICT DO NOTHING`, a CTE,
  `INSERT … SELECT … WHERE EXISTS`). A violation that still happens is a defect.

**The public schema** (spec 2.3). Every id column of a live table is `bigint` (P-9), stored through `long`
converters by convention (`StronglyTypedIdConventions`). The legacy tables from `main` are outside the EF model and keep
`numeric(20,0)` until P-7 drops them; no foreign key joins them to a live table.

- **Pardons** (Q8): one `Pardons` row per sin, keyed by `SinId`, holding who first pardoned it and when. Neither
  changes. The reason is in `PardonReasonHistory` (`SinId`, `SetAt`, `Reason`, `ModeratorId`): a re-pardon adds a row,
  and the latest is the reason shown. `/Pardon` is one statement (a CTE that inserts the pardon only if it is missing,
  then the reason), so two at once leave one pardon and two reasons.
- **Discriminators are readable strings** (Q9): `XpHistory."Type"` (`Earned`, `Awarded`, `Reclaimed`, `Migrated`) and
  `MessageHistory."Discriminator"` (`Created`, `Edited`, `Deleted`, `DeletedByModerator`).
- **`ServerDisplayNameHistory`** (TD-9; was `NicknameHistory`) holds a member's name on a server over time. Until 3.11
  it records nicknames, and `null` means the nickname was cleared.
- **`leaderboard_view`** is a materialized view refreshed every 15 minutes (Q12). It ranks members with more than 0 XP
  (Q12b), breaks ties by `UserId` (defect 23, enforced by a unique `(GuildId, Rank)` index), and carries each member's
  latest server display name and username (TD-9) for 3.15 to render.

**The Settings schema** (spec 2.2). Its ids are `bigint` (P-9).

- **Guild settings are versioned JSON documents** (TD-6, C13): `"Settings"."GuildSettings"` holds one row per
  `(GuildId, Document, Version)`, with a `jsonb` value and who/when. There are six documents: `Modules`, which holds
  all six on/off flags, and `General`, `Leveling`, `MessageLog`, `Moderation` and `UserLog`, which each hold only that
  module's settings. The latest version is in force. A missing document or field is the default, and the
  default isn't written. The records and their encoding are in `Grimoire.Settings/Domain/Documents`
  (`SettingsJson`): snowflakes are strings, durations whole seconds. A write applies a pure change to the latest
  version and inserts the next one only if that is still the latest. A writer that loses the race re-reads and
  re-applies, up to three times. A change that leaves the document equal stores nothing (`Write.Unchanged`). Each
  document is cached under its own key, so the module gate reads only `Modules`, and a disabled module's document is
  never loaded.
- **Mutes, channel locks and thread locks are one row each** (TD-1, TD-2), whose `State` only moves forward:
  `Pending` → `Active`, and `Pending` or `Active` → `Expired` or `Removed`. A check constraint requires exactly the
  fields each state needs. A partial unique index allows one row in force per member or channel. The state is the
  `ItemState` union in C#, mapped from a flat row (`Grimoire.Settings/Rows`). Each change is an `UPDATE … WHERE
  "State" IN (…)`, so a race with the expiry poll updates no row and reads as `NotFound`. Until 3.4 and 3.5 add the
  processors, the commands act first and record `Active`.

**Errors** (`DatabaseErrors.Classify`, the one place a database exception becomes a value):

| Failure | Becomes |
|---|---|
| The run's token cancelled (shutdown) | `Errors.Cancelled` |
| Connection refused or lost, a timeout, the pool exhausted, Postgres classes `08` and `53`, `57P01`–`57P03`, `57014` | `Unavailable`, transient (a read retries) |
| `40001` serialization, `40P01` deadlock | `Unavailable`, not retried |
| Constraint (`23xxx`), syntax or unknown column (`42xxx`), data (`22xxx`), a mapping error, anything else | defect |

Either way the error keeps a `DatabaseException`. Its message, which the alert shows, names the statement, whether it
was a read or a write and in a transaction, the SQLSTATE and its name, Postgres's message, and the schema, table,
column, constraint and type. Its `ToString()`, which the log writes, adds the hint, position, context, routine and the
parameter names, then the Npgsql exception. Neither holds a parameter value or Postgres's `Detail`, which is why the
built connection string has no `Include Error Detail`. A connection failure names the host and port and the socket
error, never the connection string.

**Type handlers** (`Grimoire.Data/Dapper/DapperTypeHandlers.cs`, registered by `AddEffectRuntime`):

- Every id is sent and read as `bigint`, which every live column is since spec 2.3 (P-9). `ReadSnowflake` still
  accepts a `numeric` value, for a legacy table read by raw SQL; nothing does so today.
- The text value types go through their `FromDatabase` (or `Create`), `DateTimeOffset` is UTC, and `Option<T>` of
  each is `NULL` for `None`. A unit test checks by reflection that every stored domain type has a handler.
- `Jsonb<T>` documents use System.Text.Json (`RegisterDocument<T>(options)`): unknown fields are ignored, missing
  ones take the record's defaults, and one that doesn't parse is a defect.
- **Not through handlers.** Enums: pass `ToString()`. Dapper sends the number, which an insert into a `text` column
  stores without complaint and a comparison refuses. `ModerationActor`: read the column as `long?` and map it with
  `DatabaseColumns.Actor`. Arrays: `= ANY(@ids)` takes `DatabaseColumns.Ids(…)`. Rows: read into a private row
  record next to the SQL and map it to the domain type with a pure function.

**Tests.** Unit tests run pipelines against `FakeDatabase` (in `TestEnv`), which answers by statement name, records
every call in the `Journal` (with `begin`, `commit` and `rollback` around a transaction), and can fail a statement a
given number of times. SQL tests live in `Grimoire.IntegrationTests/Database/`, one class per SQL module
(`WarnSqlTests` for `WarnSql`). They build the production `Sql` value and run it through `DatabaseTestEnv`, the live
capability over the test container. `DatabaseTestEnv` sends every `Sql.Read` to a read-only connection, so a
statement marked as a read that writes fails its test.

## 6. Discord

From spec 1.4. Ported code calls Discord through `Discord<RT>`, never DSharpPlus directly.

- **Ids in, snapshots out.** `IDiscordIO` takes ids and returns Grimoire records (`ChannelSnapshot`,
  `UserSnapshot`, `MemberSnapshot`, `RoleSnapshot`, `MessageSnapshot`), because DSharpPlus's entities can't be built
  in a test (plan C5). Each snapshot's `From` is the one place its entity is read. The live capability resolves ids
  with DSharpPlus's own lookups, which return its cached entity and ask REST only on a miss; Grimoire keeps no cache of
  Discord entities.
- **Only lookups and idempotent calls are retried** (Q25): three times, under 4 s, while the failure is transient.
  New messages, follow-ups, DMs, log posts and initial interaction responses are never retried, because a 502 may
  come after Discord already did it. A delete that finds the message gone counts as done. `Discord<RT>` makes the
  choice per call. Unported code uses `DiscordRetryPolicy.RetryDiscordRead` for reads and edits only.
- **Adding an operation:** a method on `IDiscordIO` taking ids; in `LiveDiscordIO`, a `DiscordOperation` (name,
  action, target, guild, channel, the permission it needs) and the call through `Call`/`Attempt`, with no
  try/catch; in `Discord<RT>`, retried or not; the same in `FakeDiscord`; integration tests for success, 404,
  403/50013 and 500.

**Errors** (`DiscordErrors.Classify`, the one place a Discord exception becomes a value). `DiscordFailure.Read` reads
the exception once (the status, which DSharpPlus keeps on `UnauthorizedException` for both 401 and 403, and the JSON
`code` and top-level `message`); the decision is a pure function of that.

| Failure | Becomes |
|---|---|
| The run's token cancelled | `Errors.Cancelled` |
| 404 | `NotFound` ("That member no longer exists."). On the guild lookup itself, also 403 50001: "The server is no longer available to me." (provisional, spec 1.4 OQ-9) |
| 403 50013 Missing Permissions, 50001 Missing Access | `Forbidden` naming the permission (and the role order for member and role actions), with a `BotRefusal` when the call has a guild |
| 403 50007 | `Forbidden`: closed DMs, no refusal |
| 401 | defect: the token is bad |
| 400, 405, 413, a bulk delete of old messages | `Invalid`, quoting only Discord's top-level message, never its `errors` (which can echo content) |
| 5xx, a timeout, a lost connection | `Unavailable`, transient |
| A rate limit DSharpPlus couldn't absorb; a `NullReferenceException` inside DSharpPlus's REST client (KI-54) | `Unavailable`, not retried |
| Anything else | defect |

Each `Unavailable` keeps a `DiscordCallException` naming the operation and the status, for the log and the alert.

**Guild logs.** A post is one `GuildLogMessage` record (Q26): guild, type, `At` (when the event happened, never when
it's posted) and the message, built by `Text`, `Embed` or `Of`, which stamp any embed without a timestamp with `At`.
`GuildLogs<RT>.Post(guild, type, build)` is the only way ported code posts one: it asks the gate in `GuildLog`
(module on and a channel set, both cache hits) and runs `build` only then, on the caller, never in the publisher's
single reader (Q26b). The publisher checks the setting again, posts once through the Discord edge, and reports a
deleted channel or a refused post as a guild issue. Unported code's `GuildLogMessageBase` classes are converted when
queued, stamped with the time then.

Problems found in DSharpPlus itself go in [`upstream/dsharpplus.md`](upstream/dsharpplus.md), for the owner to report
or fix. Grimoire contains them at this edge and never reaches into DSharpPlus's internals.

**Tests.** `FakeDiscord` (in `TestEnv`) is a small in-memory guild: a lookup of something it doesn't hold is
`NotFound`, every call is in the `Journal` as `discord Name`, and an operation can be told to fail by name
(`FakeDiscord.Outage()` for a transient one, `FakeDiscord.MissingPermission(…)` for a refusal). `FakeGuildIssues`
records reports. The live edge's tests (`Grimoire.IntegrationTests/Shared/Discord/`) run it against the fake REST.

## 7. PluralKit

From spec 1.5. Ported code asks PluralKit about a deleted message through `PluralKit<RT>.Lookup(message, createdAt)`.

- **What it answers** is a `ProxyLookup`: `NotAsked` (PluralKit is off, or the message is over 60 s old),
  `NotProxied` (PluralKit's 404 with code 20006), `ReplacedByProxy(link)` (PluralKit deleted this original; the
  `ProxiedMessageLink` is already built), `ReplacedByProxyOfDeletedMember` (the same, but PluralKit sent no system or
  member because the member was deleted: nothing is logged or linked, Q37) and `IsTheProxy`. PluralKit's strings are
  parsed once, in `PluralKitMessage.Classify`.
- **The rate limit.** PluralKit publishes 10 lookups a second; the live capability keeps to 5 with a token bucket (one
  token every 200 ms, a burst of 2, so never more than 7 in a second) and queues up to 50 lookups, about 10 s, instead
  of refusing them (Q24). Past that the lookup fails at once.
- **Retry and cap.** `PluralKit<RT>` retries an outage or a 429 twice (about 0.5 and 1 s), each attempt waiting its
  turn in the bucket, and gives up after 15 s in all. Each HTTP attempt has 5 s.
- **Errors** (`PluralKitErrors`, the one place an answer or exception becomes a value). Every failure is
  `Unavailable` carrying a `PluralKitCallException` with its `PluralKitProblem`, except a defect:

| Answer | Problem | Retried | The delete log's fallback logs it as |
|---|---|---|---|
| 5xx, a timeout, a lost connection | `Outage` | yes | Information |
| 429 | `RateLimited` | yes | Warning |
| The bucket's queue is full | `QueueFull` | no | Warning |
| Past the 15 s cap | `TooSlow` | no | Information |
| 401, 403, 400, a 404 without code 20006 | `Refused` | no | Warning, and a Warning alert |
| A body that isn't the message, or is about another message | `Unreadable` | no | Warning |
| Anything else | `Unexpected` (a defect) | no | Error, and a Warning alert |

- **The delete log never loses a delete to PluralKit** (I-3): `DeletedMessageLookup<RT>.Run` logs a failure as above
  and answers `NotProxied`. Only cancellation gets through.
- **Configuration.** `PluralKitOptions.From` reads `pluralkitApiEndpoint`, `pluralkitUserAgent` and `pluralkitToken`
  when the capability is first resolved. With any missing, the capability is `DisabledPluralKitIO` and one warning
  names them (KI-101).

**Tests.** `FakePluralKitIO` (in `TestEnv`) answers for the messages it `Knows` and can fail. The live capability's
tests (`Grimoire.IntegrationTests/Shared/PluralKit/`) run it, with its HttpClient and bucket, against `FakePluralKit`.

## 8. Telemetry

From spec 1.6. OpenTelemetry traces, metrics and logs, exported over OTLP/HTTP from the bot's own process to
`otel-lgtm` on the owner's home server. Setup and use: [`operations/telemetry.md`](operations/telemetry.md).

- **One capability, `ITelemetryIO`.** `Telemetry<RT>.Span(operation, effect)` runs the whole effect, with its
  runtime, inside a span (`MapIO`), so the span is `Activity.Current` for everything the effect awaits: retries,
  timeouts and forks included. Npgsql's and `HttpClient`'s own spans nest under it. A span can't be started by one
  `IO` and stopped by another, because `Activity.Current` is an `AsyncLocal`.
- **Where spans open.** Root spans: `CommandBoundary.Run` (`command {name}`), `EventBoundary.Run`
  (`event {handler}`), each worker tick (`worker {task}`, in both worker bases) and each guild log post
  (`guildlog post`, linked to the span that queued it). Children: every `Db<RT>` call (`db {Sql.Name}`,
  `db transaction`), every `Discord<RT>` call (`discord Discord.{Method}`), `PluralKit<RT>.Lookup` when it asks
  (`pluralkit PluralKit.GetMessage`) and each worker item (`worker.item`). Features get these for free; a feature adds
  only counters of its own events, with bounded tags.
- **Retries** are `retry` events on the call's one span, one per retry, and `grimoire.call.retries`.
- **Outcome and status** (`SpanResult`, pure): `grimoire.outcome` is the `CommandOutcome` name (or a value's own
  label, such as the `ProxyLookup` case). Only `Unavailable` and defects set status error; a refusal is not a fault.
- **Tags** (`TelemetryTags`, pure) come from typed fields only: the SQLSTATE, table and constraint of a
  `DatabaseException`; the status and code of a `DiscordCallException`; the problem and status of a
  `PluralKitCallException`; the permission of a `BotRefusal`. **Never** a message, SQL text built by us, a parameter
  value, message content, a name or a user id (defect 9). Guild ids go on spans, never on metrics.
- **Sampling** (`GrimoireSampler`): a span with no parent is kept only if it's one of our roots (`grimoire.root`).
  So unported EF queries and DSharpPlus's own REST calls don't become one-span traces. Tail sampling at home drops
  successful worker ticks that did nothing.
- **Redaction**: Discord puts the interaction token in the URL path; `RedactingProcessor` replaces it with
  `{token}` on every `HttpClient` span before export.
- **The error id** is the root's trace id (32 hex characters), in the generic reply, the log line and the alert.
  Outside any span it's 10 random hex characters, as before.
- **Metrics** (meter `Grimoire`, seconds): `grimoire.command.duration` (recorded by `CommandHandler` for every
  command, ported or not, from Discord's timestamp), `grimoire.event.duration`, `grimoire.worker.tick.duration`,
  `grimoire.call.duration` (by dependency, operation and outcome), and counters for retries, worker items, guild log
  posts and skips, guild issues, alerts and PluralKit's full queue; gauges for the guild log queue, the PluralKit
  queue and the gateway. Built in: `System.Runtime`, `Npgsql` (pool named `grimoire`), `System.Net.Http`,
  FusionCache.
- **Logs** carry the trace id (Serilog reads `Activity.Current`), on the console and over OTLP.
- **Export** is on only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. The exporters send from background threads with
  bounded queues and drop the newest data when full; they never block the bot. `ExportHealth` logs the SDK's
  export problems at most once a minute.

**Tests.** `FakeTelemetry` (in `TestEnv`) records spans, their parents, events, late tags and counters in its own
lists, not the `Journal`. `LiveTelemetryIOTests` run the live capability against a real `ActivityListener` and
`MeterListener` on a source named for the test. `TelemetryWiringTests` (integration) export the real host's spans and
metrics to in-memory exporters and pick their spans by trace id, since other hosts share the `Grimoire` source.

## 9. Tests

| Project | Holds | May touch |
|---|---|---|
| `Grimoire.UnitTests` | Smart constructors, pure decisions, embed builders, and whole `Eff` pipelines run against the fake runtime (`Functional/Fakes`): outages, refusals, defects, ordering | Nothing outside the process |
| `Grimoire.IntegrationTests` | SQL against Postgres, migrations, DSharpPlus wiring, event dispatch, PluralKit/HTTP, and one end-to-end smoke test per command and handler (C7) | Docker, the fake Discord harness |

The fake runtime is the production `GrimoireRuntime` and `CommandRuntime` built from recording fakes (`TestEnv`),
`FakeTelemetry` among them.
Each fake can be told to fail with a given error, and a shared `Journal` records the order of calls across them.
Stryker runs against the unit tests only.
