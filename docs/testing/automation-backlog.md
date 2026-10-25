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
- **All option types**: `SlashOption` has user, string, integer, `Enum<T>`, boolean, channel and role options, plus
  `Subcommand` and `Group` nesting. Use the registered snake_case names, such as `general_settings` and
  `mod_log_channel`. Enum options send the command enum's numeric value. Some commands declare their own enum whose
  numbers differ from the settings enum, so use the command's.
- **Replies**: `bot.RunSlashCommandAndWaitAsync(...)` returns a `FakeReply` holding the message the member sees,
  `Description`, `IsEphemeral`, and `WasDeferred`. It handles immediate replies, such as check failures, and
  deferred-then-edited replies alike.
- **Mod log**: `bot.WaitForModLogEmbedAsync(match)` and `bot.IsModLogPost`. The mod log is gated on the always-on
  General module, not on Moderation.
- **The bot's own posts**: messages the bot posts to a channel are kept in that channel's history.
  `Rest.PostsIn(channel)` lists them, `GET` and `PATCH channels/{c}/messages/{m}` read and edit them (404 code 10008
  if missing), and `Rest.DeleteMessageByHand` removes one. Posting to a guild channel the bot can't send in returns
  403.
- **Bans and the audit log**: the fake refuses bans without Ban Members, or of anyone ranked at or above the bot
  (403, code 50013). Otherwise it records the ban, writes an audit entry carrying the request's reason, and sends
  `GUILD_BAN_ADD`; unban sends `GUILD_BAN_REMOVE`, or 404 code 10026 if not banned. `bot.BanFromClient` and
  `UnbanFromClient` act as a moderator using Discord's own client. `Rest.MarkBanned` bans silently, as if before the
  bot joined. `GET audit-logs` returns 403 without View Audit Log. `Rest.Banned` lists current bans.
- **Ranks**: a `higher` member sits above the bot, and `Guild.HierarchyOf` follows Discord's rules. A
  `BannerRoleId` role holds only Ban Members.
- **Restarting with narrower permissions**: tests that restart the bot call `ConfigureModerationAsync` again. Settings
  helpers accept "not modified" (`ShouldBeSetOrUnchanged`), because the value is already stored.
- **Roles and overwrites**: `Rest.CreateRole(name)` and `Rest.DeleteRole(id)` act as a moderator in Discord's
  client, sending `GUILD_ROLE_CREATE` and `GUILD_ROLE_DELETE`. `POST guilds/{g}/roles` needs Manage Roles.
  `PUT channels/{c}/permissions/{id}` needs Manage Roles and a channel the bot can see, stores the overwrite, and sends
  `CHANNEL_UPDATE`. Read results with `Guild.OverwriteOf(channelId, targetId)`. `#private` denies the bot's role View
  Channel and Send Messages. There are also `Category`, `Voice`, `Stage` and `Announcements` channels.
