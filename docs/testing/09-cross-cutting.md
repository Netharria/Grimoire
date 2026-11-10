# 09 - Cross-cutting

Scope: startup and migrations, PluralKit (proxied messages in leveling, logging and the spam filter), the permission
matrix, the module-toggle matrix, settings cache invalidation, and restart persistence.

Source: `Grimoire/Program.cs`, `Grimoire/GrimoireServiceRegistration.cs`, `Grimoire/Utilities/DiscordStartService.cs`,
`Grimoire/Utilities/GenericBackgroundService.cs`, `Grimoire/Features/Shared/PluralKit/`,
`Grimoire/Features/Shared/Attributes/`, `Grimoire/Features/Shared/CommandHandler.cs`,
`Grimoire/Features/Shared/Channels/GuildLog/GuildLogPublisher.cs`, `Grimoire.Settings/SettingsServiceRegistration.cs`,
`Grimoire.Settings/Services/`, and the gate attributes on every command class under `Grimoire/Features/`.

Do the standard preflight from `00-environment.md` first. Accounts: `owner`, `admin`, `mod`, `user1`, `user2`,
`higher`, `pk`. Area 01 already tests the `RequireModuleEnabled` and `RequireUserGuildPermissions` checks themselves
(GEN-01 to GEN-17). This doc builds the full matrices on top of them and does not repeat those cases.

Most of this area is automated (backlog A-34 to A-38). Cases a test covers are tagged **[auto]** with the test name.
Test classes are in `Grimoire.Tests/Integration/` unless the name starts with `Settings.Tests`, which means
`Grimoire.Settings.Tests/`.

## How the gates work

- `RequireGuild` (DSharpPlus) marks the command guild-only. No command is offered in DMs at all
  (`CommandChecksTests.NoCommand_IsAvailableInDirectMessages` checks every registered command).
- `RequireUserGuildPermissions(p)` (Grimoire) is checked by the bot only. It reads the member's **guild-level**
  permissions. The owner and Administrator pass. It does not change who Discord shows the command to.
- `RequirePermissions(bot, user)` (DSharpPlus) checks the bot's and the user's permissions **in the channel**. A
  non-empty user list also sets the command's `default_member_permissions`, so Discord hides it from members without
  that permission. The one-argument form `RequirePermissions(p)` applies `p` to both.
- `RequireModuleEnabled(m)` (Grimoire) reads the module flag through the settings cache. A lookup failure reads as
  disabled.
- Attributes on a command class apply to every subcommand in it. Nested group classes (`/Log Message`, `/Log User`,
  `/Diagnostics Ignore`) add their own.
- When checks fail, `CommandHandler` replies with an ephemeral yellow embed that joins every distinct failure message,
  one per line. Bot permission failures from `RequirePermissions` read "The bot did not have the needed permissions to
  execute this command." A Discord 403 during the command reads as the `BotMissingPermissions` guild issue, with a
  suggested fix.

---

## A. Startup

On start the host runs its hosted services in order. `GuildLogPublisher`, `AlertService`, `GatewayWatchdogService`,
`WarningFlushService` and `DailyWarningReportService` start first. Then `DiscordStartService` applies pending
migrations (main schema, then settings schema) and only then connects to Discord. The cleanup, lock, mute and
leaderboard background services start after that. Each `GenericBackgroundService` waits a random delay up to one
interval before its first tick.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| XC-01 | Up-to-date database | Start the bot | Bot comes online; slash commands registered; commands work | No "Applied pending migrations" log line. **[auto]** `StartupMigrationTests.OnAnUpToDateDatabase_NothingIsMigrated` |
| XC-02 | `Grimoire/appsettings.json` missing | Start the bot | Bot never comes online | Host exits at once with a missing-file error (the file is not optional) |
| XC-03 | `token` empty, or a revoked token | Start the bot | Bot never comes online | Record the error and whether the process exits or retries. Migrations still run first |
| XC-04 | `ConnectionStrings:Grimoire` empty, `ConnectionProperties` section filled | Start against the local docker DB | Note: the fallback string forces `SSL Mode=Require`, so a local DB without SSL is refused | Record the error. With an SSL-enabled DB, it starts |
| XC-05 | DB container stopped | Start the bot | Bot never comes online | Npgsql retries (`EnableRetryOnFailure`), then the host stops with a connection error. Discord is never contacted |
| XC-06 | `pluralkitToken` or `pluralkitUserAgent` empty | Start, then delete three logged messages | Deletes logged as usual | One warning "UserAgent or Pluralkit Token Not Present. Pluralkit integration disabled." for the whole run. **[auto]** `PluralKitTests.WithoutPluralKitConfigured_TheWarningIsLoggedOnce` |
| XC-07 | `pluralkitToken` and `pluralkitUserAgent` set, `pluralkitApiEndpoint` removed | Start, then `user1` sends a message and deletes it within a minute | **Expected:** PluralKit treated as not configured; the delete is logged. **Actual (from code):** the bot starts, but `CreateClient("Pluralkit")` throws `ArgumentException` outside the lookup's `try`, so the delete handler fails and nothing is logged (observation 1) | Console: event handler error; no `MessageDeletedEntry` row. **[auto]** `PluralKitAcrossFeaturesTests.WithoutAnApiEndpoint_AQuickDelete_IsStillLogged` (skipped, KI-101) |
| XC-08 | Normal start | Watch the console for the first minute | none | `LockBackgroundTasks` and `MuteBackgroundTasks` log their start delay only after the migration log (or after connect). First lock/mute tick within about 10 s |
| XC-09 | Bot in Test-A and Test-B | Start the bot | none | Members of both guilds synced into username, nickname and avatar history. **[auto]** `MultiGuildTests.BothGuildsMembers_AreSyncedAtStartup` |
| XC-10 | Invite tracking on, user log join channel set | `user2` joins with an invite in the first seconds after start | Join logged; the invite may be missing | Known startup window, see KI-52. Record how long after "ready" the invite shows |
| XC-11 | Restart on the same data | Start, stop, start | none | No duplicate history rows. **[auto]** `GuildSyncAndInviteTests.Restarting_DoesNotDuplicateHistory` |

## B. Migrations

