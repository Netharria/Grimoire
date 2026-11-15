# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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
- **Grimoire.Domain** — Domain models only. Entity definitions and strongly-typed IDs. Has `InternalsVisibleTo("Grimoire.Data")` so EF value converters can use `internal` factory methods (`FromDatabase`, `ParseFromDatabase`).
- **Grimoire.Settings** — Separate DbContext (`GrimoireSettingsDbContext`) for per-guild settings. Has its own service layer with hybrid caching.
- **Grimoire.UnitTests** — xUnit v3 tests of pure logic: domain types, settings domain, embed builders, decisions. No Docker,
  no network; Testcontainers and Respawn are deliberately not referenced, so a database test here doesn't compile.
- **Grimoire.IntegrationTests** — xUnit v3 tests against real Postgres (Testcontainers, Respawn) and the fake-Discord
  harness (fake gateway and REST around the real host). `Database/` holds SQL/settings tests; the other folders are
  harness tests by feature area.

## Feature Organization

Inside `Grimoire/`, features are organized into modules under subfolders:

- `CustomCommands/` — User-created text commands
- `Leveling/` — XP system, leaderboards, rewards
- `Logging/` — Message logs, user logs (avatar/username/nickname changes), tracker
- `Moderation/` — Bans, mutes, channel locks, warnings, sin tracking, spam filter
- `LogCleanup/` — Background cleanup of old log entries
- `Shared/` — Cross-cutting concerns (PluralKit integration, utility extensions)

## Key Architectural Patterns

- **DSharpPlus slash commands**: Command modules extend `ApplicationCommandModule`. Event listeners implement DSharpPlus event handler interfaces.
- **EF Core with PostgreSQL**: Two DbContexts — `GrimoireDbContext` for main data, `GrimoireSettingsDbContext` for guild settings. Both use code-first migrations.
- **Strongly-typed IDs**: Domain entities use custom value types for IDs (e.g., `UserId`, `GuildId`) to prevent accidental ID misuse.
- **Hybrid caching in Settings**: Guild settings use `IHybridCache` to avoid repeated DB lookups on every event.
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
- `this.` qualifier required for instance members
- CRLF line endings, 4-space indentation
- Class initializers and collection/object initializers preferred over imperative assignment

## Testing

Two projects, split by what a test touches:

- **Unit** (`Grimoire.UnitTests`): logic that runs in-process with nothing outside it. This is where core logic tests
  go, and the only project Stryker mutates against.
- **Integration** (`Grimoire.IntegrationTests`): anything that needs Postgres or the fake-Discord harness. Database
  tests use the shared container fixture (`"Test collection"` or `"Settings collection"`) and reset with Respawn
  between tests. Harness tests start the real host through `GrimoireTestBot`.

Shared helpers (`ResultShouldExtensions`, `AlertTestHelpers`) live in the unit project under the `Grimoire.TestShared`
namespace and are linked into the integration project as files.

Test code must not write to the console: the bot's logs go to the test output through `TestOutputSink`. Console
writes captured by xunit deadlocked under vstest and Stryker (see `docs/langext-migration/specs/0.4-test-project-split.md`).
