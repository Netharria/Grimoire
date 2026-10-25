# Known Issues (to fix later)

Found while writing the manual test docs. Not fixed yet. Each entry points at the test cases that confirm it, so
re-run those after a fix. Severity is a rough guess: **high** = wrong data or wrong security outcome,
**med** = wrong or misleading behaviour, **low** = polish.

Intentional behaviour is not listed. For example, `/Ban`, `/Unban`, `/Mute`, `/Unmute` and `/Lock` are gated on
ManageMessages for legacy compatibility (see 02).

## Area 02 - Moderation actions

| ID | Sev | Issue | Where | Confirm with |
|----|-----|-------|-------|--------------|
| KI-01 | high | **No invoker hierarchy check.** Ban only checks that the bot outranks the target. Moderators can ban, kick, mute, warn or lock out anyone the bot outranks, including roles above their own. Confirm whether this is wanted. | `AddBanCommand`, `KickUser`, `MuteUser`, `Warn` | MOD-21, 53, 66, 109 |
| KI-02 | med | **`/Kick` and `/Mute` have no bot hierarchy pre-check.** `/Ban` says "I do not have permissions to ban that user". Kick and Mute let Discord return 403, which is reported as a missing-permission issue when the real cause is role order. | `KickUser`, `MuteUser` | MOD-51, 52, 107, 108 |
| KI-03 | high | **`/Ban` saves the Sin before calling Discord**, so a failed ban leaves a phantom Sin. `/Kick` deliberately does it the other way round. | `AddBanCommand.BanAsync` | MOD-26 |
| KI-04 | high | **`/Mute` saves the Sin and `Mutes` row before checking the role exists or granting it.** A deleted role or a 403 leaves a recorded mute with no role; `/Mutes View` lists the member and the expiry task later "unmutes" them. | `MuteUser.MuteUserAsync` | MOD-106, 107, 108 |
| KI-05 | med | **`/Mute` with no reason** passes `null` as the audit-log reason (`reason!`) and the DM reads "...by @mod for " with nothing after it. | `MuteUser` | MOD-92 |
| KI-06 | high | **`/Lock` ignores the `Result`** from the lock call and replies "has been locked" even if it was not saved. For channels the row is saved **before** the overwrite is applied, so a refused overwrite leaves a lock recorded but not enforced; the expiry task then reverts something never changed. Changing overwrites also needs ManageRoles, but the bot gate only asks for ManageChannels. | `LockChannel` | MOD-173, 174, 177 |
| KI-07 | low | **`duration_amount` minimum is 0** for `/Lock` and `/Mute`, allowing a mute or lock that expires immediately. Probably should be 1. | `MuteUser`, `LockChannel` | MOD-93, 157, 189 |
| KI-08 | med | **`BanAddedEvent` 30 s de-dup reuses stale data.** If the audit log cannot be read and an older ban exists for the user, the mod-log embed shows the old ban's sin, actor and reason. A ban, unban, re-ban sequence inside 30 s also loses the second sin. | `BanAddedEvent` | MOD-29, 30 |
| KI-09 | med | **`BanRemovedEvent` shows the original banner as `Mod`**, and `/Unban` records nothing, so nobody is attributed for the unban. | `BanRemovedEvent`, `RemoveBanCommand` | MOD-33, 42 |
| KI-10 | low | **`/Unban` error text**: a `NotFoundException` (user is not banned) is reported as "user could not be found". | `RemoveBanCommand` | MOD-36 |
| KI-11 | med | **`/Unmute` on a member who is not muted** still sends the "Unmute" embed and DM and logs, because it falls back to the configured role and ignores `RemoveMute`'s NotFound. It should say the member is not muted. | `UnmuteUser` | MOD-114 |
| KI-12 | med | **`LockedTreadEvents` (sic) does not check the Moderation module**, and deletes messages from any member without ManageMessages in a locked thread, including bot members. Class name is misspelled ("Tread"). | `LockedTreadEvents` | MOD-162, 168 |
| KI-13 | low | **`/Mutes Set` accepts any role** (@everyone, managed roles, roles above the bot) with no validation. | `MuteAdminCommands.Set` | MOD-75 |
| KI-14 | med | **`/Mutes View`** lists expired-but-unprocessed mutes, hides mutes for members who left, and puts all mentions in one field that can exceed Discord's 1024-character limit. | `MuteAdminCommands.View` | MOD-141, 142, 143 |
| KI-15 | low | **`/Mutes Create`** makes a new "Muted" role on every run without looking for an existing one. `/Mutes Refresh` success text has a typo ("Succussfully"). | `MuteAdminCommands.Create/Refresh` | MOD-83, 86 |
| KI-16 | low | **Inconsistent DM failure handling**: `/Ban` swallows everything (logs non-403 only), `/Warn` catches only BadRequest/Unauthorized and posts to the mod log, `/Mute` and `/Unmute` catch every `Exception`, `/Kick` never DMs. | Ban, Warn, Mute, Unmute, Kick | MOD-11, 43, 58, 95, 113 |
| KI-17 | low | **`/Warn` has no `RequireUserGuildPermissions`** and no bot gate; it relies on `RequirePermissions` alone, unlike the other actions. Should behave the same in practice but is inconsistent. | `Warn` | MOD-63, 64, 65 |

## Found by automated tests

Each entry has a skipped test, or a planned one, that fails until the issue is fixed. The work items are in
`automation-backlog.md`.

| ID | Sev | Issue | Where | Confirm with |
|----|-----|-------|-------|--------------|
| KI-18 | fixed | **Fixed 2026-10-25. The guild logs never posted.** `GuildLog` was registered as a singleton and again as a separate hosted-service instance, each with its own queue, so nothing read the queue commands wrote to. After 1024 messages, senders would also have blocked forever. It is now split into `GuildLog`, the queue singleton, and `GuildLogPublisher`, the hosted service that reads it. | `Features/Shared/Channels/GuildLog/` | `WarnTests.Warn_PostsTheWarnEmbedToTheModLog` |
| KI-19 | med | **`/Warn` never DMs the member.** Slash commands resolve a user option to a plain `DiscordUser`, never a `DiscordMember`, so the `user is DiscordMember` check is always false. Other commands that DM through the same pattern are likely affected. | `Warn.cs` | `WarnTests.Warn_DirectMessagesTheWarnedMember` (skipped), MOD-57 |
| KI-20 | fixed | **Fixed 2026-10-25. Locked threads enforced nothing.** Threads inherit their parent's permissions, so thread locks rely on `LockedTreadEvents` to delete new messages and reactions until the lock is lifted. The handler replaced `ModerationEvents` in the DSharpPlus 5 migration but was never registered. It is now registered in `GrimoireServiceRegistration.cs`. KI-12, the missing module check, is still open. | `GrimoireServiceRegistration.cs` | `ThreadLockTests`, MOD-159 to MOD-167 |

## Area 01 - General settings

The Code observations section of `01-general-settings.md` lists the open items for that area (Purge has no mod-log
entry and accepts `count:0`, `UserInfo` needs ManageGuild, `GetLevel` has a duplicate module check, bulk-delete of
messages that age past 14 days mid-call). Move them here when they are triaged.
