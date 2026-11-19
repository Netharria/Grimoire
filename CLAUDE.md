# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Documentation

[`docs/README.md`](docs/README.md) lists the current source for each topic. Work on the language-ext migration
follows [`docs/langext-migration/plan.md`](docs/langext-migration/plan.md): each section gets a design spec in
`docs/langext-migration/specs/`, reviewed by the owner, before its code.

## Project Overview

Grimoire is a Discord bot written in C# (.NET 10) using the DSharpPlus library. It provides moderation, logging, leveling, custom commands, and community management features for Discord servers.

## Commands

```bash
# Build
dotnet build

# Run (requires appsettings configuration)
dotnet run --project Grimoire

# Unit tests: fast, no Docker. Run after every change.
dotnet test --project Grimoire.UnitTests

# Integration tests: Postgres via Testcontainers and the fake-Discord harness (needs Docker). Run before committing.
dotnet test --project Grimoire.IntegrationTests

# All tests
dotnet test --solution Grimoire.sln

# A specific test
dotnet test --project Grimoire.UnitTests --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Mutation testing (unit tests only; config in stryker-config.json, projects in Grimoire.Mutation.sln, which must list
# every production project too)
dotnet stryker --since:main     # only code changed since main
dotnet stryker                  # everything

# Docker (full stack with PostgreSQL + pgAdmin)
docker-compose up -d

# EF migrations (run from repo root): one chain for both schemas, in Grimoire.Migrations (spec 2.0). The design-time
# host needs ConnectionStrings__Grimoire set, even for commands that don't connect.
dotnet ef migrations add <MigrationName> --context GrimoireMigrationsDbContext --project Grimoire.Migrations --startup-project Grimoire
dotnet ef database update --context GrimoireMigrationsDbContext --project Grimoire.Migrations --startup-project Grimoire
# database update from the command line doesn't use startup's 30-minute timeout: add "Command Timeout=1800" to the
# connection string for a large database (spec 2.1, OQ-4).
```

## Solution Structure

Seven projects in the solution:

- **Grimoire** — Main executable. Discord bot entry point, all slash command handlers, event listeners, and background services.
- **Grimoire.Data** — EF Core infrastructure: `GrimoireDbContext` and its entity configurations (the model is built by
  `GrimoireModel.Configure`, which the migrations context calls too). No migrations: see Grimoire.Migrations. Also the
  Dapper type handlers (`Dapper/DapperTypeHandlers.cs`), which use the domain's internal `FromDatabase` factories.
- **Grimoire.Domain** — Domain models only. Entity definitions, strongly-typed IDs, and the error cases (`Invalid`,
  `NotFound`, `Forbidden`, `Unavailable`, in `Errors.cs`). References `LanguageExt.Core` (pinned, `5.0.0-beta-78`):
  results are `Fin<T>`, input checks `Validation<Error, T>`, absent values `Option<T>`. `Migration/` holds the
  temporary, `[Obsolete]` bridge for `Task<Fin<T>>` chains (spec 1.1). Has `InternalsVisibleTo("Grimoire.Data")` so EF
  value converters can use `internal` factory methods (`FromDatabase`, `ParseFromDatabase`).
- **Grimoire.Settings** — Separate DbContext (`SettingsDbContext`, model built by `SettingsModel.Configure`) for
  per-guild settings, in the `Settings` schema (ids `bigint`). Guild settings are versioned JSON documents, one per
  module plus `Modules` for the on/off flags (`Domain/Documents`, C13); mutes and locks are one state row each
  (`ItemState`, TD-1), mapped from internal rows in `Rows/` (spec 2.2). Its service layer (`SettingsModule`) caches reads through
  `HybridCache`, backed by FusionCache.
- **Grimoire.Migrations** — Migrations only (spec 2.0): `GrimoireMigrationsDbContext`, whose model is both runtime
  models together, holds the one ordered chain for both schemas and the one snapshot. Startup applies it through
  `MigrationRunner`. Never queried; the runtime contexts have no migrations of their own (a unit test checks).
- **Grimoire.UnitTests** — xUnit v3 tests of pure logic: domain types, settings domain, embed builders, decisions. No Docker,
  no network; Testcontainers and Respawn are deliberately not referenced, so a database test here doesn't compile.
- **Grimoire.IntegrationTests** — xUnit v3 tests against real Postgres (Testcontainers, Respawn) and the fake-Discord
  harness (fake gateway and REST around the real host). `Database/` holds SQL/settings tests; the other folders are
  harness tests by feature area.

## Feature Organization

Inside `Grimoire/Features/`, features are organized into modules:

- `CustomCommands/` — User-created text commands
- `Leveling/` — XP system, leaderboards, rewards
- `Logging/` — Message logs, user logs (avatar/username/nickname changes) and their settings
- `Moderation/` — Bans, kicks, mutes, channel locks, warnings, sins, publishing, diagnostics, spam filter
- `LogCleanup/` — Background cleanup of old log entries
- `Shared/` — Cross-cutting concerns: the command error handler, alerts, guild issues, gateway health, PluralKit,
  guild sync and invites, shared commands (modules, purge, user info)

