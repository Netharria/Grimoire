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
| 21 | I-8, I-9, I-10, I-11, R-1 | Fix all | R-1 goes with I-11 |
| 21 | I-12, I-17, M-7, M-8, M-9 | Fix all | M-7 goes with D-2 (rank 23) |
| 21 | M-10, L-11 | Fix every bullet | |
| 21 | L-10 | Fix | |
| 21 | X-7, X-8, X-9, X-10 | Fix all | X-9: accept that levels drop slightly in guilds with `Base × Modifier` ≤ 15 |
| 21 | C-4, C-5, C-8 | Fix all | C-8: every bullet |
| 22 | D-1, S-7, D-6 | Fix all | D-6 via a public `FromDiscord`; drop `InternalsVisibleTo` for `Grimoire`; rename `AttachmentFileName` here (see L-1) |
| 23 | D-2, M-7 | Do | |
| 24 | P-4, P-6 | Do | |
| 25 | D-3, D-4, D-9, S-9, S-11, I-14, L-13, M-12 | Do | |
| 26 | S-5, S-6, S-10, X-13, I-13, guild ruling | Do | |
| 27 | C-6, C-9, D-5, D-8 | Do | |
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
