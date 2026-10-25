# Automated Integration Test Backlog

The ordered list of everything to cover with fake-Discord integration tests, like
`Grimoire.Tests/Integration/Moderation/WarnTests.cs`. Each item is sized to fit one session. Work them top to bottom:
earlier items build the harness pieces that later ones reuse.

To start a session, say:

> Work on automation item **A-NN** from `docs/testing/automation-backlog.md`.

## Session rules

Every session that works an item follows these steps.

1. **Read only what the item lists**: its source files, its manual cases in the area doc, and the harness files
   (`Grimoire.Tests/Integration/GrimoireTestBot.cs` and `FakeDiscord/`). Don't re-read the whole bot.
2. **Build any harness pieces it names** (the `H-` items below) first, in `Grimoire.Tests/Integration/`. Move each
   one to "Harness: available" when it's done.
3. **Write the tests** in `Grimoire.Tests/Integration/<Area>/<Feature>Tests.cs`. Use the manual case IDs in test
   names or doc comments, so a manual case and its automated test can be traced both ways. Mark the manual case
   `**[auto]**` in its area doc.
4. **Don't fix production bugs in passing.** When a test shows a bug, keep the test, mark it
   `[Fact(Skip = "Fails: <cause>. KI-NN")]`, prove it fails by running it unskipped once, and add a `KI-NN` entry to
   `known-issues.md`. Fixes happen only when asked.
5. **Prove each test can fail.** Break the behaviour once, for example by unregistering the handler or skipping
   the call, and watch the test fail. A test that passes on an empty result is the common trap (see A-19).
6. **Run** the item's tests, then the whole suite: `dotnet test --solution Grimoire.sln`.
7. **Update this file**: tick the item's status, note what was skipped and why, and record any harness changes.

### Test conventions

- **Class shape**: `[Collection("Test collection")]`, with `IAsyncLifetime`. `InitializeAsync` resets the database,
  starts the bot, and configures the modules the item needs. `DisposeAsync` disposes the bot.
- **Waiting for REST calls**: use `bot.Rest.WaitForRequestAsync(predicate)`. It returns a request that already
  happened, or waits for one.
- **Waiting for database writes**: event handlers run concurrently in the background, so poll with
  `GrimoireTestBot.EventuallyAsync`.
- **Checking that something does not happen**: wait over a bounded window, such as
  `ReceivesRequestWithinAsync` or `EventuallyAsync` with a short timeout. Never assert absence right after sending
  an event.
- **End-of-test checks**: assert `bot.Rest.Unhandled.ShouldBeEmpty()` and `bot.Logs.Errors.ShouldBeEmpty()` in the
  main happy-path test, so unexpected calls and logged errors surface.
- **Permissions**: for slash commands, DSharpPlus checks only the bot's own permissions. Discord enforces the
  user's permissions from `default_member_permissions`, so assert that on `bot.Rest.RegisteredCommands` instead of
  sending an interaction from an unprivileged member.
- **Mentions**: a member renders as `<@!id>` (`FakeUser.MemberMention`), and a plain user as `<@id>`.

## Harness

### Available

- **Real host**: `GrimoireTestBot.StartAsync` runs `AddGrimoire`, with only the gateway transport and the REST
  handler faked. Timer-driven background services are removed.
- **Fake guild**: an owner, `mod` (ManageMessages), `user1`, and the bot (Administrator); channels `#general` and
  `#modlog`.
- **Gateway events**: slash commands (`RunSlashCommand` with user and string options), `MESSAGE_CREATE`, the four
  reaction events, and a generic `Gateway.Dispatch(eventName, json)`.
- **REST routes answered**: startup, command registration, interaction callback, original-response edits,
  follow-ups, channel messages, DM channels, `GET channels/{id}`, and guild invites. Everything else returns 404 and
  is recorded in `Unhandled`.
