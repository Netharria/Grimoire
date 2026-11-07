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
| 3 | I-2, I-13 `ToInvite` | Fix | |
| 4 | M-1 | Fix | |
| 5 | M-2 | Fix | |
| 6 | P-3 | Fix | `SetCommandTimeout(30 min)` in code before each `MigrateAsync` |
| 7 | L-2 | Fix | Skip the bot's own messages in `AddMessageEvent` |
| 8 | X-1, X-11 | Fix both | |
| 9 | X-2 | Fix, changed | Auto-remove as proposed, and send moderators an alert in the mod log channel (`Moderation`) when a reward is removed because its role was deleted; replaces `RewardRolesMissing` |
| 10 | S-2 | Fix | |
| 11 | I-4, I-13, L-12, X-13 `LogSetAsync` | Fix | One `ctx.ReplyResultAsync` helper |
| 12 | I-5, X-5 | Fix | |
| 13 | L-3, L-4, L-5, L-6, D-7 | Fix all | |
| 14 | S-4, M-5 | Fix, changed | A failed ignore lookup counts as **ignored** (no XP during an outage), not as not-ignored; `/Lock` revert half not taken |
| 15 | I-3, I-15 | Fix both | |
| 16 | M-3, M-11 | Fix all | Helper plus the `/Publish` and overwrite merges |
| 17 | X-3, X-4, C-1, C-7 | Fix all | |
| 18 | S-3 | Fix | |
| 18 | M-6, KI-53 | Fix together | |
| 19 | I-6, I-7, I-16 | Fix all | |
| 20 | L-7, L-8, L-9 | Fix all | |
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
