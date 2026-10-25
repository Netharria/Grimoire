# 01 - General Settings, Modules, UserInfo, Purge

Scope: `/GeneralSettings` (View, ModLogChannel, UserCommands), `/Modules` (View, Set), `/UserInfo`,
`/Purge` (All, User), and the `RequireModuleEnabled` / `RequireUserGuildPermissions` context checks.

Source: `Grimoire/Features/Shared/Commands/`, `Grimoire/Features/Shared/Attributes/`,
`Grimoire/Extensions/DiscordChannelExtensions.cs` (`PurgeMessagesAsync`).

Do the standard preflight from `00-environment.md` first. Accounts: `owner`, `admin`, `mod`
(no ManageGuild), `user1`, `user2`. "Settings DB" means the guild settings tables owned by
`GrimoireSettingsDbContext`; settings are cached, so verify through `/GeneralSettings View` or
`/Modules View` as well as SQL.

## Behaviour summary (what the code does)

| Command | Permission gate | Module gate | Response | Guild log (mod log) |
|---------|-----------------|-------------|----------|---------------------|
| `/GeneralSettings View` | ManageGuild (owner/admin bypass) | none | ephemeral | no |
| `/GeneralSettings ModLogChannel option [channel]` | ManageGuild | none | public | yes |
| `/GeneralSettings UserCommands option [channel]` | ManageGuild | none | public | yes |
| `/Modules View` | ManageGuild | none | public | no |
| `/Modules Set module enable` | ManageGuild | none | public | yes |
| `/UserInfo user` | ManageGuild | none (sections gated per module) | public | no |
| `/Purge All count` | ManageMessages (`RequirePermissions`) | none | ephemeral | no |
| `/Purge User user count` | ManageMessages | none | ephemeral | no |

`ChannelOption` = Off / Current Channel / Select Channel. `/Modules Set` choices: Leveling, UserLog,
Moderation, MessageLog, Commands, AntiSpam (`General` is always on and is not toggleable).

---

## A. Context checks

### A1. RequireUserGuildPermissions

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| GEN-01 | none | `owner` runs `/GeneralSettings View` (owner without ManageGuild role is fine; owner id is bypassed) | Settings embed shown | none |
| GEN-02 | none | `admin` (Administrator, no explicit ManageGuild) runs `/GeneralSettings View` | Allowed via Administrator bypass | none |
| GEN-03 | `mod` has no ManageGuild | `mod` runs `/GeneralSettings View` | Error: "You do not have the required permissions to use this command." | No settings change |
| GEN-04 | none | `user1` runs `/Modules View` | Same permission error | none |
| GEN-05 | Give `user1` a role with only ManageGuild | `user1` runs `/Modules View` | Allowed (exact permission match works) | none |
| GEN-06 | Remove the role from GEN-05 | `user1` runs `/Modules View` again | Denied again (permission read live, not cached) | none |
| GEN-07 | Bot DM | Run `/GeneralSettings View` in a DM with the bot | Not available / "only be used in a server" (`RequireGuild`) | none |
| GEN-08 | `mod` | `mod` runs `/UserInfo user:user1` | Denied (UserInfo needs ManageGuild, not just Ban/Kick/ManageMessages). **Confirm this is intended.** | none |

### A2. RequireModuleEnabled (sampled across modules)

Use one command per gated module. Disable the module with `/Modules Set`, run, re-enable, run.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| GEN-10 | Moderation disabled | `admin` runs `/Ban` (any valid target) | Error "This module is disabled in this server."; nobody banned | no Sins row |
| GEN-11 | Leveling disabled | `user1` runs `/Level` | Same disabled message | none |
| GEN-12 | Commands disabled | `admin` runs `/Commands Stats` | Same disabled message | none |
| GEN-13 | MessageLog disabled | `admin` runs `/Log Message View` | Same disabled message | none |
| GEN-14 | UserLog disabled | `admin` runs `/Log User View` | Same disabled message | none |
| GEN-15 | Module re-enabled after GEN-10..14 | Re-run each command | Commands work again immediately (no bot restart) | Settings DB shows enabled |
| GEN-16 | Test-B: Moderation enabled, Test-A: disabled | Run `/Ban` in Test-B | Works in B, blocked in A (per-guild isolation) | Only Test-A row differs |
| GEN-17 | Any module disabled | Run `/GeneralSettings View`, `/Modules View`, `/Modules Set`, `/UserInfo`, `/Purge All` | Still work (these are not module gated) | none |