Both contexts use the same connection string and database, in separate schemas. Each pending set is applied with a
30-minute command timeout (`SettingsServiceRegistration.MigrationCommandTimeout`) and logged at Warning with its
duration.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| XC-20 | Empty database | Start the bot | Bot online, commands work | "Applied pending migrations in N ms" and "Applied pending settings migrations in N ms". EF logs two errors while reading the not-yet-created history tables; that is EF noise (A-38). **[auto]** `StartupMigrationTests.OnAnEmptyDatabase_AllMigrationsAreApplied_AndTheBotWorks` |
| XC-21 | Database one migration behind on each schema | Start the bot | Bot online | Only the last migration of each schema applied. **[auto]** `StartupMigrationTests.OnADatabaseOneMigrationBehind_TheLastMigrationIsApplied` |
| XC-22 | Restored copy of the production database, current branch (includes `DropOrphanedLegacyForeignKeys`, `SyncIndexesWithModel`, `AddReactionModel`, `RequirePardonModerator`, `RenameAttachmentFileNameToAttachmentUrl`) | Start the bot | Bot online; `/Modules View`, `/SinLog`, `/Level` show the expected data | Every migration logged once. Spot-check row counts of `Sins`, `Pardons`, `XpHistory`, `Messages`, `Attachments` before and after. Record the total migration time |
| XC-23 | Same as XC-22 on a large `Messages` / `Attachments` table | Start the bot | none | No Npgsql 30 s timeout: the 30-minute command timeout applies. Record the longest statement |
| XC-24 | A pending main migration made to fail (for example, a row that breaks a new constraint) | Start the bot | Bot never comes online | Host exits with the migration error. The failed migration is rolled back. Settings migrations are not attempted. Fix the row and restart: the rest apply |
| XC-25 | Main schema current, a pending settings migration made to fail | Start the bot | Bot never comes online | Main schema unchanged, host exits with the settings error. Restart after the fix applies only the settings migration |
| XC-26 | Pending migrations | Start two bot processes at the same moment on the same DB | Record | Record whether the second waits, fails, or applies twice. Do not run this against production data |
| XC-27 | Database ahead of the binary (run an older build after XC-22) | Start the older build | Record | No pending migrations, so it starts without warning. Record any query errors from renamed columns (for example `AttachmentUrl`) |
| XC-28 | Pending migration that takes over 2 minutes (XC-23), `warningReportTimeUtc` in the past today | Start the bot and watch `#alerts` | Record | `DailyWarningReportService` and `WarningFlushService` start before migrations finish, so their first tick may query the old schema. Expect at most a `BackgroundTaskFailure` warning, then normal ticks (observation 8). **[auto]** `StartupOrderTests.TheWarningServices_StartAfterMigrations` (registration order, no slow migration; skipped, KI-106) |

## C. PluralKit

Only the message log's delete handler calls PluralKit (`DeleteMessageEvent`). The edit handler reads the stored
`ProxiedMessageLink`. Leveling, the spam filter and custom commands don't call PluralKit; they skip webhook
authors because webhook messages are from bots and have no member. The lookup is skipped for messages over a minute
old and is rate-limited to two a second with no queue.