- **Member roles**: `PUT` and `DELETE guilds/{g}/members/{u}/roles/{r}` need Manage Roles and a role below the bot's
  top role (403 otherwise), update the member's roles, and send `GUILD_MEMBER_UPDATE`. `Rest.CreateRole(name, perms,
  FakeGuild.RoleAboveTheBot)` makes a role the bot can't manage. `Guild.GrantRole` and `RevokeRole` change roles
  directly, as a moderator would by hand.
- **DMs**: a member's DM channel id is their user id plus 1000. Close a member's DMs with
  `Rest.On(HttpMethod.Post, $"channels/{id + 1000}/messages", _ => FakeDiscordRest.Error(Forbidden, 50007, ...))`.
- **Config**: the test host supplies `validImageExtensions`, which the user-log handlers need.
- **Members**: `GET guilds/{g}/members/{u}` answers for members and returns 404 code 10007 otherwise.
- **Kicks**: `DELETE guilds/{g}/members/{u}` refuses without Kick Members, or for anyone ranked at or above the bot,
  and otherwise sends `GUILD_MEMBER_REMOVE`. `Rest.Kicked` lists kicked members, and `KickerRoleId` holds only Kick
  Members.
- **Unregistered commands**: `StartAsync(database, unregisteredCommands: [typeof(...)])` adds a command class that
  production forgot to register, so its logic can still be tested. Cite the KI that tracks the missing registration.
- **Background services**: `bot.RunBackgroundTickAsync<T>()` runs one tick now, through the protected `RunTask` by
  reflection, reusing one instance so retry backoff carries over. `bot.StartBackgroundServiceAsync<T>()` starts the
  real timer, so allow about twice its interval. Backoff uses the real clock, so retries after 30 seconds stay manual.
- **Membership and channels**: `bot.MemberLeaves(user)` and `MemberJoins(user)` send `GUILD_MEMBER_REMOVE` and
  `GUILD_MEMBER_ADD`; rejoining clears roles. `bot.SetRolesByHand(user, roles)` sends `GUILD_MEMBER_UPDATE`.
  `bot.DeleteChannel(channel)` sends `CHANNEL_DELETE` or `THREAD_DELETE`, and `GET channels/{id}` then answers 404
  code 10003.
- **Own database**: `StartAsync(database, connectionString: ...)` points the bot at another database, for example one
  made with `CREATE DATABASE` in the shared container. Drop it afterwards with `WITH (FORCE)` after
  `NpgsqlConnection.ClearAllPools()`.
- **Second guild**: `StartAsync(secondGuild: true)`, then use `bot.SecondGuild!` channels with the usual helpers
  (`RunSlashCommandAndWaitAsync(channel, ...)`, `SendMessage`). `ConfigureModerationAsync(guild)` and
  `SetModuleAsync(module, enabled, guild)` take the guild. `IsModLogPost` only matches the first guild's mod log.
  Legacy `dbContext.Guilds` and `dbContext.Mutes` are obsolete tables; mutes live in the settings database.
- **PluralKit**: set `pluralkitToken` and point the "Pluralkit" HttpClient at a `FakePluralKit` through
  `configureHost`. `AddProxy(original, proxy, sender, guild, systemId, memberId)` makes `GET messages/{id}` answer for
  either id. A `SendMessage` author who isn't a guild member is sent as a webhook, with no member and a `webhook_id`.
  The bot only asks PluralKit about messages under a minute old, judged by the time-based snowflake.
- **Invites**: add `FakeInvite`s to `guild.Invites` in `configureGuild`; `GET guilds/{id}/invites` returns them.
  `bot.MemberJoinsWithInvite(user, invite)` bumps the use count, drops a used-up single-use invite, and sends
  `GUILD_MEMBER_ADD`.
- **Gateway lifecycle**: `bot.Gateway.RequestReconnect()` sends op 7, and the bot resumes (op 6, then RESUMED).
  `CloseFromServer(code)` delivers a close frame. With `HoldReconnects` set, a reconnecting bot gets no hello and
  stays down. `Connects` counts websocket connections.
- **Host hook**: `GrimoireTestBot.StartAsync(database, configureHost: builder => ...)` runs after `AddGrimoire`, so
  a test can add configuration (such as `channelId` and `warningChannelId`) or replace services (such as
  `TimeProvider` with the unit tests' `ManualTimeProvider`).
- **Member edits**: `PATCH guilds/{id}/members/{id}` applies a new `roles` list, with the same hierarchy check as
  the single-role routes (403 when any changed role is at or above the bot's top role), and sends
  `GUILD_MEMBER_UPDATE`. Other fields are ignored.
- **Profile changes and images**: `bot.UpdateProfile(user, nickname:, username:, avatarHash:)` sends
  `GUILD_MEMBER_UPDATE`. The bot's "Default" HttpClient goes to `FakeCdn`, which answers every GET with a 1-pixel
  PNG and records `bot.Cdn.Downloads`. Fake members start without a nickname, and the startup sync writes an
  empty-nickname row; seed a newer row to stand in for an existing nickname. Username history newer than two
  seconds is ignored by the handler, so wait that long after startup.
- **Message events**: `bot.EditMessage`, `DeleteMessage` and `BulkDeleteMessages` send `MESSAGE_UPDATE`,
  `MESSAGE_DELETE` and `MESSAGE_DELETE_BULK`. `Rest.AddAuditLogEntry` takes optional `options`, such as `channel_id`
  and `count` for a message-delete entry.
- **Mentionable options and files**: `SlashOption.MentionableUser` and `MentionableRole` (type 9) send resolved users
  or roles. Files uploaded with a request are in `RecordedRequest.Files`, by file name.
- **REST overrides**: `Rest.On(method, pathPattern, respond)` answers matching requests your way, with the newest
  override winning. `FakeDiscordRest.Error(status, code, message)` builds a Discord error body, for example 403 with
  code 50007 for a closed DM.
- **Channel history**: `Rest.SeedHistory(channel, (author, at)...)` seeds earlier messages, with ids minted from
  their times. `GET channels/{id}/messages` pages that history newest first by `before` and `limit`, and single and
  bulk deletes remove from it. `Rest.HistoryIds(channel)` shows what's left. `RecordedRequest.QueryValue` reads the
  query string.
- **Channels and bot permissions**: `RunSlashCommand(channel, ...)` and `RunSlashCommandAndWaitAsync(channel, ...)`
  run a command from any channel, which is what "Current Channel" options read. Channel options are sent in the
  interaction's resolved data. `StartAsync(database, guild => guild.BotPermissions = ...)` starts a bot without
  Administrator, which ignores overwrites. `#private` denies the bot's role Send Messages.