---

## B. `/GeneralSettings`

### B1. View

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| GEN-20 | Fresh guild, no channels set | `admin` runs `/GeneralSettings View` | Ephemeral embed "General Settings": `Moderation Log: None`, `User Command Channel: None` | none |
| GEN-21 | Mod log = `#modlog`, user cmd = `#general` | Run View | Ephemeral, shows `#modlog` and `#general` as clickable mentions | none |
| GEN-22 | Configured in Test-A only | Run View in Test-B | Shows `None` / `None` (no leakage) | none |
| GEN-23 | Mod log channel deleted in Discord after being set | Run View | Note actual behaviour (likely shows an unresolved `<#id>` mention); no crash | none |

### B2. ModLogChannel

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| GEN-30 | none | `/GeneralSettings ModLogChannel option:Select Channel channel:#modlog` | Public reply "Updated the moderation log to #modlog" | Settings DB: mod log channel = `#modlog` id, modified-by = admin id |
| GEN-31 | none | Run in `#locktest` with `option:Current Channel` | Reply names `#locktest`; setting updated | Setting = `#locktest` id |
| GEN-32 | Mod log set | `option:Off` | Reply "`@admin` disabled the moderation log to " (**trailing "to" with empty mention is cosmetic bug**) | Setting cleared (null) |
| GEN-33 | Mod log set to `#modlog` | Change to `#userlog` | Reply OK; **the "updated" log message is sent to the new channel** (`#userlog`) since the log reads the new setting | Setting = `#userlog` |
| GEN-34 | Mod log set | Set `option:Off` | Reply sent; no log message posted anywhere (log is disabled by the time it is sent) | Setting null |
| GEN-35 | `#private` hidden from bot (deny View/Send for bot) | Select `#private` | Red error "`@Grimoire` doesn't have permission to send messages in that channel."; setting unchanged | Setting unchanged |
| GEN-36 | Bot has Send but not Embed Links in channel (optional) | Select that channel | Setting accepted; observe whether logs arrive (embeds need Embed Links) | Setting updated |
| GEN-37 | none | `option:Select Channel` with no `channel` argument | Ephemeral red error "Select a channel when using the Select Channel option." (`GetChannelOption` returns an invalid `Validation`); no hung "thinking" state | Setting unchanged; console logs the rejected command |
| GEN-38 | none | `option:Off` while also passing `channel:#general` | Off wins, channel ignored | Setting null |
| GEN-39 | none | `option:Current Channel` and a forum/thread channel | Setting accepts thread/forum or shows a sensible error; note result | note |
| GEN-40 | `mod` | Run any ModLogChannel variant | Permission error (see GEN-03) | Unchanged |

### B3. UserCommands (user command channel)

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| GEN-50 | Mod log set to `#modlog` | `/GeneralSettings UserCommands option:Select Channel channel:#general` | Reply "Updated the User Command Channel to #general"; mod log entry "updated the User Command Channel" | Setting = `#general` id |
| GEN-51 | Set | `option:Current Channel` in `#spam` | Setting = `#spam` | Updated |
| GEN-52 | Set | `option:Off` | Reply "Disabled the User Command Channel."; mod log entry posted | Setting null |
| GEN-53 | none | Select a channel the bot cannot send in (`#private`) | **No bot-permission check here (unlike ModLogChannel)**: setting is accepted. Record this inconsistency. | Setting = `#private` |
| GEN-54 | none | `option:Select Channel` without `channel` | Same validation error as GEN-37 | Unchanged |
| GEN-55 | Mod log not set | Change the setting | Reply works; no mod log message; no error | Updated |
| GEN-56 | Leveling enabled, user cmd channel = `#general` | `user1` runs `/Level` in `#general`, then in `#spam` | `#general`: public reply. `#spam`: ephemeral reply (only `user1` sees it) | none |
| GEN-57 | Same as GEN-56 | `mod` (ManageMessages) runs `/Level` in `#spam` | Public reply (ManageMessages bypasses the channel restriction) | none |
| GEN-58 | User cmd channel = Off | `user1` runs `/Level` anywhere | Ephemeral everywhere | none |
| GEN-59 | Same as GEN-56 | Repeat with `/Leaderboard` | Same public/ephemeral split | none |
| GEN-60 | `mod` | Run any UserCommands variant | Permission error | Unchanged |

