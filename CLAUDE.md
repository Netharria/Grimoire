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

# EF migrations (run from repo root)
dotnet ef migrations add <MigrationName> --context GrimoireDbContext --project Grimoire.Data --startup-project Grimoire
dotnet ef database update --project Grimoire.Data --startup-project Grimoire
```

## Solution Structure

Six projects in the solution:

- **Grimoire** — Main executable. Discord bot entry point, all slash command handlers, event listeners, and background services.
- **Grimoire.Data** — EF Core infrastructure: `GrimoireDbContext`, entity configurations, and migrations.
- **Grimoire.Domain** — Domain models only. Entity definitions, strongly-typed IDs, and the error cases (`Invalid`,
  `NotFound`, `Forbidden`, `Unavailable`, in `Errors.cs`). References `LanguageExt.Core` (pinned, `5.0.0-beta-78`):
  results are `Fin<T>`, input checks `Validation<Error, T>`, absent values `Option<T>`. `Migration/` holds the
  temporary, `[Obsolete]` bridge for `Task<Fin<T>>` chains (spec 1.1). Has `InternalsVisibleTo("Grimoire.Data")` so EF
  value converters can use `internal` factory methods (`FromDatabase`, `ParseFromDatabase`).
- **Grimoire.Settings** — Separate DbContext (`SettingsDbContext`) for per-guild settings. Its service layer
  (`SettingsModule`) caches reads through `HybridCache`, backed by FusionCache.
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
`Grimoire/DatabaseQueryHelpers`.

## Key Architectural Patterns

- **DSharpPlus.Commands**: commands are plain classes with `[Command]` methods, gated by check attributes such as
  `[RequireGuild]` and `[RequireModuleEnabled]`. Most commands work both as slash commands and as `!` text commands.
  Event handlers implement `IEventHandler<TEventArgs>`. Grimoire is on a DSharpPlus 5 nightly (`5.0.0-nightly-02614`).
- **EF Core with PostgreSQL**: two DbContexts, `GrimoireDbContext` for main data and `SettingsDbContext` for guild
  settings. Both use code-first migrations. The language-ext migration (`docs/langext-migration/plan.md`) moves runtime
  queries to Dapper and keeps EF Core for migrations only.
- **Strongly-typed IDs**: Domain entities use custom value types for IDs (e.g., `UserId`, `GuildId`) to prevent accidental ID misuse.
- **Hybrid caching in Settings**: guild settings are read through `HybridCache` to avoid a DB lookup on every event.
- **PluralKit integration**: Resilience pipeline with rate limiting for the external PluralKit API.

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
  "pluralkitToken": "<optional>",
  "pluralkitUserAgent": "<optional; PluralKit lookups are off unless this and the token are set>",
  "pluralkitApiEndpoint": "<optional>"
}
```

`ConnectionProperties` is used only when `ConnectionStrings:Grimoire` is empty, and builds a connection string with
SSL required. Serilog is configured from the `Serilog` section. `guildId` may still appear in older settings files, but
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

Shared helpers (`FinShouldExtensions`, `AlertTestHelpers`) live in the unit project under the `Grimoire.TestShared`
namespace and are linked into the integration project as files. Assert a failure by its case and message:
`result.ShouldFail().ShouldBeOfType<Invalid>().Message.ShouldBe(...)`; a settings write with `ShouldBeSaved()` or
`ShouldBeUnchanged()`.

Test code must not write to the console: the bot's logs go to the test output through `TestOutputSink`. Console
writes captured by xunit deadlocked under vstest and Stryker (see `docs/langext-migration/specs/0.4-test-project-split.md`).