- **Threads and staff**: an `admin` member (Administrator), a `#forum` channel, and public, private and forum-post
  threads sent in `GUILD_CREATE`. `FakeChannel` carries a type and a parent.
- **More REST routes**: message deletes and single-reaction deletes. Every request records its decoded
  `AuditLogReason`.
- **Settings helpers**: `ConfigureModerationAsync` (module on, mod log set to `#modlog`), `ConfigureMessageLogAsync`,
  `SetModuleAsync`, and `LockThreadAsync` and `UnlockThreadAsync`, which write lock rows directly.
- **Log capture**: `bot.Logs` (all events, plus `Errors`).

### To build

Build each piece when the first item that needs it comes up.

| ID | Piece | First needed by |
|----|-------|-----------------|
| H-01 | **REST overrides**: `Rest.On(method, pathPattern, respond)` to return errors or custom bodies for one test, such as 403 on a DM channel or 404 on a member | A-05 |
| H-02 | **More options**: `SlashOption` for integer, boolean, channel, role and number, and subcommand or group options (`type` 1 and 2, nested `options`) | A-01 |
| H-03 | **Guild builder** (the fixed threads, forum and `admin` exist already; per-test changes don't): add members, roles with positions and permissions, channel permission overwrites, threads and forum channels per test, including the `higher`, `admin` and `user2` accounts from `00-environment.md`. It must stay in sync with what `GUILD_CREATE` sends | A-06 |
| H-04 | **Member and guild events**: `GUILD_MEMBER_ADD`, `GUILD_MEMBER_REMOVE`, `GUILD_MEMBER_UPDATE`, `USER_UPDATE`, `GUILD_BAN_ADD`, `GUILD_BAN_REMOVE`, `MESSAGE_UPDATE`, `MESSAGE_DELETE`, `MESSAGE_DELETE_BULK` and `THREAD_CREATE` helpers | A-06 |
| H-05 | **Moderation REST routes**: bans (`PUT`, `DELETE` and `GET guilds/{g}/bans/{u}`), kicks (`DELETE guilds/{g}/members/{u}`), member roles (`PUT` and `DELETE .../members/{u}/roles/{r}`), role create and edit, channel overwrites (`PUT` and `DELETE channels/{c}/permissions/{id}`), message fetch and delete, bulk delete, and `GET guilds/{g}/members/{u}` | A-04, A-06 |
| H-06 | **Audit log**: canned `GET guilds/{g}/audit-logs` entries, so ban and kick events can attribute a moderator | A-06 |
| H-07 | **Background services**: `bot.StartBackgroundService<T>()` starts one of the removed timer services. Seed already-expired rows, start the service, and wait. Its first tick takes up to about 10 s, because of a random 0 to 5 s start delay plus a 5 s period. Making `RunTask` `protected internal` would allow direct ticks, but that is a production change and needs your decision | A-11 |
| H-08 | **Fake time**: replace the `TimeProvider` singleton with `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`, for the services that use it (`AlertService`, `DailyWarningReportService`, `ReactionEvents`) | A-29 |
| H-09 | **Second guild**: a `READY` with two guilds and a second `FakeGuild`, for isolation checks | A-35 |
| H-10 | **Fake PluralKit**: replace the primary handler of the named `HttpClient` "Pluralkit" with a fake that serves canned system and message lookups | A-33 |
| H-11 | **Components**: button and select interactions (`INTERACTION_CREATE` type 3), for pagination such as `/Leaderboard` and `/Commands History` | A-24 |

## Backlog

Status: `[ ]` todo, `[~]` partial, `[x]` done, `[!]` blocked (see notes). Size: S is about half a session, M is one
session, and L may take two.

### Phase 0: Prerequisites

| ID | Status | Item | Notes |
|----|--------|------|-------|
| A-00 | `[x]` | **Fixed the GuildLog double registration** (KI-18). `GuildLog` is now only the queue, and the new `GuildLogPublisher` hosted service posts from it. `Warn_PostsTheWarnEmbedToTheModLog` is unskipped and passes. | Mod-log, user-log and level-log posts can now be asserted directly with `bot.Rest.WaitForRequestAsync`. Set the log channel first, for example with `ConfigureModerationAsync`. |

### Phase 1: General settings and shared checks (area 01)

| ID | Status | Item | Source | Cases | Harness | Size |
|----|--------|------|--------|-------|---------|------|
| A-01 | `[ ]` | `/Modules View` and `/Modules Set`, plus the `RequireModuleEnabled` and `RequireUserGuildPermissions` checks | `Features/Shared/Commands/ModuleCommands*.cs`, `Features/Shared/Attributes/` | GEN-01 to GEN-17, GEN-70 to GEN-89 | H-02 | M |
| A-02 | `[ ]` | `/GeneralSettings` View, ModLogChannel and UserCommands | `Features/Shared/Commands/GeneralSettingsCommands*.cs` | GEN-20 to GEN-60 | H-02 | M |
| A-03 | `[ ]` | `/UserInfo` | `Features/Shared/Commands/UserInfoCommands.cs` | GEN-100 to GEN-121 | H-01 | M |
| A-04 | `[ ]` | `/Purge All` and `/Purge User` | `Features/Shared/Commands/PurgeCommands.cs` | GEN-130 to GEN-158, GEN-170 to GEN-173 | H-05 (message fetch and bulk delete) | M |

### Phase 2: Moderation actions (area 02)

| ID | Status | Item | Source | Cases | Harness | Size |
|----|--------|------|--------|-------|---------|------|
| A-05 | `[~]` | **`/Warn`, remaining cases.** Done: the reply, sin record and mod-log post (MOD-57 core), self-warn, and the registration permission check. Skipped: the DM (KI-19). Still to do: DMs closed (MOD-58), a non-member (MOD-59), the module disabled (MOD-67), and markup in the reason (MOD-68) | `Features/Moderation/Warn/Warn.cs` | MOD-57 to MOD-68 | H-01 | S |
| A-06 | `[ ]` | `/Ban`, `/Unban`, `BanAddedEvent` and `BanRemovedEvent` | `Features/Moderation/Ban/` | MOD-01 to MOD-42 | H-03, H-04, H-05, H-06 | L |
| A-07 | `[ ]` | `/Kick` | `Features/Moderation/Kick/KickUser.cs` | MOD-43 to MOD-56 | H-05 | M |
| A-08 | `[ ]` | `/Mutes` Create, Set, View and Refresh | `Features/Moderation/Mute/Commands/MuteAdminCommands*.cs`, `MuteRoleResolver.cs` | MOD-69 to MOD-89 | H-03, H-05 | M |
| A-09 | `[ ]` | `/Mute` and `/Unmute` | `Features/Moderation/Mute/Commands/MuteUser.cs`, `UnmuteUser.cs` | MOD-90 to MOD-123 | H-03, H-05 | L |
| A-10 | `[ ]` | `/Lock` and `/Unlock`, for channels and threads | `Features/Moderation/Lock/Commands/` | MOD-144 to MOD-158, MOD-169 to MOD-189 | H-03, H-05 | L |
| A-11 | `[ ]` | Mute and lock expiry (`MuteBackgroundTasks`, `LockBackgroundTasks`, `ExpiredItemTriage`), and `UserJoinedWhileMutedEvent` | `Features/Moderation/Mute/`, `Features/Moderation/Lock/LockBackgroundTasks.cs`, `Utilities/ExpiredItemTriage.cs` | MOD-124 to MOD-143 | H-04, H-07 | M |
| A-12 | `[x]` | **Locked-thread enforcement.** Registered `LockedTreadEvents` (KI-20) and added `Integration/Moderation/ThreadLockTests.cs`: deletion of member messages and reactions in public, private and forum threads; staff, admin and bot exemptions; unlocked threads; re-locking. Skipped: MOD-168, because the handler never checks the module (KI-12) | `Features/Moderation/Lock/LockedTreadEvents.cs` | MOD-159 to MOD-168 | built the thread parts of H-03 | S |
| A-13 | `[ ]` | Area 02 cross-checks: several actions on one user, and the mod log turned off | none extra | MOD-190 to MOD-196 | none | S |

A-12 is done. Thread locks work by deletion, not permission overwrites, because threads inherit their parent
channel's permissions. The lock check ignores the end time: a lock lasts until an unlock row is written, either by
`/Unlock` (A-10) or by the lock background task once the timer passes (A-11).

### Phase 3: Sins (area 03; no manual doc yet)

The area 03 doc is still `todo`. Write each case table from the source in the same session, using the case format
in `README.md`, and save it as `03-sins.md`.

| ID | Status | Item | Source | Harness | Size |
|----|--------|------|--------|---------|------|
| A-14 | `[ ]` | `/SinLog`, including the auto-pardon display | `Features/Moderation/SinAdmin/Commands/SinLog.cs` | H-11 if it paginates | M |
| A-15 | `[ ]` | `/Pardon`, `/Forget` and `/Reason`, including the reason history | `Features/Moderation/SinAdmin/Commands/PardonSin.cs`, `ForgetSin.cs`, `UpdateSinReason.cs` | none | M |
| A-16 | `[ ]` | `/ModSettings` View, PublicBanLog and AutoPardon | `Features/Moderation/SinAdmin/Commands/ModSettings*.cs` | H-02 | S |
| A-17 | `[ ]` | `/Publish` Ban, Unban and Kick, and the `/Unban` attribution gap (KI-09) | `Features/Moderation/PublishSins/` | H-05 | M |

### Phase 4: Spam filter and diagnostics (area 04; no manual doc yet)

| ID | Status | Item | Source | Harness | Size |
|----|--------|------|--------|---------|------|
| A-18 | `[ ]` | `/SpamFilter` Override and View, and `SpamEvents` (bursts of `MESSAGE_CREATE`) | `Features/Moderation/SpamFilter/` | H-05 (mute role grant) | M |
| A-19 | `[~]` | **Reaction tracking. Done**: save, soft delete, revive, remove-emoji, clear-all, unlogged messages, and purge with the message, in `Integration/Logging/ReactionTests.cs`. To do: the message log module turned off | `Features/Logging/MessageLogging/ReactionEvents.cs` | none | S |
| A-20 | `[ ]` | `/Diagnostics` Check and Ignore (Add, Remove, View), and the diagnostics follow-up posted by `/Mutes Create` and `/Mutes Refresh` | `Features/Moderation/Diagnostics/` | H-03 | M |

### Phase 5: Logging (area 05; no manual doc yet)

| ID | Status | Item | Source | Harness | Size |
|----|--------|------|--------|---------|------|
| A-21 | `[ ]` | `/Log Message` Set, View, Override and ViewOverrides | `Features/Logging/Settings/LogSettingsCommands.Message*.cs` | H-02 | M |
| A-22 | `[ ]` | Message add, edit, delete and bulk delete, and the message-log posts they cause | `Features/Logging/MessageLogging/` | H-04, H-06 | L |
| A-23 | `[ ]` | `/Log User` Set and View, plus member join and leave, avatar, nickname and username events | `Features/Logging/Settings/LogSettingsCommands.User*.cs`, `Features/Logging/UserLogging/` | H-04 | L |
| A-24 | `[ ]` | `CleanupLogsBackgroundTask`: old-log deletion via REST, retries, and history trimming | `Features/LogCleanup/CleanupLogsBackgroundTask.cs` | H-05, H-07 | M |

### Phase 6: Leveling (area 06; no manual doc yet)

| ID | Status | Item | Source | Harness | Size |
|----|--------|------|--------|---------|------|
| A-25 | `[ ]` | `GainUserXp`: XP gain, cooldown, ignores, reward role grants, and the level-up message | `Features/Leveling/Events/GainUserXp.cs` | H-05 (roles) | L |
| A-26 | `[ ]` | `/Level`, `/Leaderboard` and `LeaderboardRefreshBackgroundTask`. Refresh the materialized view around Respawn resets (see test infra notes) | `Features/Leveling/UserCommands/`, `LeaderboardRefreshBackgroundTask.cs` | H-07, H-11 | M |
| A-27 | `[ ]` | `/LevelSettings` View, Set and LogSet | `Features/Leveling/Settings/LevelSettingsCommandGroup*.cs` | H-02 | M |
| A-28 | `[ ]` | `/Rewards` Add, Remove and View, `/Ignore` Add, Remove and View, and `/Award` and `/Reclaim` | `Features/Leveling/Rewards/`, `Settings/IgnoreCommandGroup*.cs`, `Awards/` | H-02 | L |

### Phase 7: Custom commands (area 07; no manual doc yet)

| ID | Status | Item | Source | Harness | Size |
|----|--------|------|--------|---------|------|
| A-29 | `[ ]` | `/Commands` Learn, Forget, History, Revert, Stats and Leaderboard | `Features/CustomCommands/CustomCommandSettings*.cs` | H-02, H-11 | L |
| A-30 | `[ ]` | `/Command` and `TextCustomCommandProcessor` (prefix text triggers through `MESSAGE_CREATE`) | `Features/CustomCommands/GetCustomCommand.cs`, `TextCustomCommandProcessor.cs` | none | M |

### Phase 8: Alerts and health (area 08; no manual doc yet)

| ID | Status | Item | Source | Harness | Size |
|----|--------|------|--------|---------|------|
| A-31 | `[ ]` | `AlertService`, the warning buffer and flush, and the daily report | `Features/Shared/Alerts/` | H-07, H-08 | M |
| A-32 | `[ ]` | `GuildIssueReporter` and its throttle, `GatewayWatchdogService`, and `GatewayEvents` (disconnects and resumes through the fake transport) | `Features/Shared/GuildIssues/`, `Features/Shared/Gateway/` | H-07, H-08 | M |
| A-33 | `[ ]` | `GuildAdded`, `UpdateAllGuilds`, `InviteEvents` and `MemberAdded` | `Features/Shared/Events/`, `Features/Logging/UserLogging/` | H-04, H-09 | M |

### Phase 9: Cross-cutting (area 09; no manual doc yet)

| ID | Status | Item | Harness | Size |
|----|--------|------|---------|------|
| A-34 | `[ ]` | **PluralKit**: proxied messages in leveling, the message log and the spam filter | H-10 | M |
| A-35 | `[ ]` | **Multi-guild isolation**: settings and data never leak between guilds | H-09 | M |
| A-36 | `[ ]` | **Matrices**: one data-driven theory over every registered command, checking `default_member_permissions` against an expected table and the "module disabled" reply for each module-gated command | H-02 | M |
| A-37 | `[ ]` | **Persistence**: settings cache invalidation, and restart persistence (start two bots one after the other on the same database) | none | S |
| A-38 | `[ ]` | **Startup and migrations**: starting on an empty database, and on a database one migration behind | none | S |

## Progress log

Add one line per session: the date, the item, what changed, and what was skipped.

- 2026-10-25: built the harness spike; A-05 partial, A-19 partial. Found KI-18, KI-19 and KI-20.
- 2026-10-25: A-00 done. Split GuildLog into a queue and a `GuildLogPublisher` hosted service, and unskipped the Warn mod-log test.
- 2026-10-25: A-12 done. Registered `LockedTreadEvents` and added `ThreadLockTests`; MOD-168 skipped (KI-12).