---

## C. `/Modules`

### C1. View

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| GEN-70 | Defaults for a freshly configured guild | `/Modules View` | Public embed "Current states of modules." listing Leveling, User Log, Message Log, Moderation, Commands, Anti Spam each True/False | none |
| GEN-71 | Toggle several via SQL, then restart bot | View | Matches DB (confirms restart persistence) | none |
| GEN-72 | Different states in A and B | View in each | Each shows own guild's state | none |
| GEN-73 | Note | View output | `General` is not listed (always on, by design). | none |

### C2. Set

Repeat GEN-80..83 for each of the six choices (Leveling, UserLog, Moderation, MessageLog, Commands, AntiSpam).

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| GEN-80 | Module enabled | `/Modules Set module:X enable:False` | Reply "Disabled X"; mod log message "`admin` Disabled X" | Module state false, modified-by = admin id |
| GEN-81 | Module disabled | `enable:True` | Reply "Enabled X"; mod log entry | Module state true |
| GEN-82 | Module already in target state | Set to the same state | Reply still says Enabled/Disabled; no error; log entry still posted (no idempotence message) | Unchanged value |
| GEN-83 | After GEN-80 | `/Modules View` immediately (no restart) | Shows False (cache invalidated on write) | Matches |
| GEN-84 | Mod log unset | Toggle any module | Reply OK, no log message, no error | Updated |
| GEN-85 | Toggle in Test-A | Check Test-B | B unchanged | Only A row changed |
| GEN-86 | Moderation disabled | Run `/Modules Set module:Moderation enable:True` | Works (Modules is not module gated, no lock-out) | Enabled |
| GEN-87 | `mod` | Run `/Modules Set` | Permission error | Unchanged |
| GEN-88 | Behavioural side effects | Disable Leveling, send messages as `user1`; disable UserLog, change nickname; disable MessageLog, send/edit/delete | No XP row, no nickname history, no message log rows while disabled. (Detail lives in areas 05/06; here just confirm the flag is honoured.) | No new rows in `XpHistory`, `NicknameHistory`, message log tables |
| GEN-90 | AntiSpam enabled | `/Modules Set module:AntiSpam enable:False`, then trip the spam filter as `user1` | Reply "Disabled AntiSpam"; `/Modules View` shows Anti Spam False. **Spam handling should stop; see code observation 5, `SpamEvents` may not read this flag yet.** | AntiSpam setting false; check for a mute row |
| GEN-89 | Mod log uses `ctx.User.Username`, other commands use mention | Compare the mod log line to GEN-30 | Cosmetic: username text, not a mention | none |

---

## D. `/UserInfo`

Embed always contains Author ("User info for {name}"), thumbnail (avatar), "Joined On".
Extra fields appear per enabled module: UserLog (Usernames, Nicknames), Leveling (Level, Can Gain Xp, Earned Rewards),
Moderation (Warns, Mutes, Bans).

