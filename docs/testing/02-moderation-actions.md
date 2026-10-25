# 02 - Moderation Actions

Scope: `/Ban`, `/Unban`, `/Kick`, `/Warn`, `/Mute`, `/Unmute`, `/Lock`, `/Unlock`; `/Mutes` (Create, Set, View,
Refresh); the ban added/removed events; `UserJoinedWhileMuted`; `MuteBackgroundTasks` and `LockBackgroundTasks`
expiry; `LockedTreadEvents`.

Source: `Grimoire/Features/Moderation/{Ban,Kick,Warn,Mute,Lock}/`, `Grimoire/Utilities/ExpiredItemTriage.cs`,
`Grimoire/Utilities/GenericBackgroundService.cs`, `Grimoire.Settings/Services/SettingsModule.Moderation.{Mutes,Locks}.cs`.
Sins admin (`/SinLog`, `/Pardon`, ...) is area 03; the `/Diagnostics` commands are area 04.

Do the standard preflight from `00-environment.md` first. Also:

- `/GeneralSettings ModLogChannel` set to `#modlog` (most cases assert a mod-log entry).
- Accounts: `owner`, `admin`, `mod`, `user1`, `user2`, `higher`. Give `user1` a spare "Perm" role you can edit to
  get exact-permission cases.
- Bot role sits above `admin`, `mod` and the Muted role (see role hierarchy in `00`). The bot needs Ban, Kick,
  ManageRoles, ManageChannels, ManageMessages and View Audit Log for the happy paths; remove one at a time for the
  bot-lacks-permission cases and restore it afterwards.
- "Sins" = `Sins` and `SinReasonHistory` in `GrimoireDbContext`. "Mutes", "ChannelLocks" and "ThreadLocks" are in
  `GrimoireSettingsDbContext` (append-only event rows: the latest row per user/channel is the current state, so
  "unmute" adds a `MuteRemoved` row rather than deleting). Table names assume EF defaults; adjust to your schema.
- Have a disposable account (`user2`) you can ban, kick and re-invite repeatedly. Keep an invite link handy.

## Behaviour summary (what the code does)

| Command | User gate | Bot gate | Records | Reply | Mod log |
|---------|-----------|----------|---------|-------|---------|
| `/Ban user [reason] [delete_messages] [delete_days]` | **ManageMessages** | BanMembers | Sin (Ban) + reason | public embed | via `BanAddedEvent` |
| `/Unban user` | **ManageMessages** | BanMembers | nothing | public embed | via `BanRemovedEvent` |
| `/Kick member [reason]` | KickMembers | KickMembers | Sin (Kick) + reason | public embed | yes (same embed) |
| `/Warn user reason` | ManageMessages (`RequirePermissions`) | none | Sin (Warn) + reason | public embed, then DM | yes |
| `/Mute user duration_type duration_amount [reason]` | ManageMessages | ManageRoles | Sin (Mute) + `Mutes` row | public embed, then DM | yes |
| `/Unmute user` | ManageMessages | ManageRoles | `MuteRemoved` row | public embed, then DM | yes |
| `/Lock duration_type duration_amount [channel] [reason]` | ManageMessages (`RequirePermissions`) | ManageChannels | `ChannelLocks` / `ThreadLocks` row | public text | yes |
| `/Unlock [channel]` | ManageMessages | ManageChannels | unlock row | public text | yes |
| `/Mutes Create` | ManageGuild | ManageRoles | mute role setting | several public messages + diagnostics follow-up | yes |
| `/Mutes Set role` | ManageGuild | ManageRoles | mute role setting | public text | yes |
| `/Mutes View` | ManageGuild | ManageRoles | none | ephemeral embed | no |
| `/Mutes Refresh` | ManageGuild | ManageRoles | none | several messages + diagnostics follow-up | no |

All of these are `RequireGuild` and `RequireModuleEnabled(Moderation)`. `DurationType` is Minutes / Hours / Days.
`duration_amount` has a minimum of 0, not 1. Ban, Kick, Warn and Mute all `DeferResponseAsync` first, so a check
failure shows as the yellow embed rather than an interaction error.

Background tasks: `MuteBackgroundTasks` and `LockBackgroundTasks` tick every 5 s (plus a random start-up delay of up
to 5 s). A failed item is retried with backoff: 30 s, then doubling up to 15 min (`RetryBackoff`). Use 1-minute
durations and allow about 10 s after expiry before asserting.

---

## A. `/Ban`

