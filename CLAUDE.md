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

# Run all tests
dotnet test

# Run a single test project
dotnet test Grimoire.Test.Unit

# Run a specific test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Docker (full stack with PostgreSQL + pgAdmin)
docker-compose up -d

# EF migrations (run from Grimoire/ directory)
dotnet ef migrations add <MigrationName> --context GrimoireDbContext
dotnet ef database update
```

## Solution Structure

Four projects in the solution:

- **Grimoire** — Main executable. Discord bot entry point, all slash command handlers, event listeners, background services, and the primary `GrimoireDbContext`.
- **Grimoire.Domain** — Domain models only. EF Core entity definitions, entity configurations, strongly-typed IDs.
- **Grimoire.Settings** — Separate DbContext (`GrimoireSettingsDbContext`) for per-guild settings. Has its own service layer with hybrid caching.
- **Grimoire.Test.Unit** — xUnit tests using Testcontainers (PostgreSQL), Respawn for DB cleanup, NSubstitute for mocking.

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

`Grimoire/appsettings.json` (copy from `appsettings.example.json`). Key fields:

```json
{
  "token": "<Discord bot token>",
  "ConnectionStrings": {
    "Grimoire": "<PostgreSQL connection string>"
  },
  "channelId": "<error log channel ID, optional>",
  "guildId": "<guild for experimental commands, optional>",
  "pluralkitToken": "<optional>",
  "pluralkitEndpoint": "<optional>"
}
```

Environment variables use `__` as separator (e.g., `ConnectionStrings__Grimoire`).

## Code Style

The `.editorconfig` enforces strict rules — CI checks compliance. Notable requirements:

- File headers: AGPL-3.0 license boilerplate on every source file
- Nullable reference types enabled; violations are errors
- `this.` qualifier required for instance members
- CRLF line endings, 4-space indentation
- Class initializers and collection/object initializers preferred over imperative assignment

## Testing

Tests use Testcontainers to spin up a real PostgreSQL instance. Tests that touch the database must use the shared container fixture and call Respawn between tests. NSubstitute is used for mocking non-database dependencies.