Seed data first: give `user1` a few nicknames/renames (UserLog on), some XP (Leveling on), 2 warns + 1 mute + 1 ban
history (Moderation on) and a level reward at a level `user1` has reached.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| GEN-100 | All modules enabled, seed data present | `admin` runs `/UserInfo user:user1` | Embed with all three sections; Joined On is a Discord timestamp; colour = member role colour | Compare to `UsernameHistory`, `NicknameHistory`, `XpHistory`, `Sins` |
| GEN-101 | Same | Check Usernames / Nicknames | Newest first, max 3 each; `Unknown Usernames` / `Unknown Nicknames` when empty | Matches history tables, null nicknames excluded |
| GEN-102 | Same | Check Level | Level matches `/Level user1`; sum of `RawXp` for this guild only | Compare `XpHistory` sum |
| GEN-103 | `user1` has XP in Test-B only | UserInfo in Test-A | Test-A level ignores Test-B XP | none |
| GEN-104 | `user1` has an ignored role / ignored-user entry | Run UserInfo | `Can Gain Xp: No`; remove ignore, `Yes` | none |
| GEN-105 | `user1` reached a level with a reward | Run UserInfo | **Expected: reward role mention listed. Code observation: condition is inverted (see below), so you'll see the mention list when there are NO rewards and "None" when there are.** Record actual. | Compare rewards config |
| GEN-106 | `user1` below all reward levels | Run UserInfo | Earned Rewards shows an empty value (empty join) rather than "None" per the inverted condition. Record actual; an empty embed field value may make Discord reject the message. | none |
| GEN-107 | Moderation on | Warns/Mutes/Bans counts | Counts match `Sins` for that user and guild, within the auto-pardon window only | Pardoned sins should not count if the code excludes them; verify against `Sins` |
| GEN-108 | Auto-pardon set short (e.g. 1 day) and an old sin exists | Run UserInfo | Old sin excluded from counts | `Sins.SinOn` older than window |
| GEN-109 | UserLog disabled | Run UserInfo | No Usernames / Nicknames fields | none |
| GEN-110 | Leveling disabled | Run UserInfo | No Level / Can Gain Xp / Earned Rewards fields | none |
| GEN-111 | Moderation disabled | Run UserInfo | No Warns / Mutes / Bans fields | none |
| GEN-112 | All three disabled | Run UserInfo | Only author, thumbnail, Joined On | none |
| GEN-113 | `user1` has no history at all | Run UserInfo | Zeros / Unknown values, no exceptions | none |
| GEN-114 | Target has a custom server avatar | Run UserInfo | Thumbnail shows the guild avatar; target with no avatar shows default avatar | none |
| GEN-115 | Target has no coloured role | Run UserInfo | Default/none colour, no crash | none |
| GEN-116 | Target is the bot | Run UserInfo on the bot | Works, no exception | none |
| GEN-117 | Target is a user who left the guild (or was banned) | Pass the user through the user picker (or by id) | "Joined On: Not On Server", user colour/blurple fallback, default avatar path, no roles (Can Gain Xp evaluated without roles) | none |
| GEN-118 | Target ID of a user who never joined the server (any Discord user) | Run UserInfo | Same "Not On Server" path | none |
| GEN-119 | Target is `pk` PluralKit system member (optional) | Run UserInfo on the proxy webhook user | Note behaviour; no crash | none |
| GEN-120 | `user1`, `mod` | Run UserInfo | Permission error (GEN-03/08) | none |
| GEN-121 | Long histories | User with 20+ renames | Only 3 shown | none |

---

## E. `/Purge`

`[RequirePermissions(ManageMessages)]` applies to the caller (and possibly the bot; note which by removing the bot's
Manage Messages in GEN-140). `count` is limited to 0..1000 by the slash command; the reply is ephemeral.
Only messages newer than 14 days are eligible.

Prepare `#spam`: helper to post N messages as `user1` and `user2` alternating; keep the message counts below.

### E1. Purge All

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| GEN-130 | 10 messages in `#spam` | `mod` runs `/Purge All count:5` | 5 newest messages deleted; ephemeral "Purged 5 messages" (green) | Audit log: bulk message delete, reason "`mod` purged these messages." |
| GEN-131 | 3 messages | `count:1` | Ephemeral "Purged 1 message" (singular); exactly one deletion (single delete path) | Audit log entry for one message |
| GEN-132 | 10 messages | `count:0` | "Purged 0 messages", nothing deleted (0 is accepted by the range) | none |
| GEN-133 | 3 messages | `count:50` (more than exist) | Deletes all 3, "Purged 3 messages"; no error | none |
| GEN-134 | Empty channel | `count:10` | "Purged 0 messages" | none |
| GEN-135 | 250 messages | `count:250` | Deleted in chunks of 100; "Purged 250 messages" | Audit log: multiple bulk deletes |
| GEN-136 | 101 messages | `count:101` | Chunks 100 + 1: the trailing single-message chunk goes through the single-delete path (bulk delete requires 2-100); all 101 gone, no error | Console: check for exceptions; all 101 gone |
| GEN-137 | Exactly 100 | `count:100` | Deleted in one chunk | none |
| GEN-138 | Messages older than 14 days exist (needs a channel with old history, or import) | Purge more than the number of recent messages | Only <14-day messages deleted; older stay; message count reflects only deleted | none |
| GEN-139 | none | `count:-1` and `count:1001` | Discord client rejects out-of-range values before sending | none |
| GEN-140 | Remove bot's Manage Messages in `#spam` | Run purge | Nothing deleted; user sees a permission error or Discord error. Record exact behaviour and whether the interaction hangs on "thinking". | Console error |
| GEN-141 | Bot has no access to `#private` | Run purge there as `admin` | Error, no hang | Console error |
| GEN-142 | `user1` (no ManageMessages) | Run `/Purge All count:5` | Permission error; no messages deleted | none |
| GEN-143 | `user1` given ManageMessages only in `#spam` via channel override | Run in `#spam`, then `#general` | Works in `#spam`; check `#general` (user has no ManageMessages there): denied if permissions are resolved per-channel, allowed if resolved guild-wide. Record. | none |
| GEN-144 | Pinned message present | Purge including it | Record whether pinned messages are deleted (no pin exclusion in code) | none |
| GEN-145 | Thread/forum post channel | Purge inside a thread | Works or graceful error | none |
| GEN-146 | Messages currently arriving (spam another account while purging) | Run purge | Deleted set is a snapshot from when the command started; no crash | none |
| GEN-147 | Message Log enabled | Purge 10 messages | Message log (`#messagelog`) records the bulk delete (cross-check in area 05); Purge itself posts nothing to the mod log (**no purge audit in the mod log**) | Message log rows for deleted messages |
| GEN-148 | Large history (channel with thousands of messages) | `count:10` | Observe latency: `PurgeMessagesAsync` calls `GetMessagesAsync()` with no limit and sorts before `Take`, so it may enumerate the whole channel history before deleting. Record time and whether the interaction (15 min token) survives. | none |

