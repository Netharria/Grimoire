# Branch review decisions

Owner's action for each finding in `branch-review.md`, decided 2026-11-07. "Fix" means the fix proposed in the review unless a
note says otherwise.

## Owner decisions

| Finding | Decision |
|---------|----------|
| S-1 | Trust stored values: `FromDatabase` doesn't revalidate; `Create` validates input |
| P-1, C-2 | Support deny lists: `CASE` in the migration, deny arm in `BuildRoles`, correct the spec |
| P-2 | Keep it: `ModeratorId?` on `ReclaimedXp` mapped to `AwarderId`; phase 7 skips type 2 |
| M-4 | No: check Manage Messages on the target channel |
| C-3 | Suppress `@everyone`/`@here`; ping only `%Mention`'s resolved user or role via `WithAllowedMentions`; `%Message` text never pings; delete the sanitiser |
| X-6 | Not deliberate: use `Moderation`, add who did it to `/Rewards Add`, give the leveling log its own test channel |

## Ranked fix list

| Rank | Finding | Decision | Note |
|------|---------|----------|------|
| 1 | L-1 | **Done** 2026-11-07, uncommitted | Store the full `x.Url` again, as `main` did (Discord's signed URLs mean building it from the file name no longer works) |
| 2 | I-1 | **Done** 2026-11-07, uncommitted | Also fixed `GainUserXp.cs:197` (same catch). Found KI-54 (DSharpPlus send fails after a timeout) |
| 3 | I-2, I-13 `ToInvite` | **Done** 2026-11-07, uncommitted | `Invite.Inviter` is now optional (Discord can leave it out); also fixed KI-52 |
| 4 | M-1 | **Done** 2026-11-07, uncommitted | |
| 5 | M-2 | **Done** 2026-11-07, uncommitted | All six `sin_id` options, including `/Pardon` and the three `/Publish` ones; file renamed to `SinIdArgumentConverter.cs` |
| 6 | P-3 | **Done** 2026-11-07, uncommitted | `SetCommandTimeout(30 min)` in code before each `MigrateAsync` |
| 7 | L-2 | **Done** 2026-11-07, uncommitted | Skip the bot's own messages in `AddMessageEvent`, plus `main`'s stored-author check in `DeleteMessageEvent` for rows saved before the deploy |
| 8 | X-1, X-11 | **Done** 2026-11-07, uncommitted | `FakeGuild` roles can now be `managed` (test-harness item) |
| 9 | X-2 | **Done** 2026-11-07, uncommitted | Auto-remove as proposed, recorded as removed by the bot. Moderators are told through the guild issue system (`RewardsForDeletedRolesRemoved`, a "Setup problem" in the mod log and the daily warning report), not a separate message; the role ids are in the issue code so a second removal on the same day isn't throttled. Replaces `RewardRolesMissing` |
| 10 | S-2 | **Done** 2026-11-07, uncommitted | |
| 11 | I-4, I-13, L-12, X-13 `LogSetAsync` | **Done** 2026-11-07, uncommitted | `ctx.ReplyResultAsync`, plus `ChannelSettingCommand` for the six channel setters. Also merged the double switches in both Override commands (L-12, M-12) |
| 12 | I-5, X-5 | **Done** 2026-11-07, uncommitted | All nine sites. `/Level` shows "Unavailable" for Next Reward instead of failing, like `/UserInfo` skipping its level fields |
| 13 | L-3, L-4, L-5, L-6, D-7 | **Done** 2026-11-07, uncommitted | Also the L-12 bullet on the duplicated System Id and Member Id fields (`AddMessageAuthorFields`) |
| 14 | S-4, M-5 | **Done** 2026-11-07, uncommitted | A failed ignore lookup counts as **ignored** (no XP during an outage), not as not-ignored; `/Lock` revert half not taken. `GetAllExpiredMutes` and `GetAllMutes` still throw on purpose: they return no `Result`, and an empty list would read as "no mutes" |
| 15 | I-3, I-15 | **Done** 2026-11-07, uncommitted | A failed lookup logs at Information and the delete is logged as an ordinary message |
| 16 | M-3, M-11 | **Done** 2026-11-07, uncommitted | `UsernameHistory.GetDisplayNameAsync` used by all six; `/Publish` shares one path; one overwrite method |
| 17 | X-3, X-4, C-1, C-7 | **Done** 2026-11-07, uncommitted | A use is counted only after the reply was sent, so a refused send doesn't count either |
| 18 | S-3 | **Done** 2026-11-07, uncommitted | |
| 18 | M-6, KI-53 | **Done** 2026-11-07, uncommitted | Recording the mute before granting the role is kept: a role with no record is the worse failure |
| 19 | I-6, I-7, I-16 | **Done** 2026-11-07, uncommitted | Images are buffered at event time, so retries re-upload them (DSharpPlus builders aren't disposable). Only the first embed carries text. 10 MiB per-file cap; a per-message total wasn't checked against real Discord |
| 20 | L-7, L-8, L-9, rest of L-12 | **Done** 2026-11-07, uncommitted | New `SettingsModule.IsLogChannelSet`; the audit log parser takes the stored author and has no database access now |
| 21 | I-8, I-9, I-10, I-11, R-1 | **Done** 2026-11-08, uncommitted | R-1 has its own issue, `LogChannelMissing`. The publisher reports without awaiting, so it can't block on its own full queue |
| 21 | I-12, I-17, M-7, M-8, M-9 | **Done** 2026-11-08 (M-7 at rank 23) | M-7 goes with D-2 (rank 23). `/Diagnostics` still doesn't check Manage Messages for thread locks |
| 21 | M-10, L-11 | **Done** 2026-11-08, uncommitted | Durations capped at 36,500. Log channels need Embed Links (and Attach Files for delete, bulk delete and avatar logs) and must hold messages. No test for the `/SinLog` order or the `InviteService` race, which the harness can't make happen |
| 21 | L-10 | **Done** 2026-11-08, uncommitted | The trim runs on the first tick, then daily. The message purge still runs every tick |
| 21 | X-7, X-8, X-9, X-10 | **Done** 2026-11-08, uncommitted | X-9: accept that levels drop slightly in guilds with `Base × Modifier` ≤ 15. The shortcut is replaced by an exact binary search, not a plain loop, so a huge XP total can't freeze the bot (the reason the shortcut existed). Long View lists are paged, not cut off: `/Rewards View`, `/Ignore View`, `/Mutes View`, `/SpamFilter View` and `/Log Message ViewOverrides` use `ctx.ReplyPagedAsync` |
| 21 | C-4, C-5, C-8 | **Done** 2026-11-08, uncommitted | C-8: every bullet. An `embed_color` without `embed` is refused. No test for the Revert numbering or the autocomplete cap: the harness can't drive autocomplete |
| 22 | D-1, S-7, D-6 (and S-1) | **Done** 2026-11-08, uncommitted | S-1 done here: level settings are trusted on read. D-6: one public non-validating `Create(string)` for `Username`, `AvatarFileName`, `InviteCode`, `InviteUrl` and `AttachmentUrl` (owner's call, replacing `FromDatabase` plus `FromDiscord`); `InternalsVisibleTo` for `Grimoire` dropped. `AttachmentFileName` renamed `AttachmentUrl`, with the property and column; migration `RenameAttachmentFileNameToAttachmentUrl` |
| 23 | D-2, M-7 | **Done** 2026-11-08, uncommitted | `Sin.ForX` take an optional `ModerationReason?`; new `ModerationReason.CreateIfNotBlank` for optional reasons. Each caller keeps one throw, for ids that can't be zero |
| 24 | P-4, P-6 | **Done** 2026-11-08, uncommitted | Both contexts: one `ConfigureStronglyTypedIds` convention (a linked file shared by `Grimoire.Data` and `Grimoire.Settings`); EF reports no model change in either. Get-only settings properties keep a bare `Property(...)` so they stay mapped. One `ModerationActorConverter` |
| 25 | D-3, D-4, D-9, S-9, S-11, I-14, L-13, M-12 | **Done** 2026-11-08, uncommitted | D-3: `Match` keeps `onNotFound` and `onNotModified`, which this branch's callers now pass. Found and fixed: on a `Task<Result<T>>`, `await ...Match(...)` with task-returning handlers didn't await the handler (ten call sites); new non-generic `Task`/`ValueTask` overloads do. D-4: the task `Bind` stays (used since). S-9: `GuildSettingDefault` kept with its `Create` and redundant-write arm (owner plans a reset-to-default option); `DisableMuteRole` made `internal`, as the tests' way to clear the mute role. I-14: the `guildId` setting is now read nowhere. L-13: `/ModSettings View` and `/LevelSettings View` keep the line and no longer need their module on, so it can say False (owner's call); the Set commands beside them are still gated |
| 26 | S-5, S-6, S-10, X-13, I-13, guild ruling | **Done** 2026-11-08, uncommitted | Guild ruling: `ctx.RequiredGuild`, plus `ctx.RequiredMember` for the `ctx.Member!` uses; the autocomplete provider keeps `ctx.Guild!`. Owner's call: every `UnreachableException` is now Grimoire's own `InvariantViolationException`, so one from a library can't be mistaken for ours. X-13: `/Ignore Remove`'s option is `items` now, like Add. The Module Enabled bullet was decided at rank 25. I-13: `ToInvite` was done at rank 3 and the shared channel setter at rank 11; turning the moderation log off is no longer logged to itself |
| 27 | C-6, C-9, D-5, D-8 | **Done** 2026-11-08, uncommitted | D-5/D-8: the command factories take the allow-role ids and the time, and return the type; `AsNewVersion` does Revert. C-9: `IsUserAuthorized` has a pure overload, tested on every row of the spec's table (the old branching passes the same nine). The autocomplete providers check the guild themselves, so the last `ctx.Guild!` goes. C-3 has a decision but no rank, so the sanitiser is still there |
| 28 | P-5, S-8 | Do | One new migration dropping the 12 indexes |
| 29 | P-7 | Later release | Only after Settings' `Initial` has run in production |

## Items the review said could wait or be left alone

All taken.

| Finding | Decision | Note |
|---------|----------|------|
| I-18 | Do | |
| I-19 | Do | The `ctx.Guild!` half is covered by the `RequiredGuild` ruling |
| P-8 | Do | Unmeasured; check it doesn't break anything that relies on per-context state |
| S-12 | Do | Removing the zero-id checks lets those factories drop `Validation` (ties into D-5) |
| S-13 | Do | Needs a design first: the review found no simple safe lower bound |
| X-12 | Do both bullets | Ignoring a channel covers its threads; `/Ignore Add`/`Remove` report no-ops instead of adding rows |

## Test harness

All three, each done with the fix it covers:

- `FakeGuild` payloads carry attachments, and roles can be `managed` (L-1, X-1).
- `FakeDiscordRest` enforces the 6,000 / 4,096 / 1,024 embed limits and refuses empty messages (L-5, X-3, X-10, I-6, I-12, C-1).
- The leveling log gets its own channel in tests (X-6).

## Still needed before deploy

The production queries in sections 2, 3 and 7, and the real-Discord checks in section 9's "Before deploying".

## Rulings

| Ruling | Decision |
|--------|----------|
| `ctx.RequiredGuild` extension property | Adopt; delete `GetRequiredGuild` |