General helpers live outside `Features/`, in `Grimoire/Extensions`, `Grimoire/Utilities` and
`Grimoire/DatabaseQueryHelpers`. The language-ext runtime (capabilities, runtimes, effects, boundaries, workers) is in
`Grimoire/Functional/`, described in [`docs/architecture.md`](docs/architecture.md).

## Key Architectural Patterns

- **DSharpPlus.Commands**: commands are plain classes with `[Command]` methods, gated by check attributes such as
  `[RequireGuild]` and `[RequireModuleEnabled]`. Most commands work both as slash commands and as `!` text commands.
  Event handlers implement `IEventHandler<TEventArgs>`. Grimoire is on a DSharpPlus 5 nightly (`5.0.0-nightly-02614`).
- **EF Core with PostgreSQL**: two runtime DbContexts, `GrimoireDbContext` for main data and `SettingsDbContext` for
  guild settings, and one migrations-only `GrimoireMigrationsDbContext` that migrates both schemas as one chain (spec
  2.0). Change a model's configuration, then add the migration to `Grimoire.Migrations`; `MigrationModelTests` fails
  if the runtime models and the migrations model drift apart. All of them run over the one `NpgsqlDataSource` the host
  registers, which Dapper shares (one connection pool). The language-ext migration (`docs/langext-migration/plan.md`) moves runtime
  queries to Dapper and keeps EF Core for migrations only.
- **Data access (section 1.3)**: ported code reads and writes through `Db<RT>` with `Sql.Read`/`Sql.Write` values
  built by pure functions in the feature's SQL module. Only reads are retried; a transaction's body is `IO`, so no
  Discord call runs inside it. Never catch a constraint exception: write the race into the statement (`ON CONFLICT`).
  Ids are sent as `bigint`; enums are passed as `ToString()`. See `docs/architecture.md` section 5.
- **Boundaries (language-ext migration, section 1.2)**: ported code returns an `Eff` and hands it to a boundary,
  never throwing to DSharpPlus. Commands call `CommandRunner.Run(ctx, input, act)` (privacy from `[PrivateReply]`, or
  a privacy function); event handlers call `EventRunner.Run(name, guild, () => effect)`; workers derive from
  `EffectBackgroundService` and run items through `Items.Item(key, work)`. New code doesn't call `TryDeferAsync` or
  the `ctx.ReplyAsync`/`SendErrorResponseAsync` helpers; those stay for unported commands until Phase 5.
- **Discord calls (section 1.4)**: ported code calls Discord through `Discord<RT>`, never DSharpPlus directly. It
  takes ids and returns snapshot records (`MemberSnapshot`, `ChannelSnapshot`, …), never DSharpPlus entities. Only
  lookups and idempotent calls are retried; anything that creates a message is never retried (Q25). Guild logs are
  posted through `GuildLogs<RT>.Post(guild, type, build)`, which builds only when the log is on, as one
  `GuildLogMessage` record stamped with the event's time. A suspected DSharpPlus bug goes in
  `docs/upstream/dsharpplus.md` as a `DSP-` entry, for the owner to report or fix. See `docs/architecture.md`
  section 6.
- **Strongly-typed IDs**: Domain entities use custom value types for IDs (e.g., `UserId`, `GuildId`) to prevent accidental ID misuse.
- **Hybrid caching in Settings**: guild settings are read through `HybridCache` to avoid a DB lookup on every event.
- **Telemetry (section 1.6)**: OpenTelemetry through `ITelemetryIO`/`Telemetry<RT>`. The boundaries open root
  spans; every `Db`, `Discord` and `PluralKit` call is a child span, with retries as events, so features get spans
  for free and add only counters with bounded tags. Span tags come from typed error fields, never messages, content,
  names or user ids (defect 9); guild ids go on spans, never on metrics. The generic reply's id is the trace id.
  Export is on only with `OTEL_EXPORTER_OTLP_ENDPOINT` (straight to home: App Platform has no sidecars). See
  `docs/architecture.md` section 8 and `docs/operations/telemetry.md`.
- **PluralKit (section 1.5)**: ported code asks PluralKit through `PluralKit<RT>.Lookup`, which returns a `ProxyLookup`
  and asks only for deletes under 60 s old. The live capability queues lookups in a token bucket (5 a second, about
  10 s of queue) instead of refusing them; transient failures are retried in `PluralKit<RT>`, under a 15 s cap. The
  delete log uses `DeletedMessageLookup<RT>`, which never fails: a failed lookup is logged and the delete logged as
  usual. See `docs/architecture.md` section 7.

## Configuration

`Grimoire/appsettings.json` is committed with placeholder values; put real secrets in environment variables or user
secrets rather than in that file. Keys the code reads:

```json
{
  "token": "<Discord bot token>",
  "ConnectionStrings": {
    "Grimoire": "<PostgreSQL connection string>"
  },
  "ConnectionProperties": {
    "Hostname": "...", "Port": "...", "DbName": "...", "Username": "...", "Password": "..."
  },
  "validImageExtensions": "<comma-separated, required: the image embed service throws without it>",
  "channelId": "<urgent alert channel ID, optional>",
  "warningChannelId": "<warning alert channel ID, optional>",
  "warningReportTimeUtc": "<time of the daily warning report, optional>",
  "pluralkitToken": "<optional; PluralKit lookups are off unless all three PluralKit keys are set>",
  "pluralkitUserAgent": "<optional>",
  "pluralkitApiEndpoint": "<optional, an absolute URL such as https://api.pluralkit.me/v2/>",
  "telemetryTraceUrl": "<optional: a Grafana link with {traceId} in it, added to urgent alerts>"
}
```

Telemetry export uses the standard environment variables `OTEL_EXPORTER_OTLP_ENDPOINT` and
`OTEL_EXPORTER_OTLP_HEADERS` (the basic-auth header); without the endpoint nothing is exported.

`ConnectionProperties` is used only when `ConnectionStrings:Grimoire` is empty, and builds a connection string with
SSL required and without `Include Error Detail` (Postgres's `Detail` holds row values; the error logs carry the rest).
A development database can add it to its own `ConnectionStrings:Grimoire`. Serilog is configured from the `Serilog` section. `guildId` may still appear in older settings files, but
nothing reads it.

Environment variables use `__` as separator (e.g., `ConnectionStrings__Grimoire`).

## Code Style

The `.editorconfig` sets strict rules, which the build and IDE analyzers check (there is no CI pipeline yet). Notable
requirements:

- File headers: AGPL-3.0 license boilerplate on every source file
- Nullable reference types enabled; violations are errors
- `this.` qualifier required for instance members. Primary-constructor parameters are used directly: no `this.` and
  no copying them into `readonly` fields
- LF line endings (`.editorconfig` and `.gitattributes`), 4-space indentation
- Class initializers and collection/object initializers preferred over imperative assignment
- `using LanguageExt;` is global in `Grimoire.Domain` and `Grimoire.Settings` only. Elsewhere add it per file: a global
  import makes `HashSet<T>`, `Queryable.Distinct` and `Duration` ambiguous. `LanguageExt.Common` and
  `LanguageExt.Prelude` (static) are global everywhere except the integration tests, where `Prelude.Array` clashes
  with `System.Array`

## Testing

Two projects, split by what a test touches:

- **Unit** (`Grimoire.UnitTests`): logic that runs in-process with nothing outside it. This is where core logic tests
  go, and the only project Stryker mutates against.
- **Integration** (`Grimoire.IntegrationTests`): anything that needs Postgres or the fake-Discord harness. Database
  tests use the shared container fixture (`"Test collection"` or `"Settings collection"`) and reset with Respawn
  between tests. Harness tests start the real host through `GrimoireTestBot`.

Effects are unit-tested against the fake runtime in `Grimoire.UnitTests/Functional/Fakes` (`TestEnv`): the production
runtime records built from recording fakes, each of which can be told to fail. `FakeDatabase` answers by statement
name (`Returns`, `Affects`, `Fails`). `FakeDiscord` is a small in-memory guild (`Has(snapshot)`; anything it doesn't
hold is `NotFound`; `Fails(operation, error)`, with `FakeDiscord.Outage()` and `FakeDiscord.MissingPermission(…)`),
and `FakeGuildIssues` records guild issue reports. `FakeTelemetry` records spans (with their parents), events and
counters in its own lists, not the `Journal`. `FakePluralKitIO` answers for the messages it `Knows` and can
`Fail`; the integration tests' `FakePluralKit` stands behind the real HttpClient. Integration tests drive an `EffectBackgroundService` with
`GrimoireTestBot.RunEffectTickAsync<T>()`. Telemetry tests export the host's spans to an in-memory exporter and pick
them by trace id: other test hosts share the `Grimoire` source. SQL tests run the production `Sql` values through `DatabaseTestEnv`
(`Grimoire.IntegrationTests/Database/Dapper`), which sends every `Sql.Read` to a read-only connection.

Shared helpers (`FinShouldExtensions`, `AlertTestHelpers`) live in the unit project under the `Grimoire.TestShared`
namespace and are linked into the integration project as files. Assert a failure by its case and message:
`result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe(...)`; a settings write with `ShouldBeSaved()` or
`ShouldBeUnchanged()`.

Test code must not write to the console: the bot's logs go to the test output through `TestOutputSink`. Console
writes captured by xunit deadlocked under vstest and Stryker (see `docs/langext-migration/specs/0.4-test-project-split.md`).