- **Changing roles**: `Guild.SetRoles(user, roleIds...)` changes a member's roles from then on, and interactions
  carry the new roles. A `ManagerRoleId` role with only ManageGuild is available.
- **Settings database**: `database.CreateSettingsDbContext()`; Grimoire.Settings internals are visible to the tests.
  Setting a module to its current state stores no new row.
- **Settings helpers**: `ConfigureModerationAsync` (module on, mod log set to `#modlog`), `ConfigureMessageLogAsync`,
  `SetModuleAsync`, and `LockThreadAsync` and `UnlockThreadAsync`, which write lock rows directly.
- **Log capture**: `bot.Logs` (all events, plus `Errors`).

### To build

Build each piece when the first item that needs it comes up.

| ID | Piece | First needed by |
|----|-------|-----------------|
| H-03 | **Guild builder** (the fixed threads, forum and `admin` exist already; per-test changes don't): add members, roles with positions and permissions, channel permission overwrites, threads and forum channels per test, including the `higher`, `admin` and `user2` accounts from `00-environment.md`. It must stay in sync with what `GUILD_CREATE` sends | A-06 |
| H-04 | **Member and guild events**: `GUILD_MEMBER_ADD`, `GUILD_MEMBER_REMOVE`, `GUILD_MEMBER_UPDATE`, `USER_UPDATE`, `GUILD_BAN_ADD`, `GUILD_BAN_REMOVE`, `MESSAGE_UPDATE`, `MESSAGE_DELETE`, `MESSAGE_DELETE_BULK` and `THREAD_CREATE` helpers | A-06 |
| H-05 | **Moderation REST routes**: bans (`PUT`, `DELETE` and `GET guilds/{g}/bans/{u}`), kicks (`DELETE guilds/{g}/members/{u}`), member roles (`PUT` and `DELETE .../members/{u}/roles/{r}`), role create and edit, channel overwrites (`PUT` and `DELETE channels/{c}/permissions/{id}`), message fetch and delete, bulk delete, and `GET guilds/{g}/members/{u}` | A-04, A-06 |
| H-08 | **Fake time**: replace the `TimeProvider` singleton with `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`, for the services that use it (`AlertService`, `DailyWarningReportService`, `ReactionEvents`) | A-29 |
| H-09 | **Second guild** (done): `StartAsync(secondGuild: true)` adds `bot.SecondGuild`, a `FakeGuild(idOffset)` with its own guild, channel and role ids and the same users. READY lists both guilds. Helpers act in whichever guild owns the channel they're given, and the fake REST picks the guild from each request's path | A-35 |
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
| A-01 | `[x]` | `/Modules View` and `/Modules Set`, plus the `RequireModuleEnabled` and `RequireUserGuildPermissions` checks. Tests: `Integration/Shared/CommandChecksTests.cs`, `ModuleCommandsTests.cs` | `Features/Shared/Commands/ModuleCommands*.cs`, `Features/Shared/Attributes/` | GEN-01 to GEN-17, GEN-70 to GEN-89 (multi-guild and restart cases deferred to A-35 and A-37) | built H-02 | M |
| A-02 | `[x]` | `/GeneralSettings` View, ModLogChannel and UserCommands. Tests: `Integration/Shared/GeneralSettingsCommandsTests.cs`. Skipped: GEN-32 wording (KI-22), GEN-37 ephemeral (KI-21). GEN-56 to GEN-59 moved to A-26 | `Features/Shared/Commands/GeneralSettingsCommands*.cs` | GEN-20 to GEN-60 | channel options, narrower bot permissions | M |
| A-03 | `[x]` | `/UserInfo`. Tests: `Integration/Shared/UserInfoCommandTests.cs`. Found KI-23 (throws with User Log on), and KI-19 also affects this command. 5 skipped | `Features/Shared/Commands/UserInfoCommands.cs` | GEN-100 to GEN-121 | seeds history, XP, rewards, ignores and sins directly | M |
| A-04 | `[x]` | `/Purge All` and `/Purge User`. Tests: `Integration/Shared/PurgeCommandsTests.cs`. Found KI-24 (never more than 100 messages); 2 skipped | `Features/Shared/Commands/PurgeCommands.cs`, `Extensions/DiscordChannelExtensions.cs` | GEN-130 to GEN-158 (GEN-170 to 173 moved to A-37) | built channel history and bulk delete (part of H-05) | M |

### Phase 2: Moderation actions (area 02)

| ID | Status | Item | Source | Cases | Harness | Size |
|----|--------|------|--------|-------|---------|------|
| A-05 | `[x]` | **`/Warn`.** All automatable cases are in `Integration/Moderation/WarnTests.cs`. Skipped: the DM in MOD-57 and the closed-DM path in MOD-58, both blocked by KI-19. The MOD-58 test already uses `Rest.On` to fail the DM, so it runs once KI-19 is fixed | `Features/Moderation/Warn/Warn.cs` | MOD-57 to MOD-68 | built H-01 | S |
| A-06 | `[x]` | `/Ban`, `/Unban`, `BanAddedEvent` and `BanRemovedEvent`. Tests: `Integration/Moderation/BanTests.cs`, 33 tests with 3 skipped (KI-19 with KI-03, KI-01, KI-03) | `Features/Moderation/Ban/` | MOD-01 to MOD-42 | built H-06 and the ban parts of H-03, H-04 and H-05 | L |
| A-07 | `[x]` | `/Kick`. Tests: `Integration/Moderation/KickTests.cs`. **Found KI-25: `/Kick` is never registered**, so the test bot registers it to test its logic. Skipped: the real registration (KI-25), MOD-53 (KI-01). Registering it is a one-line production fix that needs your go-ahead | `Features/Moderation/Kick/KickUser.cs` | MOD-43 to MOD-56 | kick route, `unregisteredCommands` option | M |
| A-08 | `[x]` | `/Mutes` Create, Set, View and Refresh. Tests: `Integration/Moderation/MuteAdminTests.cs`, 20 tests with 2 skipped. Found KI-26 (View never lists mutes), KI-27 (retry fallback never applies) and KI-28 | `Features/Moderation/Mute/Commands/MuteAdminCommands*.cs` | MOD-69 to MOD-89 | role registry, overwrites, more channel types | M |
| A-09 | `[x]` | `/Mute` and `/Unmute`. Tests: `Integration/Moderation/MuteTests.cs`, 32 tests with 5 skipped (KI-05, KI-07, KI-04, KI-01, KI-11). Found that zero-length mutes are never recorded, so they never expire (KI-07, raised to high) | `Features/Moderation/Mute/Commands/MuteUser.cs`, `UnmuteUser.cs` | MOD-90 to MOD-123 | member role routes, role positions | L |
| A-10 | `[x]` | `/Lock` and `/Unlock`, for channels and threads. Tests: `Integration/Moderation/LockCommandTests.cs`, 22 tests with 1 skipped (KI-06). A zero-length lock works, unlike a zero-length mute | `Features/Moderation/Lock/Commands/` | MOD-144 to MOD-158, MOD-163, MOD-164, MOD-169 to MOD-177 | none new | L |
| A-11 | `[x]` | Mute and lock expiry, and rejoining while muted. Tests: `Integration/Moderation/MuteExpiryTests.cs` (16) and `LockExpiryTests.cs` (9). MOD-135 and MOD-185 (retries after 30 seconds of backoff) stay manual, and MOD-141 to 143 need `/Mutes View` to list members (KI-26) | `Features/Moderation/Mute/`, `Features/Moderation/Lock/LockBackgroundTasks.cs`, `Utilities/ExpiredItemTriage.cs` | MOD-124 to MOD-143, MOD-178 to MOD-189 | built H-07, member leave and join, channel deletion | M |
| A-12 | `[x]` | **Locked-thread enforcement.** Registered `LockedTreadEvents` (KI-20) and added `Integration/Moderation/ThreadLockTests.cs`: deletion of member messages and reactions in public, private and forum threads; staff, admin and bot exemptions; unlocked threads; re-locking. Skipped: MOD-168, because the handler never checks the module (KI-12) | `Features/Moderation/Lock/LockedTreadEvents.cs` | MOD-159 to MOD-168 | built the thread parts of H-03 | S |
| A-13 | `[x]` | Area 02 cross-checks. Tests: `Integration/Moderation/ModerationCrossCheckTests.cs` for MOD-190, 193 and 194. MOD-191 and 195 are covered by the item tests, MOD-192 stays manual, and MOD-196 is in A-35 | none extra | MOD-190 to MOD-196 | the fake refuses posts where the bot can't send | S |

A-12 is done. Thread locks work by deletion, not permission overwrites, because threads inherit their parent
channel's permissions. The lock check ignores the end time: a lock lasts until an unlock row is written, either by
`/Unlock` (A-10) or by the lock background task once the timer passes (A-11).

### Phase 3: Sins (area 03; no manual doc yet)

`03-sins.md` has section A (SinLog). Add each later item's section to it, written from the source, using the case
format in `README.md`.

| ID | Status | Item | Source | Harness | Size |
|----|--------|------|--------|---------|------|
| A-14 | `[x]` | `/SinLog`. Wrote section A of `03-sins.md`. Tests: `Integration/Moderation/SinLogTests.cs`, 16 tests with 3 skipped. Found KI-29, KI-30 and KI-31 (System sins can't be saved, which breaks unattributed bans) | `Features/Moderation/SinAdmin/Commands/SinLog.cs` | none new | M |
| A-15 | `[x]` | `/Pardon`, `/Forget` and `/Reason`. Wrote section B of `03-sins.md`. Tests: `Integration/Moderation/SinAdminTests.cs`, 21 tests with 3 skipped. Found KI-32 (Pardon without a reason throws), KI-33 and KI-34 | `Features/Moderation/SinAdmin/Commands/PardonSin.cs`, `ForgetSin.cs`, `UpdateSinReason.cs` | none new | M |
| A-16 | `[x]` | `/ModSettings` View, PublicBanLog and AutoPardon. Wrote section C of `03-sins.md`. Tests: `Integration/Moderation/ModSettingsTests.cs`, 14 tests with 1 skipped (KI-35) | `Features/Moderation/SinAdmin/Commands/ModSettings*.cs` | none new | S |
| A-17 | `[x]` | `/Publish` Ban, Unban and Kick. Wrote section D of `03-sins.md`, completing the area doc. Tests: `Integration/Moderation/PublishTests.cs`, 11 tests with 2 skipped (KI-36, KI-33) | `Features/Moderation/PublishSins/` | the fake keeps the bot's posts, with GET and PATCH for a single message | M |

### Phase 4: Spam filter and diagnostics (area 04; no manual doc yet)

Area 04's cases live in the test classes' doc comments until someone writes `04-spamfilter-diagnostics.md`.

| ID | Status | Item | Source | Harness | Size |
|----|--------|------|--------|---------|------|
| A-18 | `[x]` | `/SpamFilter` Override and View, and `SpamEvents`. Tests: `Integration/Moderation/SpamFilterTests.cs`, 14 tests with 3 skipped. **KI-31 means spam is never acted on**; the "not filtered" tests are meaningful because a flagged message logs an error, which a spam control confirmed. Found KI-37. GEN-90 (AntiSpam flag) is blocked behind KI-31 | `Features/Moderation/SpamFilter/` | none new | M |
| A-19 | `[x]` | **Reaction tracking.** `Integration/Logging/ReactionTests.cs`: save, soft delete, revive, remove-emoji, clear-all, unlogged messages, purge with the message, and the message log turned off | `Features/Logging/MessageLogging/ReactionEvents.cs` | none | S |
| A-20 | `[x]` | `/Diagnostics` Check and Ignore (Add, Remove, View), and the report after `/Mutes Create`. Tests: `Integration/Moderation/DiagnosticsTests.cs`, 8 tests. The analysis is unit tested already. No new bugs | `Features/Moderation/Diagnostics/` | mentionable options, uploaded files | M |

### Phase 5: Logging (area 05; no manual doc yet)

Area 05's cases live in the test classes' doc comments until someone writes `05-logging.md`.

| ID | Status | Item | Source | Harness | Size |
|----|--------|------|--------|---------|------|
| A-21 | `[x]` | `/Log Message` Set, View, Override and ViewOverrides. Tests: `Integration/Logging/MessageLogSettingsTests.cs`, 14 tests; overrides are checked by whether messages get saved. No new bugs | `Features/Logging/Settings/LogSettingsCommands.Message*.cs` | none new | M |
| A-22 | `[x]` | Message add, edit, delete and bulk delete, and their log posts. Tests: `Integration/Logging/MessageLogEventsTests.cs`, 9 tests with 3 skipped. **Found KI-38: message edits are never logged**, and KI-39 (DSharpPlus breaks delete attribution) | `Features/Logging/MessageLogging/` | message edit, delete and bulk-delete events; audit entry options | L |
| A-23 | `[x]` | `/Log User` Set and View, and join, leave, nickname, username and avatar logging. Tests: `Integration/Logging/UserLogTests.cs`, 16 tests with 1 skipped. **Found KI-40: a first nickname is never logged** | `Features/Logging/UserLogging/`, `Settings/LogSettingsCommands.User*.cs` | `GUILD_MEMBER_UPDATE` profile changes; fake CDN | M |
| A-24 | `[x]` | `CleanupLogsBackgroundTask`: old-log deletion via REST, retries, and history trimming. Tests: `Integration/Logging/LogCleanupTests.cs`, 6 tests, each proven by mutating the cutoffs, the retry limit, the keep count or the failure handling. No bugs found | `Features/LogCleanup/CleanupLogsBackgroundTask.cs` | none new | M |

### Phase 6: Leveling (area 06; no manual doc yet)

| ID | Status | Item | Source | Harness | Size |
|----|--------|------|--------|---------|------|
| A-25 | `[x]` | `GainUserXp`: XP gain, cooldown, ignores, reward role grants, and the level-up message. Tests: `Integration/Leveling/GainXpTests.cs`, 15 tests with 1 skipped. Found KI-41 (an ungrantable reward is still logged as earned) | `Features/Leveling/Events/GainUserXp.cs` | `PATCH guilds/{id}/members/{id}` with roles | L |
| A-26 | `[x]` | `/Level`, `/Leaderboard` and `LeaderboardRefreshBackgroundTask`. Tests: `Integration/Leveling/LevelAndLeaderboardTests.cs`, 15 tests with 2 skipped; the view is refreshed after seeding and around the reset. Found KI-42 (Next Reward shows the wrong level and can pick the wrong reward) | `Features/Leveling/UserCommands/`, `LeaderboardRefreshBackgroundTask.cs` | none new | M |
| A-27 | `[x]` | `/LevelSettings` View, Set and LogSet. Tests: `Integration/Leveling/LevelSettingsTests.cs`, 14 tests with 1 skipped. Found KI-43 (View prints record dumps). The commands are `level_settings` with `log_set` | `Features/Leveling/Settings/LevelSettingsCommandGroup*.cs` | none new | M |
| A-28 | `[x]` | `/Rewards` Add, Remove and View, `/Ignore` Add, Remove and View, and `/Award` and `/Reclaim`. Tests: `Integration/Leveling/RewardCommandsTests.cs` (10, 2 skipped) and `IgnoreAndAwardTests.cs` (14, 2 skipped). **Found KI-45: any member can remove rewards**, plus KI-44 (channels can't be ignored) and KI-46 | `Features/Leveling/Rewards/`, `Settings/IgnoreCommandGroup*.cs`, `Awards/` | none new | L |

### Phase 7: Custom commands (area 07; no manual doc yet)

| ID | Status | Item | Source | Harness | Size |
|----|--------|------|--------|---------|------|
| A-29 | `[x]` | `/Commands` Learn, Forget, History, Revert, Stats and Leaderboard. Tests: `Integration/CustomCommands/CustomCommandSettingsTests.cs`, 18 tests with 1 skipped. Found KI-47 (Forget's log). Embed colors are stored upper case | `Features/CustomCommands/CustomCommandSettings*.cs` | none new | L |
| A-30 | `[x]` | `/Command` and `TextCustomCommandProcessor`. Tests: `Integration/CustomCommands/CustomCommandUseTests.cs`, 13 tests, proven by mutating the role check, the @everyone guard, the argument split and the bot filter. No bugs found; the inverted role check from the earlier review is already fixed | `Features/CustomCommands/GetCustomCommand.cs`, `TextCustomCommandProcessor.cs` | `DELETE .../messages/@original`; `FakeInteraction.IsOriginalResponseDelete` | M |

### Phase 8: Alerts and health (area 08; no manual doc yet)

| ID | Status | Item | Source | Harness | Size |
|----|--------|------|--------|---------|------|
| A-31 | `[x]` | `AlertService`, the warning buffer and flush, and the daily report. Tests: `Integration/Shared/AlertTests.cs`, 7 tests with a manual clock, proven by mutating the cooldown and the report's day and time guards. No bugs found | `Features/Shared/Alerts/` | `StartAsync(configureHost:)` for extra config and replaced services | M |
| A-32 | `[x]` | `GuildIssueReporter` and its throttle, `GatewayWatchdogService`, and `GatewayEvents`. Tests: `Integration/Shared/GatewayAndIssueTests.cs`, 6 tests with 2 skipped. **Found KI-48: DSharpPlus never reports disconnects or resumes, so outages go unnoticed.** The watchdog works when health is marked down by hand | `Features/Shared/GuildIssues/`, `Features/Shared/Gateway/` | `Gateway.RequestReconnect()`, `CloseFromServer(code)`, `HoldReconnects` | M |
| A-33 | `[x]` | `GuildAdded`, `UpdateAllGuilds`, `InviteEvents` and `MemberAdded`. Tests: `Integration/Shared/GuildSyncAndInviteTests.cs`, 8 tests with 2 skipped. Found KI-49 (a rejoin logs a false avatar change) and KI-50 (stale invite count). Invite deletion via audit log is not covered | `Features/Shared/Events/`, `Features/Logging/UserLogging/` | `FakeGuild.Invites` and `FakeInvite`; `bot.MemberJoinsWithInvite` | M |

### Phase 9: Cross-cutting (area 09; no manual doc yet)

| ID | Status | Item | Harness | Size |
|----|--------|------|---------|------|
| A-34 | `[x]` | **PluralKit**. Only the message log's delete handler uses it; leveling and the spam filter don't. Tests: `Integration/Logging/PluralKitTests.cs`, 3 tests, proven by flipping the proxy check. Editing a proxied message is blocked by KI-38. No new bugs | `FakePluralKit` behind the "Pluralkit" HttpClient; webhook authors in `SendMessage` | M |
| A-35 | `[x]` | **Multi-guild isolation**, including MOD-196. Tests: `Integration/Shared/MultiGuildTests.cs`, 10 tests: modules, ban, mute and mute role, locks, sins, XP and custom commands stay in their guild, covering MOD-25, 89, 111, 176 and 196. Proven by dropping `/Level`'s guild filter. No leaks found | H-09 | M |
| A-36 | `[x]` | **Matrices**. Tests: `Integration/Shared/CommandMatrixTests.cs`, 64 tests: a snapshot of every command's `default_member_permissions` and a check that the table lists every registered command, plus the module-off refusal for 33 module-gated commands. Proven by disabling the module check. Only `RequirePermissions` user lists register Discord-side permissions (lock, unlock, purge and warn), so every other command is shown to every member and refused bot-side. That's a design choice to review, not a known issue | H-02 | M |
| A-37 | `[x]` | **Persistence**. Tests: `Integration/Shared/PersistenceTests.cs`, 5 tests. The four cache tests (XP amount, rewards, XP ignores, log override) read, change through a command, then read again, and fail when invalidation is broken. A restart keeps leveling settings, the log channel and rewards. A static scan found an invalidation on every cached write path. No bugs | none | S |
| A-38 | `[x]` | **Startup and migrations**. Tests: `Integration/Shared/StartupMigrationTests.cs`, 3 tests, each on a fresh database in the shared container: empty, one migration behind on both schemas, and up to date. Proven by skipping the startup migration. On a fresh database EF logs two errors while reading the not-yet-created history table; that's EF's noise, not a bug | `StartAsync(connectionString:)` | S |

## Progress log

Add one line per session: the date, the item, what changed, and what was skipped.

- 2026-10-25: built the harness spike; A-05 partial, A-19 partial. Found KI-18, KI-19 and KI-20.
- 2026-10-25: A-00 done. Split GuildLog into a queue and a `GuildLogPublisher` hosted service, and unskipped the Warn mod-log test.
- 2026-10-25: A-12 done. Registered `LockedTreadEvents` and added `ThreadLockTests`; MOD-168 skipped (KI-12).
- 2026-10-25: A-01 done. 30 tests for the shared checks and `/Modules`; built H-02 and the reply reader. No new bugs.
- 2026-10-25: A-02 done. 20 tests; found KI-21 (public errors after defer) and KI-22.
- 2026-10-25: A-03 done. 16 tests (5 skipped); found KI-23 and widened KI-19 to `/UserInfo`, `/Ban` and `/Level`.
- 2026-10-25: A-04 done. 20 tests (2 skipped); found KI-24 (purge capped at 100 messages).
- 2026-10-25: A-05 done. 14 Warn tests (2 skipped for KI-19); built H-01 REST overrides.
- 2026-10-25: A-06 done. 33 Ban tests (3 skipped); confirmed KI-19 breaks Ban's hierarchy check. Built H-06.
- 2026-10-25: A-07 done. 15 Kick tests (2 skipped); found KI-25 (`/Kick` never registered).
- 2026-10-25: A-08 done. 20 Mutes admin tests (2 skipped); found KI-26, KI-27 (retry fallback broken) and KI-28.
- 2026-10-25: A-09 done. 32 Mute tests (5 skipped); zero-length mutes are never recorded (KI-07 raised to high).
- 2026-10-25: A-10 done. 22 Lock tests (1 skipped, KI-06); a zero-length lock is fine, so KI-07 only bites /Mute.
- 2026-10-25: A-11 done. 16 mute expiry and rejoin tests and 9 lock expiry tests; built H-07. No new bugs.
- 2026-10-25: A-13 done. 3 cross-check tests; the fake now refuses posts to channels the bot can't send in.
- 2026-10-25: A-14 done. Started `03-sins.md`; 16 SinLog tests (3 skipped); found KI-29, KI-30 and KI-31.
- 2026-10-25: A-15 done. 21 Pardon, Forget and Reason tests (3 skipped); found KI-32, KI-33 and KI-34.
- 2026-10-25: A-16 done. 14 ModSettings tests (1 skipped); found KI-35.
- 2026-10-25: A-17 done. 11 Publish tests (2 skipped); found KI-36 and widened KI-33. Area 03 doc drafted.
- 2026-10-25: A-18 done. 14 spam filter tests (3 skipped); KI-31 breaks the whole filter; found KI-37.
- 2026-10-25: A-19 done. Added the message-log-off reaction test.
- 2026-10-25: A-20 done. 8 Diagnostics tests; phase 4 complete.
- 2026-10-25: A-21 done. 14 message log settings tests.
- 2026-10-25: A-22 done. 9 message event tests (3 skipped); found KI-38 (edits never logged) and KI-39.
- 2026-10-25: A-23 done. 16 user log tests (1 skipped); found KI-40 (first nickname never logged). Added FakeCdn.
- 2026-10-25: A-24 done. 6 log cleanup tests, no bugs.
- 2026-10-25: A-25 done. 15 XP gain tests (1 skipped); found KI-41.
- 2026-10-25: A-26 done. 15 level and leaderboard tests (2 skipped); found KI-42.
- 2026-10-25: A-27 done. 14 level settings tests (1 skipped); found KI-43.
- 2026-10-25: A-28 done. 24 reward, ignore, award and reclaim tests (4 skipped); found KI-44, KI-45 (any member can remove rewards) and KI-46.
- 2026-10-25: A-29 done. 18 custom command settings tests (1 skipped); found KI-47.
- 2026-10-25: A-30 done. 13 custom command use tests, no bugs.
- 2026-10-25: A-31 done. 7 alert pipeline tests, no bugs. Added the host hook.
- 2026-10-25: A-32 done. 6 gateway and guild issue tests (2 skipped); found KI-48 (outages never detected).
- 2026-10-25: A-33 done. 8 guild sync and invite tests (2 skipped); found KI-49 and KI-50.
- 2026-10-25: A-34 done. 3 PluralKit tests, no new bugs. Added FakePluralKit.
- 2026-10-25: A-35 done. 10 multi-guild isolation tests, no leaks. Built H-09 (second guild).
- 2026-10-25: A-36 done. 64 matrix tests, no new bugs.
- 2026-10-25: A-37 done. 5 cache and restart tests, no bugs.
- 2026-10-25: A-38 done. 3 startup and migration tests, no bugs. The numbered backlog is complete.