### A1. Happy path and options

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-01 | `user2` in guild, has DMs open | `admin` runs `/Ban user:user2 reason:"spam"` | `user2` receives DM "You have been banned from Test-A for spam" with author "Ban ID n". Public embed "Banned" with User, Sin Id, Moderator, Reason. `user2` appears in Server Settings > Bans with audit-log reason "spam" | `Sins`: one row, type Ban, actor = admin. `SinReasonHistory`: one row "spam". `#modlog` gets a "Banned" embed **once** (from `BanAddedEvent`, which reuses the command's sin because it is under 30 s old) with Mod field = admin |
| MOD-02 | Re-invite and unban `user2` first (MOD-33) | `/Ban user:user2` with no reason | Embed Reason "None"; DM has no "for ..." text | `Sins` row, **no** `SinReasonHistory` row. Mod-log Reason "None" |
| MOD-03 | `user2` has sent messages in `#general` in the last hour | `/Ban user:user2 delete_messages:true` (delete_days omitted) | Banned; `user2`'s messages from the last 7 days are removed from all channels | Sin row |
| MOD-04 | as MOD-03 | `/Ban ... delete_messages:true delete_days:1`; seed one message older than 1 day if possible | Only the last day removed | Sin row |
| MOD-05 | as MOD-03 | `/Ban ... delete_messages:false delete_days:7` | **No** messages removed (delete_days ignored while the flag is false) | Sin row |
| MOD-06 | as MOD-03 | `/Ban ... delete_messages:true delete_days:0` | Banned; no messages removed | Sin row |
| MOD-07 | none | `/Ban ... delete_days:8` and `-1` | Discord client rejects (0-7 range) | none |
| MOD-08 | none | `/Ban ... reason:` with 1001 characters | Discord client rejects (max 1000) | none |
| MOD-09 | Ban a user who is **not** in the guild (pick from the user picker, e.g. a user who left) | `/Ban user:<non-member>` | Banned, no DM attempted, embed shown | Sin row; mod-log entry |
| MOD-10 | User already banned | `/Ban` the same user again | Discord treats it as success; embed shown | A **second** Sin row (no duplicate check). Mod-log entry: the 30 s window applies, so `BanAddedEvent` may not fire at all for an already-banned user (no gateway event). Record which happens |
| MOD-11 | `user2` has DMs closed / has blocked the bot | `/Ban user:user2` | Ban still succeeds and the embed is shown | Debug log "Was not able to send a direct message" only when the failure is **not** `UnauthorizedException` (403 is swallowed silently) |

### A2. Permission and target checks

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-12 | none | `user1` (no permissions) runs `/Ban user:user2` | Error "You do not have the required permissions to use this command." | No Sin; not banned |
| MOD-13 | Give `user1` **only** BanMembers | `user1` runs `/Ban user:user2` | Denied: the user gate is ManageMessages, not BanMembers (legacy compatibility, intentional) | none |
| MOD-14 | Give `user1` **only** ManageMessages (no BanMembers) | `user1` runs `/Ban user:user2` | Allowed (legacy ManageMessages gate, intentional) | Sin row |
| MOD-15 | `mod` has Ban + ManageMessages | `mod` runs `/Ban user:user2` | Allowed | Sin actor = mod |
| MOD-16 | `owner` and `admin` | Each runs `/Ban` on `user2` (re-invite between) | Both allowed via bypass | Sin row each |
| MOD-17 | none | `admin` runs `/Ban user:admin` (self) | Yellow "I do not have permissions to ban that user." (the hierarchy check runs first and fails for equal or higher rank, so the self-ban message is unreachable for anyone the bot cannot outrank). Repeat as `user1`-level user with ManageMessages to reach "You can't ban yourself." | No Sin |
| MOD-18 | none | `admin` bans `higher` | "I do not have permissions to ban that user." | No Sin; not banned |
| MOD-19 | none | `admin` bans `owner` | Same message (owner hierarchy is highest) | No Sin |
| MOD-20 | none | `admin` bans the bot itself | Same message | No Sin |
| MOD-21 | `mod` sits **below** `admin`, bot above both | `mod` runs `/Ban user:admin` | Bot can act, so it **bans admin**. There is no check that the invoker outranks the target. **Confirm intent** | Sin row, actor = mod |
| MOD-22 | Remove BanMembers from the bot role | `admin` runs `/Ban user:user2` | Check fails with the bot-permission message; nobody banned | No Sin |
| MOD-23 | Bot has BanMembers but bot role is **below** `user2`'s top role | Move `user2` above the bot, run `/Ban user:user2` | "I do not have permissions..." (same as MOD-18) | No Sin |
| MOD-24 | Moderation disabled | `admin` runs `/Ban` | "This module is disabled in this server." | No Sin |
| MOD-25 | none | Ban `user2` in Test-A; run `/UserInfo` / check Test-B | `user2` is **not** banned in Test-B (guild isolation) | Sin row has Test-A guild id only |
| MOD-26 | Bot has BanMembers, Discord returns an error mid-way (hard to force; e.g. ban a user id just deleted) | Run `/Ban` | Generic error embed with an error id | **A Sin row remains even though the ban failed** (the sin is saved before the Discord call; `/Kick` deliberately does the reverse). Record whether the row exists |

### A3. `BanAddedEvent` (bans not made through the bot)

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-27 | Bot has View Audit Log | Ban `user2` from the **Discord client** (member context menu, reason "manual") | `#modlog` "Banned" embed with Mod = the acting moderator and Reason "manual" | New Sin row: type Ban, actor = that moderator, reason "manual" |
| MOD-28 | Bot has View Audit Log | Ban from the client with no reason | Embed Reason "None" | Sin row, no reason history |
| MOD-29 | Remove View Audit Log from the bot | Ban from the client | `#modlog` gets an alert-style guild issue (`CannotReadAuditLog`) via the issue reporter (check `#alerts` / mod log); **no** "Banned" embed, because nothing was recorded (`lastBan` is null) | No new Sin (unless an older ban of the same user exists, in which case the embed reuses **that** old sin's id and reason: see observation 8) |
| MOD-30 | Two bans of the same user inside 30 s (bot ban, unban, client re-ban) | Do it quickly | The client re-ban reuses the sin from the first ban, so the mod-log embed shows the first ban's actor | No new Sin. Confirms the 30 s de-dup window |
| MOD-31 | Moderation disabled | Ban from the client | No mod-log entry | No Sin |
| MOD-32 | Bot ban of a user with a very recent Sin from `/Ban` | `/Ban` (MOD-01) | Only **one** mod-log embed, not two | One Sin row |

---

## B. `/Unban` and `BanRemovedEvent`

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-33 | `user2` banned via `/Ban` | `admin` runs `/Unban user:user2` | Green embed "Unbanned" with User and Moderator. `user2` removed from the ban list | No new Sin. `#modlog` "Unbanned" embed from `BanRemovedEvent` with the original Sin id and `Mod` = **the original banner** (not the person who unbanned: observation 9) |
| MOD-34 | `user2` banned from the client only (Sin created by MOD-27) | Unban from the client | `#modlog` "Unbanned" embed with the Sin id from MOD-27 | none |
| MOD-35 | `user2` banned before the bot existed (delete their Sin rows first) | Unban | No mod-log embed (no ban sin to attach) | none |
| MOD-36 | `user2` **not** banned | `/Unban user:user2` | Yellow "Error: user2 was not unbanned because user could not be found." Wording is misleading (the user exists, they are just not banned) | none |
| MOD-37 | none | `user1` (no perms) runs `/Unban` | Permission error | none |
| MOD-38 | `user1` has only ManageMessages | `/Unban user:user2` | Allowed (legacy ManageMessages gate, as MOD-14) | none |
| MOD-39 | Bot lacks BanMembers | `/Unban` | Bot-permission check message | none |
| MOD-40 | Moderation disabled | `/Unban` | "This module is disabled in this server." | none |
| MOD-41 | Moderation disabled | Unban from the client | No mod-log entry | none |
| MOD-42 | `user2` banned, unbanned, banned again (two Ban sins) | Unban | Embed uses the **newest** Ban sin id | none |

---

## C. `/Kick`

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-43 | `user2` in guild | `admin` runs `/Kick member:user2 reason:"rude"` | `user2` removed from the guild (Audit Log: kick, reason "rude"). Yellow embed "Kick" with User, Sin Id, Moderator, Reason. **No DM is sent** | `Sins` row type Kick + reason. `#modlog` gets the same embed |
| MOD-44 | `user2` re-invited | `/Kick member:user2` without reason | Embed has no Reason field | Sin row, no reason history |
| MOD-45 | none | `/Kick` with 1001-character reason | Client rejects | none |
| MOD-46 | none | `user1` (no perms) runs `/Kick` | Permission error | No Sin; still in guild |
| MOD-47 | `user1` has only ManageMessages | `/Kick member:user2` | Denied (gate is KickMembers) | none |
| MOD-48 | `user1` has only KickMembers | `/Kick member:user2` | Allowed | Sin row |
| MOD-49 | `mod` has KickMembers | `mod` kicks `user2` | Allowed | Sin actor = mod |
| MOD-50 | none | `admin` runs `/Kick member:admin` | Yellow "You can't kick yourself." | No Sin |
| MOD-51 | none | `admin` kicks `higher` | **No pre-check.** Discord refuses (403), the command errors. Expect the "bot is missing permissions" embed with the suggested fix (which is misleading: this is hierarchy, not a permission). Record the exact text | **No Sin row** (kick happens before save) |
| MOD-52 | none | `admin` kicks `owner` and the bot | Same as MOD-51 | No Sin |
| MOD-53 | `mod` below `admin` | `mod` kicks `admin` | Allowed (no invoker-hierarchy check). **Confirm intent** | Sin row |
| MOD-54 | Remove KickMembers from the bot | `/Kick member:user2` | Bot-permission check message | No Sin |
| MOD-55 | Moderation disabled | `/Kick` | "This module is disabled in this server." | none |
| MOD-56 | `user2` kicked | `user2` rejoins via invite | Rejoins normally (no ban) | none |

---

## D. `/Warn`

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-57 | `user1` has DMs open | `mod` runs `/Warn user:user1 reason:"be nice"` | Yellow embed "Warn" with User, Sin Id, Moderator, Reason. `user1` gets a DM "You have been warned by @mod for be nice" (author "Warning Id n"). Embed is sent **before** the DM | `Sins` row type Warn + reason. `#modlog` gets the embed |
| MOD-58 | `user1` has DMs closed | `/Warn user:user1 reason:"x"` | Embed shown; no DM | `#modlog` gets a red line "Was not able to send a direct message with the warn details to @user1" **and** the warn embed |
| MOD-59 | User is not in the guild | `/Warn user:<non-member> reason:"x"` | Embed shown, no DM attempted | Sin row; mod-log embed |
| MOD-60 | none | `mod` warns themself | Error "You cannot warn yourself." | No Sin |
| MOD-61 | none | `/Warn` with a 1001-character reason | Client rejects | none |
| MOD-62 | none | `/Warn` with reason omitted | Client requires it | none |
| MOD-63 | none | `user1` (no perms) runs `/Warn` | Permission error | No Sin |
| MOD-64 | `user1` has ManageMessages only | `/Warn user:user2 reason:"x"` | Allowed | Sin row |
| MOD-65 | `owner` (no role) and `admin` | Each warns `user1` | Allowed (`RequirePermissions` honours owner/Administrator) | Sin row each |
| MOD-66 | none | `mod` warns `admin`, `higher`, and the bot | Allowed: warn has no hierarchy or bot checks. Confirm intent | Sin rows |
| MOD-67 | Moderation disabled | `/Warn` | "This module is disabled in this server." | No Sin |
| MOD-68 | Use bold, backticks and `@everyone` in the reason | `/Warn ... reason:"@everyone **hi**"` | Embed field renders text; **no** mass ping (embed content does not ping) | Reason stored verbatim |

---

## E. `/Mutes` mute-role admin

Run these first: `/Mute` cases depend on a configured role. Start with no mute role configured.

### E1. `/Mutes View`, `Set`, `Create`, `Refresh`

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-69 | No mute role configured, nobody muted | `admin` runs `/Mutes View` | Ephemeral embed: Mute Role "None", Muted Users "None" | none |
| MOD-70 | none | `mod` (no ManageGuild) runs `/Mutes View` | Permission error | none |
| MOD-71 | none | `owner` and `admin` run `/Mutes View` | Allowed | none |
| MOD-72 | Moderation disabled | `/Mutes View` | "This module is disabled in this server." | none |
| MOD-73 | none | `admin` runs `/Mutes Set role:@Muted` (pre-created role) | Public "Will now use role @Muted for muting users." | Mute-role guild setting saved; `#modlog` "admin updated the mute role to @Muted" |
| MOD-74 | MOD-73 done | `/Mutes View` | Mute Role shows `@Muted` | none |
| MOD-75 | none | `/Mutes Set role:@everyone`; then a bot-managed role; then a role **above** the bot | Accepted with no validation. Record what happens; then reset to `@Muted`. Later `/Mute` will fail (403) for the above-bot role | Setting saved |
| MOD-76 | `mod` | `mod` runs `/Mutes Set` | Permission error | Setting unchanged |
| MOD-77 | Bot lacks ManageRoles | `/Mutes Set role:@Muted` | Bot-permission check message | Setting unchanged |
| MOD-78 | Delete the configured role in Discord | `/Mutes View` | Mute Role "None" (role lookup returns null) even though the setting still holds the old id | none |
| MOD-79 | Test-A with a mix of text, voice, stage, announcement, forum channels, a category, and a thread | `admin` runs `/Mutes Create` | Messages in order: "Role @Muted is created..."; "...saved in configuration..."; success (or partial-failure list). New role "Muted" exists. Then, ~2 s later, a diagnostics follow-up embed with `moderation-diagnostics.txt` | Mute-role setting = new role; `#modlog` "admin created @Muted to use as a mute role." |
| MOD-80 | after MOD-79 | Inspect channel overwrites for the new role | Text, category, forum and media channels: **deny** Send Messages, Send in Threads, TTS, Add Reactions. Voice and stage: deny Connect, Speak. **Announcement channels and threads are untouched** | none |
| MOD-81 | `#private` hidden from the bot (bot has no View Channel there) | `/Mutes Create` | Yellow message listing `#private` (and any other channel where the overwrite failed) as not set; success is not claimed | Setting still saved |
| MOD-82 | A channel where the Muted role already has an explicit overwrite (e.g. allow Send Messages) | `/Mutes Refresh` | The allow bit is removed and the deny bit added; unrelated overwrite bits are preserved | none |
| MOD-83 | Run `/Mutes Create` twice | Second run | Second role named "Muted" is created and **replaces** the configured role; the first is orphaned. Note the duplicate name | Setting = second role |
| MOD-84 | No mute role set | `/Mutes Refresh` | Yellow "No mute role is configured." | none |
| MOD-85 | Configured role deleted in Discord | `/Mutes Refresh` | Yellow "Could not find configured mute role." | none |
| MOD-86 | Valid role, new channel added since | `/Mutes Refresh` | "Refreshing permissions for @Muted role." then "Succussfully refreshed..." (typo in code; record whether it is still there), then diagnostics follow-up. The new channel now has the deny overwrite | none |
| MOD-87 | A role higher than `@Muted` grants Send Messages in a channel (so the mute would not hold) | `/Mutes Refresh` | Diagnostics follow-up embed and attached `moderation-diagnostics.txt` flag that channel (details are covered in area 04) | none |
| MOD-88 | Bot lacks ManageRoles / ManageChannels | `/Mutes Create` | Permission check message, no role created (Create requires bot ManageRoles) | Setting unchanged |
| MOD-89 | Test-B | Run `/Mutes View` in Test-B | Shows Test-B's own setting (isolation) | none |

---

## F. `/Mute`

Precondition unless stated: mute role configured (MOD-73 or MOD-79), `user2` in guild with no Muted role.

### F1. Happy path and options

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-90 | `user2` DMs open | `mod` runs `/Mute user:user2 duration_type:Minutes duration_amount:2 reason:"cooldown"` | Red embed "Mute": User, Sin Id, Moderator, Length "2 Minutes", Reason. `user2` receives the `@Muted` role (Audit Log reason "cooldown"). DM: "You have been muted for 2 Minutes by @mod for cooldown". `user2` cannot send in `#general`, cannot react, cannot speak in voice | `Sins` row type Mute + reason. `Mutes`: `MuteAdded` row with `MuteRoleId` = the configured role and `EndTime` about now+2 min. `#modlog` embed |
| MOD-91 | none | Mute with `Hours:1` and `Days:1` | Length shown correctly; `EndTime` matches | `Mutes.EndTime` = now + 1 h / 1 d |
| MOD-92 | none | Mute without a reason | Embed has no Reason field. The role grant passes a null audit reason; DM says "...by @mod for " with an empty tail. Record exactly what is sent (observation 5) | Sin row, no reason history |
| MOD-93 | none | `duration_amount:0` | Accepted (min is 0). Role granted; mute expires on the next tick (about 5 s) | `Mutes` `EndTime` about now. See MOD-124 for the expiry behaviour |
| MOD-94 | none | `duration_amount:-1` | Client rejects | none |
| MOD-95 | `user2` DMs closed | `/Mute user:user2 ...` | Mute still works; embed shown | `#modlog` red line "Was not able to send a direct message with the mute details to @user2." **and** the mute embed |
| MOD-96 | `user2` already muted (MOD-90 active) | Mute again with a different duration | Role is already present; second embed shown | A second `MuteAdded` row (newer `SetAt`); the newer row is the active one, so expiry follows the **new** end time. Two Sin rows |
| MOD-97 | 1001-character reason | Run `/Mute` | Client rejects | none |
| MOD-99 | Moderation enabled, bot restarted during an active mute | Restart the bot, then wait for expiry | Mute expires on schedule after restart (persisted) | `MuteRemoved` row appears |

### F2. Permission and target checks

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-100 | none | `user1` (no perms) runs `/Mute user:user2 ...` | Permission error | No Sin, no Mutes row, no role |
| MOD-101 | `user1` has ManageMessages only | `/Mute user:user2 ...` | Allowed (gate is ManageMessages) | Sin + Mutes rows |
| MOD-102 | none | `owner`, `admin` | Allowed | rows |
| MOD-103 | No mute role configured | `/Mute user:user2 ...` | Yellow "The mute role is not configured. Please configure it before using this command." | No Sin, no Mutes row |
| MOD-104 | Moderation disabled | `/Mute` | "This module is disabled in this server." | none |
| MOD-105 | Bot lacks ManageRoles | `/Mute` | Bot-permission check message | none |
| MOD-106 | Configured role id exists in settings but the role was deleted in Discord | `/Mute user:user2 ...` | Yellow "The configured mute role does not exist. Please configure it again..." | **A Sin row and a `MuteAdded` row are already saved** even though no role was granted (observation 4). `/Mutes View` may list `user2` |
| MOD-107 | Mute role above the bot role | `/Mute user:user2 ...` | Discord refuses (403). Bot-missing-permissions embed with suggested fix | Sin + `MuteAdded` rows exist but no role was granted (same phantom-record issue as MOD-106); `#alerts`/mod log gets the guild issue |
| MOD-108 | none | `mod` mutes `higher` | Discord 403; same handling as MOD-107 | phantom rows |
| MOD-109 | none | `mod` mutes `admin` (below bot, above `mod`) | Allowed (no invoker-hierarchy check). Confirm intent | rows |
| MOD-110 | none | `mod` mutes themself and the bot | Self-mute works (no self check); bot mute is refused with 403 or accepted depending on hierarchy. Record | rows |
| MOD-111 | Test-B has no mute role | Mute in Test-B | "mute role is not configured" (Test-A's role does not leak) | none |

---

## G. `/Unmute`

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-112 | `user2` muted by MOD-90 | `mod` runs `/Unmute user:user2` | Green embed "Unmute": User, Moderator. Role removed (audit reason "Unmuted by @mod"). DM "You have been unmuted by @mod". `user2` can speak again | New `MuteRemoved` row; original `MuteAdded` row untouched. `#modlog` embed |
| MOD-113 | `user2` DMs closed | Unmute | Works; embed shown | `#modlog` red line "Was not able to send a direct message with the unmute details to @user2" plus the embed |
| MOD-114 | `user2` **not** muted, no role | `/Unmute user:user2` | Configured role is resolved and "revoked" (no-op); the "Unmute" embed and DM are still sent. No "not muted" message. **Misleading**: report it | `RemoveMute` finds nothing; no rows written |
| MOD-115 | `user2` has the Muted role added by hand, no DB record | `/Unmute user:user2` | Role is removed (fallback to configured role) | none |
| MOD-116 | `user2` muted; then run `/Mutes Set` to point at a different role B; the recorded role is still A | `/Unmute user:user2` | Role **A** (recorded) is removed, not B | `MuteRemoved` row |
| MOD-117 | `user2` muted with role A, then A deleted in Discord; configured role B exists | `/Unmute` | Falls back to B; B removal is a no-op; embed shown | `MuteRemoved` row |
| MOD-118 | Muted with role A; A deleted; **no** configured role (delete the setting row, restart the bot) | `/Unmute` | Yellow message: "The role this member was muted with no longer exists and no mute role is set... Their mute is cleared" | `MuteRemoved` row; no embed/DM/mod-log |
| MOD-119 | Bot role below the mute role | `/Unmute user:user2` | Discord 403 -> bot-missing-permissions embed | **No** `MuteRemoved` row (mute stays recorded, per code comment). Fix the hierarchy and retry succeeds |
| MOD-120 | none | `user1` (no perms) | Permission error | none |
| MOD-121 | Bot lacks ManageRoles | `/Unmute` | Bot-permission check message | none |
| MOD-122 | Moderation disabled | `/Unmute` | "This module is disabled in this server." | none |
| MOD-123 | After an unmute | Wait past the original `EndTime` | Nothing more happens: no "Mute expired" DM or log (record is superseded by `MuteRemoved`) | no new rows |

---

## H. Mute expiry (`MuteBackgroundTasks`) and rejoin (`UserJoinedWhileMuted`)

Use 1-minute mutes. Console at Debug for `Grimoire.Features.Moderation.Mute`.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-124 | `user2` muted 1 min, DMs open | Wait for expiry plus about 10 s | Role removed (audit reason "Mute expired"). DM embed "Mute on @user2 has expired." `#modlog` "Mute on @user2 has expired." | `MuteRemoved` row with moderator = the bot user |
| MOD-125 | `user2` muted 1 min, DMs closed | Wait | Role removed; DM failure ignored; mod-log line still posted | `MuteRemoved` row |
| MOD-126 | Muted 1 min; remove the role by hand before expiry | Wait | No role revoke attempted; DM and mod-log still sent | `MuteRemoved` row |
| MOD-127 | Muted 1 min; `user2` **leaves** the guild before expiry | Wait | Nothing sent, nothing to remove | `MuteRemoved` row (Resolve path). `user2` rejoining after this is **not** muted |
| MOD-128 | Muted 5 min; `user2` leaves then rejoins **before** expiry | Rejoin | `user2` receives the mute role again (audit reason "Rejoined while muted") | none; the mute record is unchanged |
| MOD-129 | Same as MOD-128 but rejoin **after** the `EndTime` and after the task ran | Rejoin | No role given | none |
| MOD-130 | Same as MOD-128 but rejoin after `EndTime` **while** the task has not run (stop the bot, wait, rejoin, start the bot) | Rejoin then start | On rejoin: no role (end time passed). On start: expiry task finds the member with no role, records the removal, sends the DM and log | `MuteRemoved` row |
| MOD-131 | `user2` was muted then unmuted (MOD-112) | Leave and rejoin | No role given (latest row is `MuteRemoved`) | none |
| MOD-132 | Muted with role A, A deleted, configured role B is set; `user2` leaves and rejoins before expiry | Rejoin | Role **B** is granted (fallback) | none |
| MOD-133 | Moderation disabled while the member is muted and leaves/rejoins | Rejoin | No role granted | none |
| MOD-134 | Bot role dropped **below** the mute role before expiry | Wait for expiry | Revoke fails; a `CannotUnmuteMember` guild issue is raised (mod log / `#alerts`). Role stays on `user2`. No mod-log "expired" line | **No** `MuteRemoved` row. Console error at the next attempt |
| MOD-135 | Continue MOD-134 and restore the hierarchy | Wait up to the backoff (first retry 30 s, then 60 s...) | Role removed, DM and log sent on the next allowed attempt | `MuteRemoved` row |
| MOD-136 | Muted 1 min; delete the mute role in Discord; the configured setting still points to it | Wait | `MuteRoleMissing` issue is reported; nothing to remove | `MuteRemoved` row (removed so it is not retried forever) |
| MOD-137 | Muted 1 min; delete the mute role and clear the setting | Wait | No issue reported (nothing configured to complain about) | `MuteRemoved` row |
| MOD-138 | Muted with role A; set configured role to B via `/Mutes Set` | Wait for expiry | Role A (recorded) is removed | `MuteRemoved` row |
| MOD-139 | Test a mute row for a guild the bot is no longer in (insert manually or leave Test-B with an expired mute) | Wait | Nothing happens; the record is kept (`KeepForLater`) and no repeated Discord calls in the log | Row remains, no `MuteRemoved` |
| MOD-140 | Two users muted with expiries within the same tick | Wait | Both handled independently. A failure on one (MOD-134) does not block the other | one `MuteRemoved` each; failed one absent |
| MOD-141 | `/Mutes View` while a mute has expired but the task has not yet run | View | The member is still listed (expired mutes are not filtered) | none |
| MOD-142 | Muted user leaves | `/Mutes View` | Leaver is **not** listed even though the mute is active | none |
| MOD-143 | 30+ muted members | `/Mutes View` | Embed field may exceed Discord's 1024-character limit and error; record behaviour | none |

---

## I. `/Lock` and `/Unlock`

Use `#locktest` unless stated. Verify the lock by having `user1` try to send a message and add a reaction.

### I1. Text channel locks

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-144 | `#locktest` has **no** `@everyone` overwrite | `mod` runs `/Lock duration_type:Minutes duration_amount:2` in `#locktest` (no channel option) | "#locktest has been locked for 2 Minutes". `@everyone` overwrite now denies Send Messages, Send in Threads, TTS, Add Reactions. `user1` cannot post or react. Admin/mod still can | `ChannelLocks`: `ChannelLocked` row, `EndTime` now+2 min, previous allowed/denied = 0. `#modlog` "#locktest has been locked for 2 Minutes by @mod" |
| MOD-145 | Same, with `reason:"raid"` | Lock | Log line ends "for raid" | `ChannelLocks` row has the reason |
| MOD-146 | none | Lock with `channel:#locktest` while running in `#general` | Locks `#locktest` | row for `#locktest` |
| MOD-147 | `#locktest` has an `@everyone` overwrite **allowing** Send Messages and denying Attach Files | Lock | Send Messages flips to deny; **Attach Files stays denied** | Previous allowed has Send Messages bit recorded |
| MOD-148 | `#locktest` has `@everyone` deny Add Reactions already | Lock then Unlock | After unlock the deny on Add Reactions is still there (restored to previous state, not cleared) | Recorded previous denied contains the bit |
| MOD-149 | Locked (MOD-144) | `/Unlock channel:#locktest` | "#locktest has been unlocked". Overwrite restored exactly to its pre-lock state (removed bits gone, previously allowed bits back). `user1` can post again | New `ChannelUnlocked` row. `#modlog` "@mod unlocked #locktest" |
| MOD-150 | `#locktest` not locked | `/Unlock` | "#locktest is not locked." | none |
| MOD-151 | Locked; run `/Lock` again with a longer duration | Second lock | Reply "locked for N ...". Overwrite still locked. **Original previous permissions are preserved** (a later unlock restores the true pre-lock state, not the locked state) | Second `ChannelLocked` row with the **first** row's previous allowed/denied values and the new `EndTime` |
| MOD-152 | Continue MOD-151 | `/Unlock` | Overwrite equals the original pre-lock state | unlock row |
| MOD-153 | Locked | Manually change the `@everyone` overwrite for an unrelated bit (e.g. deny Embed Links), then `/Unlock` | Embed Links deny stays; only lock bits revert | unlock row |
| MOD-154 | none | `/Lock ... channel:` a **category** | Category overwrite applies; channels that sync with the category become locked; channels with their own `@everyone` overwrite do not. Note which | `ChannelLocks` row for the category |
| MOD-155 | none | `/Lock ... channel:` a **forum** channel | Lock recorded; verify whether users can still create posts (lock permissions are SendMessages / SendThreadMessages / TTS / AddReactions, not Create Posts). Record | row |
| MOD-156 | none | `/Lock` with a voice channel | Client offers only text, thread, category and forum channel types; voice cannot be selected | none |
| MOD-157 | none | `/Lock ... duration_amount:0` | Accepted; the channel is locked, then unlocked on the next tick. Check the "expired" messages (MOD-178) | row with `EndTime` about now |
| MOD-158 | none | `/Lock` with `duration_amount:-1` | Client rejects | none |

### I2. Thread locks

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-159 | A public thread in `#general` with `user1` and `mod` in it | `mod` runs `/Lock ... channel:<thread>` (2 min) | Reply "<thread> has been locked for 2 Minutes". Thread is **not** archived or locked in Discord; the bot enforces it: a message from `user1` is deleted right after posting (audit reason "Thread is locked.") | `ThreadLocks` row. Mod-log line |
| MOD-160 | as MOD-159 | `user1` adds a reaction in the locked thread | Reaction is removed | none |
| MOD-161 | as MOD-159 | `mod` (ManageMessages) and `admin` post and react | Messages and reactions stay | none |
| MOD-162 | as MOD-159 | Bot's own message in the thread (e.g. another command reply) | Check whether the bot's own messages are deleted (bot member without ManageMessages in the thread would be). Record | none |
| MOD-163 | as MOD-159 | `/Unlock channel:<thread>` (or run inside the thread) | "<thread> has been unlocked". `user1` can post again | `ThreadUnlocked` row. Mod-log line |
| MOD-164 | Thread not locked | `/Unlock channel:<thread>` | "<thread> is not locked." | none |
| MOD-165 | Private thread | Repeat MOD-159..163 | Same behaviour | rows |
| MOD-166 | A thread in a forum channel | Lock and post as `user1` | Same behaviour | row |
| MOD-167 | Thread locked twice | Lock again with a different duration | Second row created; the newest row wins | two `ThreadLocks` rows |
| MOD-168 | Moderation disabled while a thread is locked | `user1` posts in the thread | The event never checks the module flag, so the message is still deleted. Record what happens (observation 12) | none |

### I3. Permission checks

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-169 | none | `user1` (no perms) runs `/Lock` and `/Unlock` | Permission error each | No rows; overwrite unchanged |
| MOD-170 | `user1` has ManageMessages only | `/Lock ...` | Allowed (gate is ManageMessages) | row |
| MOD-171 | none | `owner` / `admin` | Allowed | row |
| MOD-172 | Bot lacks ManageChannels | `/Lock` and `/Unlock` | Bot-permission check message | No rows |
| MOD-173 | Bot has ManageChannels but **not** ManageRoles (needed to change permission overwrites) | `/Lock` on `#locktest` | Discord may refuse (403): bot-missing-permissions embed. **A `ChannelLocks` row is saved before the overwrite is applied** (observation 6). Verify with SQL and fix the permission | Row exists; overwrite unchanged |
| MOD-174 | Channel where the bot has no access (`#private`) | `/Lock channel:#private` | 403 as above | Row exists |
| MOD-175 | Moderation disabled | `/Lock` and `/Unlock` | "This module is disabled in this server." | none |
| MOD-176 | none | Lock in Test-A, check `#general` in Test-B | Test-B unaffected | Row has Test-A guild id |
| MOD-177 | Restore the bot's permission after MOD-173, channel has a stale row from the failed lock | `/Unlock` | Overwrite is reverted (already at the pre-lock state), unlock row saved, "has been unlocked". Verify no permission corruption | unlock row |

### I4. Lock expiry (`LockBackgroundTasks`)

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| MOD-178 | `#locktest` locked for 1 minute | Wait about 1 min 10 s | Overwrite restored to the pre-lock state. Embed "Lock on #locktest has expired." is posted **in `#locktest`** and in `#modlog` | `ChannelUnlocked` row with moderator = the bot and reason "Channel lock expired." |
| MOD-179 | Thread locked for 1 minute | Wait | "Lock on <thread> has expired." posted in the thread and mod log. `user1` can post again | `ThreadUnlocked` row, reason "Thread lock expired." |
| MOD-180 | Locked; `mod` runs `/Unlock` before expiry | Wait past `EndTime` | No expiry message (record superseded) | no new rows |
| MOD-181 | Locked twice (MOD-151), first end time passes, second later | Wait through the first `EndTime` | Channel stays locked (newest row wins). Unlocks at the second end | one unlock row after the second `EndTime` |
| MOD-182 | Locked 1 min; delete `#locktest` in Discord | Wait | Nothing to unlock; the record is closed (reason "Channel no longer exists.") with no messages sent | `ChannelUnlocked` row |
| MOD-183 | Thread locked 1 min; delete the thread | Wait | Same, "Thread no longer exists." | `ThreadUnlocked` row |
| MOD-184 | Locked 1 min; remove the bot's ManageRoles or ManageChannels | Wait | Overwrite change refused; `CannotUnlockChannel` guild issue reported; channel stays locked | **No** unlock row; retried with backoff |
| MOD-185 | Continue MOD-184, restore permission | Wait up to the retry window (30 s, 60 s, ...) | Overwrite reverted, messages sent | `ChannelUnlocked` row |
| MOD-186 | Locked channel where the `@everyone` overwrite was deleted by hand | Wait for expiry | Overwrite recreated as neutral or removed; no exception | unlock row |
| MOD-187 | Bot stopped during lock and expiry | Start the bot after `EndTime` | Lock lifted on the first ticks | unlock row |
| MOD-188 | Lock row for a guild the bot left | Wait | Untouched, no repeated Discord calls | row remains |
| MOD-189 | `duration_amount:0` lock (MOD-157) | Wait | Lock lifted within about 10 s; "expired" messages posted | unlock row |

---

## J. Cross-checks for this area

| ID | Check | Expected |
|----|-------|----------|
| MOD-190 | Run `/Ban`, `/Kick`, `/Warn`, `/Mute` on the same user | Four Sin rows with distinct ids and the right types; ids appear in the embeds |
| MOD-191 | Cases where bot lacked a permission (MOD-22, 54, 105, 121, 172) | Each shows the bot-permission check message and, when a Discord call fails, a guild issue rather than an unhandled-exception alert. Check `#alerts` |
| MOD-192 | Search `#alerts` after the whole area | Only expected `UnauthorizedException`-derived issues and any real errors from MOD-26, 51, 106..108; nothing from `BanAddedEvent` beyond the audit-log case |
| MOD-193 | Set the mod-log channel to Off, then Ban, Kick, Warn, Mute, Lock | Commands still succeed; nothing posted to a mod log; no exceptions |
| MOD-194 | Point the mod log at a channel the bot cannot write to, run `/Kick` | Kick succeeds; failure is logged/reported by `GuildLog`, not shown as a command failure |
| MOD-195 | Restart the bot with an active mute and an active lock | Both expire correctly afterwards (MOD-99, MOD-187) |
| MOD-196 | Run `/Ban` and `/Mute` from Test-B for a user in both guilds | Only Test-B is affected |

## Code observations

Things noticed while reading the source. Each has cases above to confirm it. Everything except item 1 is tracked in
`known-issues.md`.

1. **Ban, Unban, Mute, Unmute and Lock are gated on ManageMessages**, not the permission named by the action. This is
   intentional, kept for legacy compatibility, so it is not an issue to fix (MOD-13, 14, 38, 101, 170). Kick alone
   uses KickMembers.
2. **No invoker hierarchy check anywhere.** Ban checks only that the *bot* outranks the target. A moderator can
   ban, kick, mute, warn or lock out anyone the bot outranks, including a role above their own (MOD-21, 53, 109).
3. **Kick and Mute have no bot hierarchy pre-check.** Ban has one ("I do not have permissions to ban that user");
   `/Kick` and `/Mute` let Discord return 403, which surfaces as a generic bot-missing-permissions message that
   blames permissions when the real cause is role order (MOD-51, 107, 108).
4. **Ban records the sin before calling Discord**, so a failed ban leaves a phantom Sin (MOD-26). `/Kick` documents
   doing the reverse on purpose. `/Mute` also saves the Sin and the `Mutes` row **before** checking that the role
   exists and before granting it, so a missing role or a 403 leaves a recorded mute with no role (MOD-106..108). The
   member is then reported as muted in `/Mutes View` and the expiry task will later "unmute" them.
5. **`/Mute` without a reason passes `null` as the audit reason** (`GrantRoleAsync(muteRole, reason!)`) and the DM
   reads "...by @mod for " with nothing after it (MOD-92). `/Ban`'s DM handles the empty case; `/Mute`'s does not.
6. **`/Lock` ignores the `Result`** from `ChannelLockAsync` and `ThreadLockAsync`, so it replies "has been locked"
   even if the lock could not be saved. For channels the row is saved **before** the overwrite is applied; if Discord
   refuses, the lock is recorded but not enforced (MOD-173, 174), and the expiry task later reverts something that
   was never changed. Changing overwrites also needs ManageRoles, but the bot gate only requires ManageChannels.
7. **`duration_amount` minimum is 0** for `/Lock` and `/Mute`, allowing a mute or lock that expires immediately
   (MOD-93, 157). The minimum should probably be 1.
8. **`BanAddedEvent`'s 30-second de-dup**: if the audit log cannot be read and an *older* ban exists for the user, the
   embed reuses that old ban's sin, actor and reason (only sins older than 30 s trigger a re-read; when the read fails
   `lastBan` keeps the stale value). A quick ban/unban/ban sequence within 30 s also loses the second ban's sin
   (MOD-29, 30).
9. **`BanRemovedEvent`'s `Mod` field is the original banner**, not the person who unbanned. `/Unban` records nothing,
   so nobody is attributed for the unban (MOD-33).
10. **`/Unban` error text**: a `NotFoundException` (user is not banned, or unknown) is reported as "user could not be
    found" (MOD-36).
11. **`/Unmute` on a member who is not muted still sends "Unmute", DMs the member and logs**, because the record lookup
    falls back to the configured role and `RemoveMute`'s NotFound is ignored (MOD-114).
12. **`LockedTreadEvents` (sic) does not check the Moderation module** and deletes messages from any member without
    ManageMessages in a locked thread, including bot members without that permission (MOD-162, 168). The class name
    is misspelled ("Tread").
13. **`/Mutes Set` accepts any role** (@everyone, managed roles, roles above the bot) with no validation (MOD-75).
14. **`/Mutes View` lists expired-but-unprocessed mutes, hides mutes for members who left, and builds one field with all
    mentions**, which can exceed Discord's 1024-character limit (MOD-141..143).
15. **`/Mutes Create` creates a new "Muted" role every run** without looking for an existing one (MOD-83). The success
    text in `Refresh` has a typo ("Succussfully").
16. **Warn is the only mod action without `RequireUserGuildPermissions`** and without any bot gate; it relies on
    `RequirePermissions`. Behaviour should be identical in practice (MOD-63..65); confirm owner bypass (MOD-65).
17. **DM failure handling is inconsistent**: `/Ban` swallows all exceptions (logs non-403 only), `/Warn` catches only
    BadRequest/Unauthorized and logs to the mod log, `/Mute` and `/Unmute` catch every `Exception`. `/Kick` never DMs
    (MOD-11, 43, 58, 95).

## Findings

Record failures here as `ID - observed - expected - severity`.

| ID | Observed | Expected | Severity | Follow-up |
|----|----------|----------|----------|-----------|
|    |          |          |          |           |

## Promote to automated tests **[auto]**

### Automated

Integration tests in `Grimoire.Tests/Integration/Moderation/` now cover these cases. They no longer need a manual run.

- `WarnTests`: MOD-57 (the reply, sin record and mod-log post), 59 to 63 (61 to 63 through registration), and 65 to 68.
  MOD-64 is enforced by Discord from the registered permission. The DM parts of MOD-57 and MOD-58 are skipped
  tests, because the DM is never attempted (KI-19).
- `BanTests`: MOD-01 to MOD-10, 12 to 17, 22, 24, 27 to 42 (MOD-03 to 06 through the deletion window sent to Discord;
  MOD-07 and 08 through registration). Skipped: MOD-18 to 20 and 23 (KI-19 and KI-03), MOD-21 (KI-01) and MOD-26
  (KI-03). The DMs in MOD-01 and MOD-11 are never sent (KI-19). MOD-25 is in A-35.
- `KickTests`: MOD-43 to MOD-52, 54 and 55, against a test bot that registers `/Kick` itself, because production never
  does (KI-25). MOD-53 is skipped (KI-01). MOD-56 (rejoining) is Discord's behaviour.
- `MuteTests`: MOD-90 to 92, 94 to 97, 100 to 107, 109, 110, 112, 113 and 115 to 122. Skipped: MOD-92's DM text
  (KI-05), MOD-93 (zero-length mutes are never recorded, KI-07), MOD-106 and 107's phantom records (KI-04), MOD-109
  (KI-01) and MOD-114 (KI-11). MOD-99 and 123 are in A-11, MOD-111 in A-35, and MOD-108 stays manual.
- `ModerationCrossCheckTests`: MOD-190, 193 and 194.
- `MuteExpiryTests`: MOD-124 to 134 and 136 to 140. `LockExpiryTests`: MOD-178 to 184 and 186 to 189. MOD-135 and
  185 (retries after backoff) stay manual, and 141 to 143 are blocked by KI-26.
- `MuteAdminTests`: MOD-69 to MOD-80, 82 to 86, and 88. MOD-81 is skipped (KI-27). Listing muted members is skipped
  (KI-26). MOD-87's diagnostics contents are in A-20, and MOD-89 in A-35. The progress messages in MOD-79 and MOD-86
  are successive edits of one response, so the moderator only sees the last.
- `LockCommandTests`: MOD-144 to 158, 163, 164 and 169 to 177 (MOD-156, 158 and 169 through registration). The
  lock-and-reply half of MOD-159 is here too. Skipped: MOD-173 and 174's leftover lock record (KI-06). MOD-176 is in
  A-35, and expiry (MOD-178 onward) in A-11.
- `ThreadLockTests`: the enforcement half of MOD-159, plus MOD-160, 161, 162, 165, 166 and 167, and the lifting half
  of MOD-163. MOD-168 is a skipped test (KI-12). The `/Lock` and `/Unlock` replies are still manual (backlog A-10).
- `Integration/Shared/MultiGuildTests.cs`: MOD-25 (as its mirror, a ban in Test-B), 89, 111, 176 and 196.

### Candidates

- `MuteRoleResolver.Resolve`: recorded role exists / recorded missing with configured present / both missing /
  both null (MOD-116..118, 132, 138).
- `ExpiredItemTriage.For`: all four (in-guild, target-exists) combinations (MOD-127, 136, 139, 182).
- `SettingsModule` mute state: latest row wins across add / add / remove; `GetAllExpiredMutes` returns only the
  latest row per user and only when expired; `GetActiveMute` after `MuteRemoved` is NotFound (MOD-96, 112, 123, 131).
- `SettingsModule` lock state: re-lock keeps the **original** previous allowed/denied (MOD-151, 152); unlock of an
  unlocked channel returns NotFound (MOD-150, 164); expired-lock queries return only the newest row.
- `PermissionsExtensions` lock helpers (`RevokeLockPermissions`, `SetLockPermissions`, `RevertLockPermissions`,
  voice variants): overwrite math incl. preserving unrelated bits and previously-set lock bits (MOD-147, 148, 153).
- `ChannelLockReverter.RevertAsync` with a substituted channel: no `@everyone` overwrite, existing overwrite (MOD-186).
- `UserJoinedWhileMuted`: active, expired, removed and no-record cases, module disabled, role fallback
  (MOD-128..133).
- `DurationTypeExtensions`: minutes / hours / days offsets.
- `BanAddedEvent` sin-reuse vs new-sin decision at the 30 s boundary, and audit-log failure paths (MOD-29, 30).
- Command level with NSubstitute for `AddBanCommand`, `KickUser`, `Warn`, `MuteUser`, `UnmuteUser`: order of DB write
  versus Discord call (MOD-26, 51, 106), which is currently untested.
