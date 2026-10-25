# Grimoire Manual / Integration Test Plan

Master index. The plan is split into one document per feature area so each can be written,
and later worked through, in its own session without loading the whole bot into context.

## How to use this plan

1. Do `00-environment.md` once (test guild, roles, accounts, DB reset, log capture).
2. Work through area docs in the order below (earlier areas are prerequisites for later ones).
3. In each doc, tick the checkbox per case and record failures in the **Findings** section at the bottom.
4. Cases tagged **[auto]** are candidates for a real automated test (Testcontainers / NSubstitute);
   everything else needs a live Discord guild. Promote [auto] cases to tests rather than re-running by hand.

## Case format (every area doc uses this)

`ID | Precondition | Action | Expected Discord result | Expected DB / log result`

IDs are `<AREA>-<NN>`, e.g. `MOD-07`. Each command gets: happy path, each option, permission
denied, module disabled, bad input, bot-lacks-permission, and target-is-higher-role cases.

## Area documents

| # | File | Scope | Status |
|---|------|-------|--------|
| 00 | `00-environment.md` | Test guild, accounts, reset, observability | drafted |
| 01 | `01-general-settings.md` | `/GeneralSettings` (View, ModLogChannel, UserCommands), `/Modules` (View, Set), `/UserInfo`, `/Purge` (All, User), `RequireModuleEnabled` / `RequireUserGuildPermissions` checks | drafted |
| 02 | `02-moderation-actions.md` | `/Ban`, `/Unban`, `/Kick`, `/Warn`, `/Mute`, `/Unmute`, `/Lock`, `/Unlock`; `/Mutes` (Create, Set, View, Refresh); Ban added/removed events; `UserJoinedWhileMuted`; `MuteBackgroundTasks` and `LockBackgroundTasks` expiry | drafted |
| 03 | `03-sins.md` | `/SinLog`, `/Pardon`, `/Forget`, `/Reason`, `/ModSettings` (View, PublicBanLog, AutoPardon), `/Publish` (Ban, Unban, Kick); the sin records created by area 02's Ban/Kick/Warn/Mute (viewing, pardoning, reason history) and the `/Unban` attribution gap (`known-issues.md` KI-09) | todo |
| 04 | `04-spamfilter-diagnostics.md` | `/SpamFilter` (Override, View), `SpamEvents`; `/Diagnostics` (Check, Ignore Add/Remove/View); the diagnostics follow-up report posted by `/Mutes Create` and `/Mutes Refresh` (embed contents, `moderation-diagnostics.txt`, ignores; area 02 only checks that it appears) | todo |
| 05 | `05-logging.md` | `/Log Message` (Set, View, Override, ViewOverrides), `/Log User` (Set, View); add/edit/delete/bulk-delete message events; avatar/nickname/username/join/leave events; `CleanupLogsBackgroundTask` | todo |
| 06 | `06-leveling.md` | `GainUserXp`, `/Level`, `/Leaderboard`, `/LevelSettings` (View, Set, LogSet), `/Rewards`, `/Ignore`, `/Award`, `/Reclaim`; `LeaderboardRefreshBackgroundTask` | todo |
| 07 | `07-custom-commands.md` | `/Command`, `/Commands` (Learn, Forget, History, Revert, Stats, Leaderboard); `TextCustomCommandProcessor` (prefix text triggers) | todo |
| 08 | `08-alerts-health.md` | `AlertService`, warning buffer/flush/daily report, `GuildIssueReporter` + throttle, `GatewayWatchdogService`, `GuildLog`, `GuildAdded`, `InviteEvents`, `MemberAdded`, `UpdateAllGuilds` | todo |
| 09 | `09-cross-cutting.md` | Startup / migrations, PluralKit (proxied messages in leveling, logging, spam), permission matrix, module-toggle matrix, settings cache invalidation, restart persistence | todo |

## Known issues

Bugs and design gaps found while writing the area docs, to fix later, are collected in `known-issues.md`
(IDs `KI-NN`, each pointing at the cases that confirm it). Add to it when a new area doc turns something up.
Behaviour that is intentional (for example the legacy ManageMessages gates on Ban/Mute/Lock) is documented in the
area doc instead and is not listed there.

## Suggested session workflow

Each area doc is produced by a fresh session that (a) reads only that area's source folder plus
`00-environment.md`, (b) writes the cases, and (c) marks the table above `drafted`. Executing
the tests is a separate pass. Suggested prompt:

> Write `docs/testing/0N-<name>.md` per the case format in `docs/testing/README.md`. Read only
> the source folders listed for that area. Include a "Code observations" section for anything
> that looks like a bug you notice while reading.

## Existing automated coverage (avoid duplicating by hand)

| Project | Covers |
|---------|--------|
| `Grimoire.Settings.Tests` | Settings services, guild settings, message log, moderation settings, module state, domain |
| `Grimoire.Tests` | Alerts, CustomCommands, Gateway, GuildIssues, Leveling (awards, events, user commands), Diagnostics, Mute |
| `Grimoire.Tests.CustomCommands` | Custom command logic |
| `Grimoire.Domain.Test.Unit` | Domain types / strongly-typed IDs |

Not covered by any automated test (highest manual priority): Ban/Kick/Warn/Lock, sins admin,
spam filter, message and user logging, Purge, publish commands, UserInfo, all Discord-facing
permission checks, and every background service's real-time behaviour.