### E2. Purge User

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| GEN-150 | 10 alternating messages by `user1` / `user2` | `/Purge User user:user1 count:3` | Only 3 newest `user1` messages deleted; `user2` untouched; "Purged 3 messages" | Audit reason "`@mod` purged the messages of `@user1`." |
| GEN-151 | Same | `count:100` (more than `user1` has) | All `user1` messages deleted, `user2` untouched | none |
| GEN-152 | `user1` has no messages in channel | Run | "Purged 0 messages" | none |
| GEN-153 | Target is a user who left the server but has messages | Run with that user | Deletes their messages (author compared by user identity) | none |
| GEN-154 | Target is a bot (Grimoire's own replies) | Run with the bot | Bot messages deleted; ephemeral replies unaffected | none |
| GEN-155 | Message by a PluralKit proxied webhook | Purge the underlying account (`pk`) | Proxied webhook messages are **not** matched (different author). Record. | none |
| GEN-156 | `user1` | Run `/Purge User` | Permission error | none |
| GEN-157 | 1 matching message | `count:1` | Single-delete path, "Purged 1 message" | none |
| GEN-158 | Matching messages older than 14 days mixed in | Run | Only recent ones removed; note that scanning stops at the 14-day boundary | none |

---

## F. Cross-checks for this area

| ID | Precondition | Action | Expected | Expected DB / log |
|----|--------------|--------|----------|-------------------|
| GEN-170 | Change a setting in Test-A | Restart the bot | Value persists (`/GeneralSettings View`, `/Modules View`) | Settings DB |
| GEN-171 | Edit a setting directly in SQL | Do not restart, run View | Stale value (cache), then correct after restart (documents the cache behaviour from `00-environment.md`) | Settings DB |
| GEN-172 | Bot removed from Test-B then re-added | Run `/Modules View` in Test-B | Defaults or previous state; no exception (also see area 08 `GuildAdded`) | Settings DB |
| GEN-173 | Two admins run `/Modules Set` on the same module at the same moment | Both complete | Final state equals the later write; no error | Settings DB single row |

---

## Code observations

Things noticed while reading the source. Items marked **fixed** were resolved after the first draft; keep the
matching cases to confirm the fix.

1. **`UserInfo` Earned Rewards condition was inverted** (**fixed**: now `Length == 0 ? "None" : join`).
   Covered by GEN-105, GEN-106.
2. **`/GeneralSettings ModLogChannel option:Off`** reply reads "disabled the moderation log to " (dangling "to"
   with an empty mention). Cosmetic, open.
3. **`/GeneralSettings UserCommands` lacks the bot Send Messages permission check** that `ModLogChannel` has.
   May be fine, since replies are interaction responses. Inconsistent, open (GEN-53).
4. **`SelectChannel` without a channel** used to throw `ArgumentNullException`. **Fixed**: `GetChannelOption` now
   returns `Validation<DiscordChannel?>` and all six callers reply with an ephemeral error (GEN-37, GEN-54; also
   spot check `/Log Message Set`, `/Log User Set`, `/LevelSettings LogSet`, `/ModSettings PublicBanLog`).
5. **AntiSpam toggle**: `/Modules Set` and `/Modules View` now include AntiSpam, but `SpamEvents` does not
   check `Module.AntiSpam`, so toggling it likely has no effect on spam handling yet (GEN-90).
6. **`/Modules Set` mod log message uses `ctx.User.Username`** while the other settings commands use `Mention`.
7. **`PurgeMessagesAsync` enumerates the entire channel history** (`GetMessagesAsync()` with no limit, then
   `OrderByDescending`, which buffers everything). Accepted as a known limitation: no sensible filter exists;
   GEN-148 records the latency so it is documented.
8. **Bulk-delete chunking**: a trailing chunk of one message is now deleted individually (**fixed**, GEN-136).
   Still open: `DeleteMessagesAsync` fails the whole chunk if any message aged past 14 days between fetch and call.
9. **Purge has no mod-log entry**, and `count:0` is accepted and does nothing (the minimum should probably be 1).
   `RequirePermissions(ManageMessages)` checks both the caller and the bot (confirmed), so GEN-140 should report
   a permission error.
10. **`UserInfo` requires ManageGuild**, which the `mod` role does not have. That makes it unusable for regular
    moderators; ManageMessages/BanMembers might be the intended gate.
11. **`GetLevel` has both `[RequireModuleEnabled(Leveling)]` and an in-body module check.** The in-body check
    (different wording) is unreachable when the attribute is present. Compare the error text in GEN-11.

## Findings

Record failures here as `ID - observed - expected - severity`.

| ID | Observed | Expected | Severity | Follow-up |
|----|----------|----------|----------|-----------|
|    |          |          |          |           |

## Promote to automated tests **[auto]**

### Automated

Integration tests in `Grimoire.Tests/Integration/Shared/` now cover these cases. They no longer need a manual run.

- `CommandChecksTests`: GEN-01 to GEN-07 (GEN-07 through the registered DM permission), GEN-10 to GEN-15 and GEN-17.
- `ModuleCommandsTests`: GEN-70, GEN-73, GEN-80 to GEN-84, GEN-86, GEN-87 and GEN-89, plus the message-log half of
  GEN-88.
- `GeneralSettingsCommandsTests`: GEN-20, 21, 23, 30 to 35, 37 to 40, 50 to 55 and 60. GEN-32's wording (KI-22) and
  GEN-37's ephemeral flag (KI-21) are skipped tests.
- `UserInfoCommandTests`: GEN-08, 100 to 118, 120 and 121, with GEN-103 using XP rows from another guild id. The User
  Log section (GEN-100, 101, 109) is skipped because the command throws (KI-23). Member join date and role-based
  ignores (GEN-100, 104, 114, 115, 117) are skipped because the member is never resolved (KI-19).
- `PurgeCommandsTests`: GEN-130 to 138, 140, 142, 145, 147 (no mod-log entry), 150 to 154, 156 to 158. Over 100
  messages (GEN-135, 136) is skipped because the scan stops at 100 (KI-24).
- Still manual or elsewhere: GEN-16, GEN-22, GEN-72 and GEN-85 (two guilds, A-35), GEN-36 (Embed
  Links, manual), GEN-56 to GEN-59 (A-26), GEN-71 (restart, A-37), GEN-90 (spam filter, A-18), GEN-139 (enforced by
  Discord's client), GEN-141, 143, 144, 146 and 148 (manual), GEN-155 (A-34), and GEN-170 to GEN-173 (A-37).

### Candidates

- `RequireUserGuildPermissionsCheck`: owner bypass, Administrator bypass, exact permission, missing permission,
  null guild, null member (GEN-01..07). Pure logic, NSubstitute-friendly.
- `RequireModuleEnabledCheck`: enabled, disabled, and null guild (GEN-10..16), using a real settings DB.
- `ModuleCommands.MapToModule` mapping for every `ModuleArguments` value (including AntiSpam).
- `GetChannelOption`: Off / Current / Select / Select-with-null returns `Invalid` (GEN-37).
- `PurgeMessagesAsync`: chunking of 1, 2, 100, 101 messages (trailing 1-message chunk must not hit bulk delete); count larger than available; filter; 14-day cut-off
  (GEN-131..138, 150..158) with a substituted channel.
- `UserInfoCommands` field builders for the Earned Rewards inversion and empty-history cases (GEN-105, 106, 113).