Setup: `pk` has a PluralKit system with one member and a proxy tag (for example `a:text`). The message log and its
delete and edit channels are set. PluralKit must be configured (`pluralkitToken`, `pluralkitUserAgent`,
`pluralkitApiEndpoint`).

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| XC-30 | none | `pk` sends `a:hello` | PluralKit replaces it with a proxy; no "Message deleted" log | Both messages saved. A `ProxiedMessageLink` row links proxy to original with system and member ids. No delete entry for the original. **[auto]** `PluralKitTests.TheOriginalBeingReplacedByAProxy_IsLinked_NotLogged` |
| XC-31 | After XC-30 | `pk` deletes the proxy (PluralKit's delete reaction or the app) within a minute | Delete log names `pk` as author and shows the system and member | Delete entry on the proxy. **[auto]** `PluralKitTests.DeletingTheProxy_IsLoggedWithTheOriginalAuthorAndSystem` |
| XC-32 | After XC-30 | Wait over a minute, then delete the proxy | Same as XC-31: the link is read from the DB, so no lookup is needed | Delete entry on the proxy. **[auto]** `PluralKitAcrossFeaturesTests.DeletingAnOldProxy_UsesTheStoredLink_WithoutALookup` |
| XC-33 | none | `user1` sends and deletes a plain message | Logged as usual | **[auto]** `PluralKitTests.AMessagePluralKitDoesNotKnow_IsLoggedAsUsual` |
| XC-34 | After XC-30 | `pk` edits the proxy through PluralKit | Edit log shows before and after, with `pk`'s avatar, not the webhook's | Edit entry. **[auto]** `PluralKitTests.EditingAProxy_ShowsTheOriginalAuthorsAvatar` |
| XC-35 | PluralKit API returns 403, non-JSON, or a changed shape | Delete a logged message | Logged as an ordinary delete | Info log "PluralKit lookup for message ... failed". **[auto]** `PluralKitTests.APluralKitFailure_StillLogsTheDelete` |
| XC-36 | none | Delete five plain messages in one second | All five logged | Lookups beyond two a second are refused by the rate limiter and fall back to an ordinary log. **[auto]** `PluralKitTests.ABurstOfDeletes_IsAllLogged` |
| XC-37 | none | `pk` sends five proxied messages within a second | **Expected:** five proxies, no delete logs. **Actual (from the limiter):** originals whose lookup is rate-limited are logged as deleted by `pk`, and their proxies get no link | Record how many false delete logs appear. Later deletes of those proxies show the webhook as author. **[auto]** `PluralKitAcrossFeaturesTests.FiveQuickProxies_AreAllLinked_AndNoneLoggedAsDeleted` (skipped: 3 of 5 logged, KI-111) |
| XC-38 | none | Delete a message that was never logged (sent while the message log was off) | Nothing logged | No PluralKit request. **[auto]** `PluralKitTests.DeletingAnUnloggedMessage_AsksPluralKitNothing` |
| XC-39 | `#spam` has message-log override Never | `pk` proxies a message in `#spam` | Nothing logged | Neither message saved; no PluralKit request. **[auto]** `PluralKitAcrossFeaturesTests.InANeverLoggedChannel_NothingIsSaved_AndPluralKitIsNotAsked` |
| XC-40 | Leveling on, XP timeout 3 min, `pk` has no timeout running | `pk` sends one proxied message | none | One `EarnedXp` row for `pk` from the original. None for the webhook user id. **[auto]** `PluralKitAcrossFeaturesTests.ProxiedMessages_EarnXpOnlyForTheMember_OncePerTimeout` |
| XC-41 | After XC-40 | `pk` sends another proxied message inside the timeout | none | No new XP row (the timeout is per account). **[auto]** `PluralKitAcrossFeaturesTests.ProxiedMessages_EarnXpOnlyForTheMember_OncePerTimeout` |
| XC-42 | Leveling on, a reward at the next level | `pk` levels up through proxied messages | Reward role granted to `pk`; level-up log names `pk` | Reward and XP rows for `pk` only. **[auto]** `PluralKitAcrossFeaturesTests.LevellingUpThroughProxies_RewardsTheMember` |
| XC-43 | AntiSpam and Moderation on, mute role set | `pk` spams through proxies | `pk` is muted; the mute log and DM go to `pk` | Sin and mute for `pk`. The filter's own delete of the original fails quietly (PluralKit already deleted it). **The proxies stay**, because the filter skips bot authors. Record. **[auto]** `PluralKitAcrossFeaturesTests.SpamThroughProxies_MutesTheMember_AndLeavesTheProxies` (proxies stay) |
| XC-44 | Commands on, text command `hello` learned | `pk` sends `a:!hello` and `!hello` with an auto-proxy | Record whether the original triggers the command (it does when it starts with `!hello`); the proxy never does | Usage row only for an original trigger. **[auto]** `PluralKitAcrossFeaturesTests.AProxiedTrigger_RunsOnlyFromTheOriginal` (auto-proxy `!hello` runs once; `a:!hello` never) |
| XC-45 | Ten proxied messages in `#spam` | `/Purge All count:10` | Bulk delete log lists ten messages | **Author shows the webhook id with no name**, not `pk`: the bulk handler doesn't read `ProxiedMessageLink` (observation 6) **[auto]** `PluralKitBulkDeleteTests.BulkDeletingProxies_NamesTheMemberBehindThem` (skipped, KI-104) |
| XC-46 | Thread locked (`/Lock` in a thread) | `pk` proxies a message in the thread | Record: the original is deleted by the lock handler or PluralKit, whichever is first; the proxy (a bot author) is also deleted, as bots stay subject to thread locks | Record which deletes happen and whether a delete log appears. **[auto]** `PluralKitAcrossFeaturesTests.InALockedThread_TheOriginalIsDeleted`; the proxy stays: `PluralKitAcrossFeaturesTests.InALockedThread_TheProxyIsDeletedToo` (skipped, KI-112) |
| XC-47 | `pk` proxied messages exist | `/Purge User user:pk` | Proxies not matched (author differs) | See GEN-155. **[auto]** `PluralKitAcrossFeaturesTests.PurgeUser_OnTheMember_LeavesTheProxies` |

## D. Permission matrix

"Who sees it" is the registered `default_member_permissions`: **all** means Discord shows it to every member (Use
Application Commands, or Discord's default); **ManageMessages** means Discord hides it from members without Manage
Messages in that channel. "User perm" is checked by the bot; **(guild)** means `RequireUserGuildPermissions` on guild
permissions, **(channel)** means `RequirePermissions` in the channel. "Bot perm" lists only what the attributes
declare. Owner and Administrator pass every user check.

The registered values are snapshot by **[auto]** `CommandMatrixTests.EachCommand_IsRegisteredForTheExpectedMembers`,
and `CommandMatrixTests.EveryRegisteredCommand_IsInTheTable` fails if a new command is added without updating it.

### General

| Command | Gate attributes | Who sees it | User perm | Bot perm | Module |
|---------|-----------------|-------------|-----------|----------|--------|
| `/GeneralSettings View` | RequireGuild, RUGP | all | ManageGuild (guild) | none | none |
| `/GeneralSettings ModLogChannel` | RequireGuild, RUGP | all | ManageGuild (guild) | none (checks Send in the target, GEN-35) | none |
| `/GeneralSettings UserCommands` | RequireGuild, RUGP | all | ManageGuild (guild) | none | none |
| `/Modules View` | RequireGuild, RUGP | all | ManageGuild (guild) | none | none |
| `/Modules Set` | RequireGuild, RUGP | all | ManageGuild (guild) | none | none |
| `/UserInfo` | RequireGuild, RUGP | all | ManageMessages (guild) | none | none (sections per module) |
| `/Purge All` | RequirePermissions(ManageMessages) | ManageMessages | ManageMessages (channel) | ManageMessages | none |
| `/Purge User` | RequirePermissions(ManageMessages) | ManageMessages | ManageMessages (channel) | ManageMessages | none |

### Moderation

| Command | Gate attributes | Who sees it | User perm | Bot perm | Module |
|---------|-----------------|-------------|-----------|----------|--------|
| `/Ban` | RequireGuild, RME, RUGP, RequirePermissions | all | ManageMessages (guild; legacy, intended, see 02) | BanMembers | Moderation |
| `/Unban` | RequireGuild, RME, RUGP, RequirePermissions | all | ManageMessages (guild; legacy) | BanMembers | Moderation |
| `/Kick` | RequireGuild, RME, RUGP, RequirePermissions | all | KickMembers (guild) | KickMembers | Moderation |
| `/Warn` | RequireGuild, RME, RequirePermissions | ManageMessages | ManageMessages (channel; intended, KI-17) | none | Moderation |
| `/Mute` | RequireGuild, RME, RUGP, RequirePermissions | all | ManageMessages (guild; legacy) | ManageRoles | Moderation |
| `/Unmute` | RequireGuild, RME, RUGP, RequirePermissions | all | ManageMessages (guild; legacy) | ManageRoles | Moderation |
| `/Lock` | RequireGuild, RME, RequirePermissions | ManageMessages | ManageMessages (channel; legacy) | ManageChannels, ManageRoles | Moderation |
| `/Unlock` | RequireGuild, RME, RequirePermissions | ManageMessages | ManageMessages (channel; legacy) | ManageChannels (**not** ManageRoles, observation 3) | Moderation |
| `/Mutes Create` | class: RequireGuild, RME, RUGP, RequirePermissions | all | ManageGuild (guild) | ManageRoles | Moderation |
| `/Mutes Set` | as Create | all | ManageGuild (guild) | ManageRoles | Moderation |
| `/Mutes View` | as Create | all | ManageGuild (guild) | ManageRoles | Moderation |
| `/Mutes Refresh` | as Create | all | ManageGuild (guild) | ManageRoles | Moderation |
| `/Publish Ban` | class: RequireGuild, RME, RUGP | all | ManageMessages (guild) | none | Moderation |
| `/Publish Unban` | as Ban | all | ManageMessages (guild) | none | Moderation |
| `/Publish Kick` | as Ban | all | ManageMessages (guild) | none | Moderation |
| `/Pardon` | RequireGuild, RME, RUGP | all | ManageMessages (guild) | none | Moderation |
| `/Forget` | RequireGuild, RME, RUGP | all | ManageMessages (guild) | none | Moderation |
| `/Reason` | RequireGuild, RME, RUGP | all | ManageMessages (guild) | none | Moderation |
| `/SinLog` | RequireGuild, RME | all | none (another user's log needs ManageMessages, in the body) | none | Moderation |
| `/ModSettings View` | class: RequireGuild, RUGP | all | ManageGuild (guild) | none | **none** (shows the module state) |
| `/ModSettings PublicBanLog` | class + RME | all | ManageGuild (guild) | none | Moderation |
| `/ModSettings AutoPardon` | class + RME | all | ManageGuild (guild) | none | Moderation |
| `/Diagnostics Check` | class: RequireGuild, RME, RUGP | all | ManageGuild (guild) | none | Moderation |
| `/Diagnostics Ignore Add` | as Check | all | ManageGuild (guild) | none | Moderation |
| `/Diagnostics Ignore Remove` | as Check | all | ManageGuild (guild) | none | Moderation |
| `/Diagnostics Ignore View` | as Check | all | ManageGuild (guild) | none | Moderation |
| `/SpamFilter Override` | RUGP only | all | ManageChannels (guild) | none | **none** (observation 2) |
| `/SpamFilter View` | RUGP only | all | ManageChannels (guild) | none | **none** |

### Leveling

| Command | Gate attributes | Who sees it | User perm | Bot perm | Module |
|---------|-----------------|-------------|-----------|----------|--------|
| `/Level` | RequireGuild, RME | all | none (public only in the user command channel, or with ManageMessages) | none | Leveling |
| `/Leaderboard` | RequireGuild, RME | all | none (as `/Level`) | none | Leveling |
| `/Award` | RequireGuild, RUGP, RME | all | ManageMessages (guild) | none | Leveling |
| `/Reclaim` | RequireGuild, RUGP, RME | all | ManageMessages (guild) | none | Leveling |
| `/Rewards Add` | RequireGuild, RME, RUGP | all | ManageGuild (guild) | none | Leveling |
| `/Rewards Remove` | RequireGuild, RME, RUGP | all | ManageGuild (guild) | none | Leveling |
| `/Rewards View` | RequireGuild, RME, RUGP | all | ManageGuild (guild) | none | Leveling |
| `/Ignore Add` | RequireGuild, RUGP, RME | all | ManageGuild (guild) | none | Leveling |
| `/Ignore Remove` | RequireGuild, RUGP, RME | all | ManageGuild (guild) | none | Leveling |
| `/Ignore View` | RequireGuild, RUGP, RME | all | ManageGuild (guild) | none | Leveling |
| `/LevelSettings View` | RequireGuild, RUGP | all | ManageGuild (guild) | none | **none** (shows the module state) |
| `/LevelSettings Set` | RequireGuild, RME, RUGP | all | ManageGuild (guild) | none | Leveling |
| `/LevelSettings LogSet` | RequireGuild, RME, RUGP | all | ManageGuild (guild) | none | Leveling |

### Logging

| Command | Gate attributes | Who sees it | User perm | Bot perm | Module |
|---------|-----------------|-------------|-----------|----------|--------|
| `/Log Message Set` | `Log` class: RequireGuild, RUGP; `Message` class: RME | all | ManageGuild (guild) | none | MessageLog |
| `/Log Message View` | as Set | all | ManageGuild (guild) | none | MessageLog (observation 5) |
| `/Log Message Override` | as Set | all | ManageGuild (guild) | none | MessageLog |
| `/Log Message ViewOverrides` | as Set | all | ManageGuild (guild) | none | MessageLog |
| `/Log User Set` | `Log` class: RequireGuild, RUGP; `User` class: RME | all | ManageGuild (guild) | none | UserLog |
| `/Log User View` | as Set | all | ManageGuild (guild) | none | UserLog (observation 5) |

### Custom commands

| Command | Gate attributes | Who sees it | User perm | Bot perm | Module |
|---------|-----------------|-------------|-----------|----------|--------|
| `/Commands Learn` | RequireGuild, RME, RUGP | all | ManageGuild (guild) | none | Commands |
| `/Commands Forget` | RequireGuild, RME, RUGP | all | ManageGuild (guild) | none | Commands |
| `/Commands History` | RequireGuild, RME, RUGP | all | ManageGuild (guild) | none | Commands |
| `/Commands Revert` | RequireGuild, RME, RUGP | all | ManageGuild (guild) | none | Commands |
| `/Commands Stats` | RequireGuild, RME | all | **none** | none | Commands |
| `/Commands Leaderboard` | RequireGuild, RME | all | **none** | none | Commands |
| `/Command` | RequireGuild, RME | all | none (per-command role restriction in the body) | none | Commands |
| `!name` text trigger | event, module checked in the body | n/a | none (role restriction in the body) | Send Messages | Commands |

RME = `RequireModuleEnabled`, RUGP = `RequireUserGuildPermissions`.

### Permission cases

Per-command permission-denied and bot-lacks-permission cases live in each area doc. These cases check the matrix
itself and the places where the two gate kinds behave differently.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| XC-50 | none | `user1` opens the command picker | Sees every command except `/Warn`, `/Lock`, `/Unlock`, `/Purge` | none. **[auto]** registration snapshot in `CommandMatrixTests` |
| XC-51 | none | `user1` runs one RUGP command from each table (`/Ban`, `/Mutes View`, `/Award`, `/Log User View`, `/Commands Learn`, `/SpamFilter View`) | Each: ephemeral "You do not have the required permissions to use this command." | Console: check failed; no data change |
| XC-52 | Server Settings > Integrations: restrict `/Ban` to the `mod` role | `user1` opens the picker; `mod` runs `/Ban` | `user1` no longer sees `/Ban`; `mod` still passes the bot check | Discord-side overrides add to the bot checks, they don't replace them |
| XC-53 | Integrations override grants `/Warn` to `user1` while `user1` lacks Manage Messages | `user1` runs `/Warn` | Record: Discord shows it, then the bot's channel check refuses | none |
| XC-54 | `user1` has Manage Messages only in `#spam` (channel overwrite) | In `#spam`, run `/Warn`, `/Lock`, `/Unlock`, `/Purge All` | All allowed (channel-resolved checks). `/Warn` channel-level is intended (KI-17); `/Lock` and `/Unlock` follow the legacy ManageMessages gate | Sin, lock and purge rows as usual. **[auto]** `ChannelPermissionTests.Warn_WithManageMessagesOnlyInTheChannel_IsAllowed`, `ChannelPermissionTests.LockAndUnlock_WithManageMessagesOnlyInTheChannel_Work`, `ChannelPermissionTests.PurgeAll_WithManageMessagesOnlyInTheChannel_Runs` |
| XC-55 | As XC-54 | In `#spam`, run `/Mute`, `/Ban`, `/Pardon`, `/UserInfo`, `/Award`, `/Publish Ban` | All refused: these check guild-level permissions | none. **[auto]** `ChannelPermissionTests.GuildLevelCommands_IgnoreTheChannelOverwrite` |
| XC-56 | As XC-54 | In `#general`, run `/Warn` | Not shown in the picker there; if forced through, refused | none. **[auto]** `ChannelPermissionTests.OutsideTheChannel_DiscordHidesTheCommand_AndTheBotDoesNotCheckAgain` (registration only: the bot does not re-check the member's side, so a forced-through interaction runs) |
| XC-57 | As XC-54, a thread under `#spam` | `user1` runs `/Warn` in the thread | Allowed: threads inherit the parent's overwrites. Record | Sin row. **[auto]** `ChannelPermissionTests.Warn_InAThreadUnderTheChannel_IsAllowed` |
| XC-58 | `owner` has no roles | `owner` runs `/Warn`, `/Lock`, `/Purge All` (RequirePermissions commands) | Allowed (Discord gives the owner every permission) | as usual. **[auto]** `ChannelPermissionTests.Owner_WithoutRoles_RunsTheChannelCheckedCommands` |
| XC-59 | `user1` lacks ManageGuild; Leveling off | `user1` runs `/Rewards View` | Both failures shown, one per line: the permission error and "This module is disabled in this server." Record the order, or if only one shows | none. **[auto]** `ChannelPermissionTests.MissingPermissionAndModuleOff_AreBothShown` (permission line first, then the module line) |
| XC-60 | Bot loses each declared permission in turn: Ban Members, Kick Members, Manage Roles, Manage Channels, Manage Messages | Run `/Ban`, `/Kick`, `/Mute`, `/Mutes View`, `/Lock`, `/Purge All` | "The bot did not have the needed permissions to execute this command." Nothing changed | Covered per command in 02 and 01 (MOD-172, MOD-177, GEN-140). **[auto]** `ChannelPermissionTests.BotPermissionDeniedInTheChannel_IsRefusedThere_ButNotElsewhere` (channel-level overwrite on the bot's role) |
| XC-61 | Bot has Manage Channels but not Manage Roles; `#locktest` locked earlier | `mod` runs `/Unlock` in `#locktest` | **Expected:** refused by the gate, as `/Lock` is. **Actual:** the gate passes, Discord refuses the overwrite, and the bot shows the missing-permissions help. The lock stays recorded (observation 3) | Guild issue `BotMissingPermissions` for `Unlock`; no unlock row. **[auto]** `ChannelPermissionTests.Unlock_WithoutManageRoles_IsRefusedByTheGate` (skipped, KI-102) |
| XC-62 | none | `user1` runs `/Commands Stats name:hello` and `/Commands Leaderboard` | Allowed for any member. **Confirm this is intended**; the other `/Commands` subcommands need ManageGuild | none. **[auto]** `ChannelPermissionTests.CommandsStatsAndLeaderboard_AreOpenToAnyMember` |
| XC-63 | `mod` (no ManageChannels) | `mod` runs `/SpamFilter View` | Refused. **[auto]** `SpamFilterTests.SpamFilter_NeedsManageChannels` | none |

## E. Module-toggle matrix

Every module reads as **disabled until it is set**: with no settings row, `IsModuleEnabled` returns false. `General`
is always on and can't be toggled. Disabling a module never stops timed mutes or locks from expiring.

| Module | Commands refused while off | Events and services that stop | Still works while off |
|--------|----------------------------|-------------------------------|-----------------------|
| Leveling | `/Level`, `/Leaderboard`, `/Award`, `/Reclaim`, `/Rewards Add/Remove/View`, `/Ignore Add/Remove/View`, `/LevelSettings Set/LogSet` | `GainUserXp` (no XP, no rewards); Leveling log posts dropped by `GuildLogPublisher` | `/LevelSettings View` (shows `Module Enabled: False`); `/UserInfo` without the level fields; `LeaderboardRefreshBackgroundTask` keeps refreshing |
| UserLog | `/Log User Set`, `/Log User View` | `GuildMemberAddedEvent`, `GuildMemberRemovedEvent`, `UpdatedAvatarEvent`, `UpdatedNicknameEvent`, `UpdatedUsernameEvent` (no history rows, no logs); UserLog log posts dropped | `/UserInfo` without Usernames/Nicknames; `MemberAdded` and `UpdateAllGuilds` still store username, nickname and avatar on join and at startup; `InviteEvents` keep tracking invites |
| MessageLog | `/Log Message Set/View/Override/ViewOverrides` | `AddMessageEvent` (via `ShouldLogMessage`), `UpdateMessageEvent`, `DeleteMessageEvent`, `BulkMessageDeletedEvent`, `ReactionEvents`; MessageLog log posts dropped | `/Purge`; `CleanupLogsBackgroundTask` keeps deleting old rows |
| Moderation | `/Ban`, `/Unban`, `/Kick`, `/Warn`, `/Mute`, `/Unmute`, `/Lock`, `/Unlock`, `/Mutes *`, `/Publish *`, `/Pardon`, `/Forget`, `/Reason`, `/SinLog`, `/ModSettings PublicBanLog/AutoPardon`, `/Diagnostics *` | `BanAddedEvent`, `BanRemovedEvent`, `UserJoinedWhileMuted`, `LockedThreadEvents`; PublicModeration posts dropped; spam mutes (`GetEffectiveMuteRole` returns none) | `/ModSettings View` (shows `Module Enabled: False`); `/UserInfo` without Warns/Mutes/Bans; `/SpamFilter Override/View`; `MuteBackgroundTasks` and `LockBackgroundTasks` still lift expiries and post to the mod log |
| Commands | `/Commands *`, `/Command` | `TextCustomCommandProcessor` (debug log "Ignored text command because the Commands module is disabled") | nothing else depends on it |
| AntiSpam | none | `SpamEvents` | `/SpamFilter Override/View` (not gated, observation 2) |
| General (always on) | never | never | `/GeneralSettings *`, `/Modules *`, `/UserInfo`, `/Purge`; the mod log (Moderation log type maps to General) |

### Module-toggle cases

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| XC-70 | Each module off in turn, the others on | `admin` runs each module-gated command | "This module is disabled in this server." for each | **[auto]** `CommandMatrixTests.EachModuleGatedCommand_RefusesWithItsModuleOff` (35 commands). Not in that list, run by hand: `/Kick`, `/Publish Unban/Kick`, `/Mutes Create/Set/Refresh`, `/Diagnostics Ignore Add/Remove/View`, `/Log Message Set/Override/ViewOverrides`, `/Log User Set`, `/ModSettings PublicBanLog`, `/LevelSettings LogSet` |
| XC-71 | Moderation off | `/ModSettings View`, then `/ModSettings AutoPardon` | View shows `**Module Enabled:** False`; AutoPardon refused | **[auto]** `ModSettingsTests.ModSettings_WithModerationDisabled_ViewShowsIt_AndChangesAreRefused` |
| XC-72 | Leveling off | `/LevelSettings View`, then `Set` and `LogSet` | View shows `**Module Enabled:** False`; both changes refused | **[auto]** `LevelSettingsTests.LevelSettings_WithLevelingDisabled_ViewShowsIt_AndChangesAreRefused` |
| XC-73 | MessageLog off, UserLog off | `/Log Message View`, `/Log User View` | Both refused, unlike the two Views above (observation 5) | **[auto]** `MessageLogSettingsGapTests.LogViews_WithTheirModulesOff_AreBothRefused` (KI-65) |
| XC-74 | Leveling off | `user1` sends messages; `/UserInfo user:user1` | No XP; UserInfo has no Level fields | No new `XpHistory` rows. Leaderboard refresh still runs every 15 min without errors. **[auto]** `ModuleToggleTests.LevelingOff_EarnsNoXp_AndUserInfoHasNoLevelFields` |
| XC-75 | UserLog off | `user1` changes nickname and username; `user2` leaves and rejoins | No user log posts | No `NicknameHistory` / `UsernameHistory` rows from the changes. The rejoin still stores the current name and avatar (`MemberAdded`). **[auto]** `ModuleToggleTests.UserLogOff_StoresAndLogsNoNicknameChange_AndTheNextLogShowsTheOlderValue` (nickname part) |
| XC-76 | After XC-75, UserLog back on | `user1` changes nickname again | Log "before" is the value stored at the rejoin or before disabling, not the one set while off. Record | One new row. **[auto]** `ModuleToggleTests.UserLogOff_StoresAndLogsNoNicknameChange_AndTheNextLogShowsTheOlderValue` (Before is the value stored before UserLog was turned off) |
| XC-77 | MessageLog off | `user1` sends, edits and deletes; reacts | Nothing logged | No `Messages` rows. **[auto]** `ModuleCommandsTests.DisabledMessageLog_SavesNoMessages_AndEnablingItStartsSaving`, `ReactionTests` (message log off) |
| XC-78 | A message saved while MessageLog was on; then MessageLog off | Delete it | Nothing logged | No delete entry. Re-enable: the row is still there with no delete entry. **[auto]** `ModuleToggleTests.MessageLogOff_ADeleteOfASavedMessage_IsNotRecorded` |
| XC-79 | Moderation off | `admin` bans `user2` from the Discord client, then unbans | No ban or unban log | No Sin row. **[auto]** `ModuleToggleTests.ModerationOff_ClientBanAndUnban_LeaveNoSinAndNoLog` |
| XC-80 | Moderation off, `user2` has an active mute | `user2` leaves and rejoins | No mute role given | **[auto]** `MuteExpiryTests.Rejoining_WithoutAnActiveMute_GivesNoRole` (moderation off) |
| XC-81 | A thread locked, then Moderation off | `user1` posts in the thread | Message stays | **[auto]** `ThreadLockTests.WithModerationDisabled_MessagesInALockedThreadStay` |
| XC-82 | A 2-minute mute and a 2-minute lock running, then Moderation off | Wait for both to end | Role removed, channel unlocked, "expired" messages posted to the mod log | `MuteRemoved` and `ChannelUnlocked` rows. **[auto]** `ModuleToggleTests.ModerationOff_MutesAndLocksStillExpire` (seeded expired records) |
| XC-83 | AntiSpam on, Moderation off, mute role set | `user1` spams **[auto]** `SpamEventsTests.WithModerationOff_SpamIsNeitherMutedNorDeleted` | Nothing happens: no mute, and **the spam is not deleted** either. Record (observation 9) | No Sin row |
| XC-84 | AntiSpam off | `user1` spams | Nothing happens | **[auto]** `SpamFilterTests.Spam_WithAntiSpamDisabled_IsIgnored` |
| XC-85 | Commands off, `hello` learned | `user1` sends `!hello`; runs `/Command command_name:hello` | Text trigger posts nothing; slash command refused | No usage row. **[auto]** `ModuleToggleTests.CommandsOff_NeitherTheTextTriggerNorTheSlashCommandAnswers` |
| XC-86 | Every module off | `/Modules View`, `/Modules Set`, `/GeneralSettings View`, `/UserInfo`, `/Purge All` | All work | **[auto]** `CommandChecksTests.ModulesCommands_WorkWithEveryModuleDisabled`; the rest is GEN-17 |
| XC-87 | Every module off, mod log set | `/Modules Set module:Leveling enable:True` | Mod log entry posted (General log type) | Module row true. **[auto]** `ModuleToggleTests.EveryModuleOff_ModulesSet_IsStillLogged` |
| XC-88 | Guild just added, no settings rows | `/Modules View` | Every module False | No rows until a module is set. **[auto]** `ModuleToggleTests.NewGuild_HasNoModuleRows_AndEveryModuleIsOff` |
| XC-89 | AntiSpam off | `/SpamFilter Override option:Always` **[auto]** `SpamFilterOverrideTests.Override_WithAntiSpamDisabled_IsRefused` (skipped, KI-60) | **Expected:** "This module is disabled in this server.", as `/ModSettings AutoPardon` does. **Actual:** the override is saved (observation 2) | `SpamFilterOverrides` row |

## F. Settings cache invalidation

Settings are cached in FusionCache through `HybridCache`, in memory only, for one day. Every write path removes or
replaces its cache key after saving. A direct SQL edit is not seen until the entry expires or the bot restarts. A
lookup that fails is not cached.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| XC-90 | Moderation off, a gated command already refused once (cache warm) | `/Modules Set module:Moderation enable:True`, then the command | Works at once | **[auto]** `CommandChecksTests.EnablingAModule_UnblocksItsCommandsImmediately`, `ModuleCommandsTests.View_RightAfterSet_ShowsTheNewState`, `Settings.Tests SetModuleStateTests.CacheInvalidatedAfterStateChange` |
| XC-91 | Leveling on, `user1` has earned XP once (cache warm) | Disable Leveling, `user1` sends after the timeout | No XP | No new row (the event reads the same cached flag). **[auto]** `ModuleToggleTests.DisablingLeveling_AfterItWasRead_StopsXpAtOnce` |
| XC-92 | XP amount read once | `/LevelSettings Set setting:Amount`, then `user1` sends | New amount used | **[auto]** `PersistenceTests.ChangingTheXpAmount_AppliesToTheNextMessage` |
| XC-93 | Rewards read once | `/Rewards Add`, then `/Level` | New reward shown | **[auto]** `PersistenceTests.AddingAReward_ShowsInTheNextLevelCheck` |
| XC-94 | Ignore list read once | `/Ignore Add user1`, then `user1` sends | No XP | **[auto]** `PersistenceTests.IgnoringAMember_AppliesToTheirNextMessage` |
| XC-95 | Message log override read once | `/Log Message Override` Never on `#spam`, then post there | Not saved | **[auto]** `PersistenceTests.ANeverOverride_AppliesToTheNextMessage` |
| XC-96 | Spam override read once | `/SpamFilter Override option:Never` on `#spam`, then spam there | Not filtered | **[auto]** `SpamFilterTests.NeverOverride_TurnsTheFilterOff`, `Settings.Tests SpamFilterOverrideTests.NewValue_InsertsRow_CacheUpdated` |
| XC-97 | Delete log set to `#messagelog` and used once | `/Log Message Set` delete log to `#modlog`; delete a message | Log appears in `#modlog` | Setting row. **[auto]** `SettingsCacheTests.MovingTheDeleteLog_AppliesToTheNextDelete` |
| XC-98 | Mute role A used once | `/Mutes Set role:B`; `/Mute user2`; trip the spam filter as `user1` | Both use role B | `Mutes.MuteRoleId` = B. **[auto]** `SettingsCacheTests.ChangingTheMuteRole_AppliesToTheNextMuteAndSpamMute` |
| XC-99 | Thread lock checked once | `/Unlock` the thread, post; `/Lock` again, post | First message stays; second deleted | **[auto]** `ThreadLockTests.UnlockingLiftsTheLock_AndLockingAgainRestoresIt`, `Settings.Tests ThreadLockTests.CacheInvalidatedAfterAddAndRemove` |
| XC-100 | 1-minute thread lock, a message already deleted under it | Wait for expiry, then post | Message stays (the expiry task removes the lock through `RemoveThreadLock`, which clears the cache) | Unlock row. **[auto]** `SettingsCacheTests.AnExpiredThreadLock_StopsDeletingPostsAtOnce` |
| XC-101 | Auto-pardon 30 years, `/SinLog` run once | `/ModSettings AutoPardon` 1 day; `/SinLog` for a user with an older sin | Old sin hidden | Setting row. **[auto]** `SettingsCacheTests.ShorteningAutoPardon_HidesOlderSinsInTheNextSinLog` |
| XC-102 | User command channel `#general`, `/Level` run once | Change it to `#spam`; `user1` runs `/Level` in both | Public in `#spam`, ephemeral in `#general` | Setting row. **[auto]** `SettingsCacheTests.MovingTheUserCommandChannel_AppliesToTheNextLevel` |
| XC-103 | Diagnostics ignore list read once | `/Diagnostics Ignore Add`, then `/Diagnostics Check` | Ignored item left out | **[auto]** `Settings.Tests DiagnosticsIgnoresTests` |
| XC-104 | Value cached | Edit the setting in SQL; check without restart, then restart | Stale until restart (or one day) | See GEN-171. **[auto]** `SettingsCacheTests.ADirectDatabaseEdit_IsStaleUntilARestart` |
| XC-105 | As XC-104: cache says A, SQL changed the row to B | Set the same value B through the command | **Expected:** the next read sees B. **Actual (from code):** the write is "not modified", so the cache is never cleared and A stays until restart (observation 4) | No new row. **[auto]** `SettingsCacheTests.SettingTheValueTheDatabaseHolds_ClearsAStaleCache` (skipped, KI-103) |
| XC-106 | Settings DB paused (`docker pause`) | `user1` sends a message; `admin` runs `/Level` | XP not awarded; `/Level` says "This module is disabled in this server." (observation 7). After `docker unpause` both work at once | Not cached. **[auto]** `Settings.Tests LevelingSettingsTests.ALookupDuringAnOutage_Fails_AndIsNotCached` |
| XC-107 | Change a setting in Test-A | Read it in Test-B | Unchanged in B | **[auto]** `MultiGuildTests.ModuleSettings_AreSeparate`, `Settings.Tests ChannelLockTests.CacheKey_TwoGuilds_NoCacheInterference` |

## G. Restart persistence

Settings, mutes, locks, XP, sins, messages and proxy links live in the database and survive a restart. The spam
tracker's points and the guild log queue live in memory and do not. There is no separate "tracker" feature in the
code any more; `CLAUDE.md` still mentions one under `Logging/`.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| XC-110 | Leveling settings, a log channel and a reward set through commands | Restart | `/LevelSettings View` and `/Rewards View` unchanged | **[auto]** `PersistenceTests.Settings_SurviveARestart` |
| XC-111 | Several modules toggled | Restart; `/Modules View` | Same states | See GEN-71. **[auto]** `RestartPersistenceTests.ModuleStates_SurviveARestart` |
| XC-112 | 3-minute mute on `user2` | Restart within the mute; wait | Role removed on schedule, DM and log sent | `MuteRemoved` row. See MOD-99, MOD-195. **[auto]** by seeded records in `MuteExpiryTests` |
| XC-113 | 2-minute mute; stop the bot for 5 minutes | Start | Mute lifted within the first ticks | See MOD-130 |
| XC-114 | 3-minute channel lock | Restart within the lock; wait | Overwrite reverted on schedule | See MOD-187. **[auto]** by seeded records in `LockExpiryTests` |
| XC-115 | Thread locked with no expiry yet | Restart; `user1` posts in the thread | Message deleted (cache rebuilt from DB) | none. **[auto]** `RestartPersistenceTests.ThreadLock_SurvivesARestart` |
| XC-116 | `user2` muted | Restart; `user2` leaves and rejoins | Mute role given back | See MOD-128. **[auto]** `RestartPersistenceTests.Mute_SurvivesARestart_AndIsGivenBackOnRejoin` |
| XC-117 | `user1` earned XP just now (timeout 3 min) | Restart; `user1` sends within the timeout | No XP (timeout read from `XpHistory.TimeOut`) | No new row. **[auto]** `RestartPersistenceTests.XpTimeout_SurvivesARestart` |
| XC-118 | `user1` muted by the spam filter once today | Restart; `user1` spams again | Spam points start from zero after the restart (intended), but the second mute is 2 minutes, not 1 (count read from `Sins`) | Second mute Sin. **[auto]** `RestartPersistenceTests.SpamMuteLength_CountsMutesFromBeforeARestart` |
| XC-119 | A logged message sent before the restart | Restart; delete it | Logged with the author mention | **[auto]** `MessageLogEventsTests` (L-3, after a restart) |
| XC-120 | Proxy linked (XC-30) | Restart; delete the proxy | Logged against `pk` | Link read from DB |
| XC-121 | Custom command learned | Restart; `!hello` and `/Command` | Both work | none. **[auto]** `RestartPersistenceTests.CustomCommand_SurvivesARestart` |
| XC-122 | Leaderboard view refreshed before restart; new XP after | Restart; `/Leaderboard` at once, then after 15 min | First shows the last refresh; second includes new XP | Materialized view refresh |
| XC-123 | Many log posts queued (for example a `/Purge All count:100` with the message log on) | Stop the bot right after the purge | Record how many posts arrive; queued posts in `GuildLog` are in memory | Rows saved; posts possibly lost |
| XC-124 | Bot removed from Test-B with an active mute and lock there | Restart, then re-invite | Mute and lock kept while out, handled after re-invite | See MOD-139, MOD-188 |

---

## Code observations

Things noticed while reading the source. "New" means not yet in `known-issues.md`.

1. **New (med). A missing `pluralkitApiEndpoint` breaks the delete log.** `PluralkitService.cs:37-38` counts PluralKit
   as configured when the token and user agent are set, without the endpoint. `GrimoireServiceRegistration.cs:247`
   throws `ArgumentException` for an empty endpoint when the named client is created, and `PluralkitService.cs:52`
   calls `CreateClient` outside the `try`. So every delete of a logged message under a minute old throws in
   `DeleteMessageEvent` and is not logged. Fix: check the endpoint in `_isConfigured`, or move `CreateClient` into
   the `try`. XC-07.
2. **New (low). `/SpamFilter` has no module gate.** `SpamFilterOverrideCommands.cs:13-14` has neither `RequireGuild`
   nor `RequireModuleEnabled(Module.AntiSpam)`. Every other settings command that changes a module's behaviour is
   refused while its module is off (for example `/ModSettings AutoPardon` since the latest commit). The missing
   `RequireGuild` has no effect, because no command is offered in DMs. XC-89.
3. **New (low). `/Unlock` doesn't declare Manage Roles.** `LockChannel.cs:19` requires Manage Channels and Manage
   Roles for the bot (KI-06 fix). `UnlockChannel.cs:18` requires only Manage Channels, yet reverting the overwrite
   needs Manage Roles. Without it the gate passes and Discord refuses. The lock stays recorded, so nothing is lost,
   but the reply differs from `/Lock`'s. XC-61.
4. **New (low). A "not modified" write never clears a stale cache.** `SettingsModule.cs:89` compares the new value
   with the newest DB row and returns `NotModified` before `RemoveAsync`. `SettingsModule.MessageLog.ChannelOverrides.cs:82`
   and `SettingsModule.Moderation.SpamFilterOverrides.cs:83` compare with the cached value instead. After a direct SQL
   edit, setting the value the DB already holds leaves the old cached value in force until restart. Only reachable
   with SQL edits. XC-105.
5. **New (low, decide). `/Log Message View` and `/Log User View` stay module-gated.** The latest commit lets
   `/ModSettings View` and `/LevelSettings View` run while their module is off, because they show the module state.
   The two Log Views (`LogSettingsCommands.cs:30`, `:37` gate the whole group) don't show the state, so they stay
   refused. Either add a Module Enabled line and move the gate to the setters, or leave as is. XC-73.
6. **New (low). Bulk delete logs a proxy under the webhook.** `BulkMessageDeletedEvent.cs:41-60` reads `m.UserId`
   and that user's username, and never reads `ProxiedMessageLink`. A purge of proxied messages shows the webhook id
   with no name. The single delete and edit logs show the member behind the proxy. XC-45.
7. **New (low). A settings lookup failure reads as "module disabled".** `RequireModuleEnabledAttribute.cs:27-29`
   turns a failed lookup into false, so during a settings DB outage every gated command says "This module is disabled
   in this server." Events stop silently in the same way. The failure isn't cached, so it recovers at once. A
   distinct message would make outages easier to tell apart. XC-106.
8. **New (low). Two alert services start before migrations.** `GrimoireServiceRegistration.cs:224-226` registers
   `WarningFlushService` and `DailyWarningReportService` before `DiscordStartService`, so a migration over about two
   minutes can overlap their first DB queries. Failures are caught: at worst one dropped warning batch or one
   `BackgroundTaskFailure` warning. XC-28.
9. **New (low, decide). Moderation off also stops the spam filter.** `SettingsModule.Moderation.Mutes.cs:22-24`
   returns no mute role while Moderation is off, and `SpamEvents` then returns before it deletes the spam. With
   AntiSpam on and Moderation off, spam is detected and nothing at all happens. Either document AntiSpam as needing
   Moderation, or still delete the message. XC-83.
10. **Existing (docs, review I-18). `CLAUDE.md` is stale.** It names `pluralkitEndpoint` (the code reads
    `pluralkitApiEndpoint` and also needs `pluralkitUserAgent`), says to copy `appsettings.example.json`, which doesn't
    exist, and lists a "tracker" under `Logging/` that no longer exists.
11. **Existing (KI-52).** Joins in the first moments after startup can be logged without their invite. XC-10.
12. **Intended, not a bug.** `/Warn`, `/Lock`, `/Unlock` and `/Purge` check permissions in the channel, so channel
    moderators can use them (KI-17 for `/Warn`). The ManageMessages gates on `/Ban`, `/Unban`, `/Mute`, `/Unmute`,
    `/Lock` and `/Unlock` are legacy and intentional (area 02). Most commands are shown to every member and refused
    bot-side; A-36 calls that a design choice to review.

## Findings

Record failures here as `ID - observed - expected - severity`.

| ID | Observed | Expected | Severity | Follow-up |
|----|----------|----------|----------|-----------|
|    |          |          |          |           |

## Automated coverage

- `Shared/StartupMigrationTests`: XC-01, XC-20, XC-21.
- `Logging/PluralKitTests`: XC-06, XC-30, XC-31, XC-33 to XC-36, XC-38.
- `Shared/CommandMatrixTests`: the registration snapshot under D, XC-50, and XC-70 for 35 commands.
- `Shared/CommandChecksTests`, `Shared/ModuleCommandsTests`: XC-86, XC-90, part of XC-77.
- `ModSettingsTests`, `LevelSettingsTests`: XC-71, XC-72.
- `Shared/PersistenceTests`: XC-92 to XC-95, XC-110.
- `Shared/MultiGuildTests`, `Shared/GuildSyncAndInviteTests`: XC-09, XC-11, XC-107.
- `Moderation/ThreadLockTests`, `MuteExpiryTests`, `LockExpiryTests`, `SpamFilterTests`: XC-63, XC-80, XC-81, XC-84,
  XC-96, XC-99, and XC-112 and XC-114 by seeded records.
- `Grimoire.Settings.Tests`: the cache cases in XC-90, XC-96, XC-99, XC-103, XC-106, XC-107.
- `Shared/ChannelPermissionTests` (A-51, interactions carry channel-resolved permissions): XC-54 to XC-62.
- `Shared/ModuleToggleTests`: XC-74 to XC-76, XC-78, XC-79, XC-82, XC-85, XC-87, XC-88, XC-91.
- `Shared/SettingsCacheTests`: XC-97, XC-98, XC-100 to XC-102, XC-104, XC-105 (skipped, KI-103).
- `Shared/RestartPersistenceTests`: XC-111, XC-115 to XC-118, XC-121.

Candidates for new tests:

- XC-07: start with token and user agent but no endpoint, delete a fresh message, expect a delete log.
- XC-37: five proxies in one second through `FakePluralKit`; count false delete logs.
- XC-40 to XC-43: proxied messages through `SendMessage` with a webhook author, for leveling and the spam filter.
- XC-45: purge proxied messages and check the bulk log's author.
- XC-61: restart with Manage Channels only, `/Unlock` a locked channel.
- XC-70 gaps: add the missing module-gated commands to `CommandMatrixTests.ModuleGated`.
- XC-83, XC-89: AntiSpam on with Moderation off, and `/SpamFilter Override` with AntiSpam off.
- XC-105: write a row by SQL, set the same value through the command, read through the service.
- XC-115, XC-117, XC-118: restart the test bot on the same database and check thread locks, XP timeout and spam
  escalation.

Manual only: XC-02 to XC-05 (process start-up), XC-22 to XC-27 (real data, concurrency), XC-52, XC-53 (Discord's
Integrations settings), XC-123 (shutdown timing).
