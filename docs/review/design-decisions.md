# Design review decisions

The owner's answers to the questions in `docs/review/design/12-rollup.md` part 7. Each entry gives the question number,
the decision, and how it changes the findings. Where a decision differs from the review's recommendation, the entry
says so.

## A. Foundations

| Q | Decision | Effect on the findings |
|---|----------|------------------------|
| 1 | **Typed `Unavailable`, no exceptions in our own code.** Differs from F-1. Keep S-4's direction. Outages are a `Result` failure of their own kind, created only by one adapter at the IO edge (where `ExecuteSafelyAsync` is now), which catches the EF/DSharpPlus exceptions. Exceptions stay only for defects (invariant violations, bugs) and cancellation. | F-1 is rewritten: don't delete `ExecuteSafelyAsync`; narrow it to the IO edge, and make it produce `Unavailable`. Remove the `GetOrElse(() => false/null)` defaults and let callers `Bind` through. The boundaries (command `RunAsync`, the last step of an event handler, the background tick) render the generic "[ID …]" reply and raise the alert. F-9 is required. SET-10 (fail-safe) is kept, so settings reads serve the last good value during an outage. LVL-4 keeps its alert, routed through the boundary. |
| 2a | **Keep `Validation<T>` and `Result<T>` as separate types.** Differs from F-2. `Validation` is for cheap, pure input checks: it runs them all and collects every error, and never touches the database. `Result` short-circuits, so expensive work doesn't run after a failure. | F-2 changes from "merge" to "use `Validation` for what it's for". In T-1, a command's validate step is a `Validation` over all of its inputs, converted to `Result` once, before the defer and any loading. Validations never query the database (no id-existence checks). The boundary renders every collected message. |
| 2b | **Drop `Error.Code` and `Error.Target`.** An error is its case plus a message. A specific error gets its own type only where code branches on it. | F-4 as proposed. About 60 test assertions move from `Code` to the case and the message. |
| 2c | **Two colours.** `Invalid` red, `NotFound` yellow. `Unavailable` always shows the generic error with an ID. | T-2's `ColorOf`, exhaustive over the cases under C# 15. |
| 3 | **Every refusal is ephemeral.** Successful actions stay public. | F-5's boundary applies the rule. MOD-1, SIN-1, LVL-3 and CC-2 as proposed. "Sin not found" gets one rendering. |
| 3b | **Warn: DM first, then the reply.** | T-1 as sketched. The reply can say whether the DM was delivered. No after-reply effect. |
| 4 | **Validate in the command.** The owner confirms that DSharpPlus hides a converter's error message. | F-6's converter base is dropped, and no spike is needed. Each command calls `Create` for its validated inputs in its `Validation` step (Q2a), so the user sees the real message, collected with the other input errors. MOD-4 and CC-4 follow this. LVL-3's `PositiveXpAmount.Create` in the input already fits. |

## B. Schema before the first deploy

| Q | Decision | Effect on the findings |
|---|----------|------------------------|
| 5 | **Keep all Settings history.** Differs from D-10. Every Settings table stays append-only, as `migration-strategy.md` says. Locks and mutes were already decided: `ClosedAt` on the xAdded rows, with the xRemoved events kept as the audit trail. | D-10 and D-11 are declined. S-5, S-13 and KI-103 stay as decided. The tombstone types, `Inherit` and `GuildSettingDefault` stay. Latest-row reads stay, served from the cache (SET-1/SET-3). D-14's positional-record cleanup is unaffected. |
| 6 | **Edit the undeployed migrations in place**, as decision 30 already does for `ModelRedesign`. No squash. A dev database has run them; it is rebuilt (dropped and recreated from `main`'s backup) after the edits, not patched. | P-12 (where it still applies, see Q9) and P-13 are done as edits. Remove the five create-then-drop index pairs, both the creates in the earlier migrations and the drops in `DropRedundantIndexes`. |
| 7 | **`bigint` ids with the first deploy.** Run the production size query first, to estimate how long the rewrite takes. | P-9 goes ahead in both contexts. `leaderboard_view` is dropped and recreated around it, and KI-31's `decimal?` workaround goes. P-11's parameters are `bigint`. |
| 8 | **Re-pardons stay, to correct a pardon's wording.** The pardon date never changes. Model it like `SinReasonHistory`: `Pardon` becomes 1:1 with `Sin` (key `SinId`, who pardoned and when), plus a new `PardonReasonHistory` (reason, actor, time). A second `/Pardon` on a pardoned sin appends a reason row. | Replaces D-12's options (a)/(b). The migration goes in `ModelRedesign`: each sin's earliest pardon row becomes the `Pardon`, and every row becomes reason history. SIN-2's single projection reads the `Pardon` plus the latest reason. Defect 7 (mixed rows) is fixed by construction. |
| 8b | **Show the original pardoner** and the original date, with the latest reason wording. | `/SinLog` and `/Publish Unban`. |
| 9 | **Readable string discriminators.** Differs from P-12. Keep `ModelRedesign`'s strings for `XpHistory.Type` and `MessageHistory`, and accept the rewrite and the UPDATE at deploy. | P-12 is declined. Combine the deploy rewrite with P-9's rewrite where the migration allows, so each table is rewritten once. |
| 10 | **Take the obsolete types out of the EF model now.** The tables stay until P-7's release drops them with raw SQL. | D-20 as proposed. |
| 11 | **Drop `MessageHistory.GuildId` and `Pardon.GuildId`. Keep `Reaction.GuildId`** for the planned per-guild reaction features. | D-19, P-14 and LOG-10 for the two columns. `Pardon.GuildId` goes as part of the Q8 rework. |
| 12 | **Keep the leaderboard materialized view.** Differs from LVL-2. The live query on `main` takes 5–10 s (30–60 s before a recent rewrite), so the view keeps `/Leaderboard` responsive. | LVL-2 is declined. Still worth doing inside the view: a tie-break in `ROW_NUMBER` (defect 23), and the fallback that folds the count into the rank query to save a round trip. |
| 12b | **Members with 0 XP stay off the leaderboard.** | — |

## C. Hot path and caching

| Q | Decision | Effect on the findings |
|---|----------|------------------------|
| 14 | **One `MessageCreated` pipeline.** It builds a shared `MessageContext` once, runs the lock check first, then spam, XP, logging and custom commands, and reports failures by step name. | HOT-3 and HOT-4 part 1 as proposed. |
| 13 | **Keep `GuildSetting` as key/value strings.** Information only, not a finding. SET-1's cached `GuildConfig` parses each value once per cache fill. | — |
| 14b | **System messages skip the spam filter.** The shared gate passes only regular messages and replies. | Fixes defect 15. |
| 15 | **A message in a locked thread is logged, with the bot recorded as deleter, and skips XP and custom commands.** The delete costs no PluralKit call and no audit-log read. | HOT-4 as proposed, through the `BanHandoff`-style handoff to `DeleteMessageEvent`. Settles defect 16. |
| 16 | **Raw SQL upsert for reactions.** One statement, in one query helper, with a test. | P-11 and HOT-6 (merged). |
| 17 | **Don't trust DSharpPlus's "before" values. Verified 2026-11-10** against `5.0.0-nightly-02614` (commit `6d4bff4d`): `OnGuildMemberUpdateEventAsync` updates the shared user cache before raising the event, and `DiscordMember.Username`/`.AvatarHash` read through that cache, so `UsernameBefore`/`AvatarHashBefore` always equal the new values. `NicknameBefore` is correct only for a cached member, and the event doesn't say which case applies. | LOG-1's merge into one handler with a pure `Diff` stays. P-10's skip based on before/after is declined. Optional follow-up for the owner: a Grimoire-owned in-memory "last stored nickname/username/avatar" per member, filled from the database on a miss, would remove the queries without depending on DSharpPlus. Consider reporting the bug upstream. Re-check on DSharpPlus upgrades. |
| 17b | **Case-only changes count.** Compare ordinally, so a change only in letter case is recorded and logged. | LOG question 1. |
| 17c | **Record user history only while User Log is on.** `MemberAdded` and the startup backfill follow the module too. | Differs from the review's suggestion. LOG question 2. |
| 18 | **In-memory XP cooldown.** The owner notes it's safe at any scale: with sharding, each guild is handled by exactly one instance. The XP total is not cached. | HOT-1 and SET-4 (merged) as proposed; fixes KI-74. |
| 17d | **Keep a Grimoire-owned last-known cache for user logs**: each member's last stored nickname, username and avatar, filled from the database on a miss and updated on every write. Safe under sharding. | Replaces P-10's skip. Member updates that change none of the three (role grants, including the bot's reward grants) make no query. |
| 19 | **Settings expire after 1 hour instead of 1 day.** | SET-9/SET-10. Any stale value from a fill that overlaps a write lasts an hour at most. |
| 20 | **Spam tracker: `ConcurrentDictionary`** with an immutable `SpamState` per member, atomic updates, a pure scoring function and a periodic prune of idle entries. | SET-8 and SIN-11 (merged). |
| 21 | **Autocomplete ranks prefix matches first, then contains, then fuzzy.** It runs in memory over the guild's cached command list (CC-1: one entry per guild, loaded with a single query on a miss, cleared by Learn/Forget/Revert, 1-hour expiry as in Q19). A pure in-memory Levenshtein runs only when nothing matches by prefix or contains. | CC-1 as proposed, and SET-5 is moot. The `fuzzystrmatch` extension is dropped in a later migration. |
| 22 | **No write-behind batching.** | HOT-10 is declined. |

## D. Configuration and infrastructure

| Q | Decision | Effect on the findings |
|---|----------|------------------------|
| 23 | **Production takes the token and database settings from environment variables, and the non-secret settings from the shipped `appsettings.json`.** | SH-1 is required before the deploy. Delete the extra `ConfigurationBuilder` block in `Program.cs`. `CreateDefaultBuilder` already loads `appsettings.json` first and the environment variables after it, which is exactly this setup. With the block as it is, the file's `"token": ""` and `"Grimoire": ""` win over the environment variables. `main` has the same block, so check why production works today: its shipped file may differ. |
| 23b | **The tracked password is for a local dev database only.** No rotation needed. | Replace it with a placeholder in `Grimoire/appsettings.json`, as CLAUDE.md now says. Moving the file to `appsettings.example.json` is unnecessary, because production ships it on purpose. |
| 24 | **PluralKit: a token bucket with a small queue.** A delete handler may wait up to about 10 s in a burst. | SH-2 as proposed. Fixes defect 3. |
| 25 | **Sends that create something aren't retried.** That means new messages and log posts, plus initial interaction responses past Discord's 3-second window. Retries stay for reads and idempotent calls. A rare lost post is preferred over a rare duplicate. | SH-15 as proposed. Fixes defect 11. |
| 26 | **One guild log message record with factory methods**, timestamped when the event happens, not when it's posted. | SH-10 as proposed. Fixes defect 27. |
| 26b | **The log gate is a builder overload on `GuildLog`**, so no log can skip it. `GuildLog` gains a `SettingsModule` dependency. | LOG-2 as proposed. Fixes defect 17. |
| 27 | **Use primary-constructor parameters directly.** Don't copy them into `readonly` fields. This is a repo-wide rule; relax the editorconfig `this.` requirement for them. | LVL-12, applied everywhere. |
| 28 | **A second, non-retrying `DbContext` factory for the few large streaming reads**: log cleanup, purge and backfills. Everything else keeps `EnableRetryOnFailure`. | P-15. Principle 8's streaming applies to reads through that factory. Its transient failures become `Unavailable` (Q1). |
| 29 | **Track splitting `GuildLogPublisher` by channel for later.** It isn't part of this update. | Add it to the backlog. |

## E. Feature behaviour

| Q | Decision | Effect on the findings |
|---|----------|------------------------|
| 30 | **A blank reason means no reason, in every moderation command.** One optional-reason type runs from the parameter to the stored row and the embed. `/Lock` stops refusing blank reasons. | MOD-4 as proposed. Fixes defect 21's reason half. |
| 31 | **Lock and mute factories take `setAt` + `ModerationDuration`.** They compute `EndTime` and still store it, so the expiry queries and their partial indexes don't change. | MOD-5 as proposed. The three "end before start" checks go. |
| 32 | **Drop `ModerationRank`'s self-exemption.** Acting on yourself is always refused. | MOD-2 as proposed. |
| 33 | **`/Publish` with no ban log says that no log is set and names `/ModSettings PublicBanLog`.** "Not set" and "no access" get separate messages. | SIN-4 as proposed. SIN-74 changes. Settles defect 19. |
| 34 | **The two sin counts measure different things and keep separate filters.** `/SinLog type:Mod` counts the sins a moderator issued, over all time. `/UserInfo` counts the sins a user received inside the auto-pardon window, and now leaves out pardoned sins. | SIN-5's shared counting helper, with the filters kept at the call sites. Settles defect 20. |
| 34b | **`/UserInfo` shows Kicks too**, so both embeds show the same four types. | — |
| 35 | **Spam mutes go through `/Mute`'s shared act step**, including KI-55's order, with the `System` actor. | SIN-12 as proposed, after MOD-1. Fixes defect 28 (one actor representation). |
| 36 | **Deliberate: a non-moderator's text `/SinLog` gets no reply,** so a user can't expose their own sins in a public channel. It's not a defect. A future update could let a user ask from a DM, with the guild id or a picker of shared guilds. | SIN-3 keeps its other parts; drop "fixes non-moderator text commands". Defect 18 is withdrawn. Add the DM idea to the backlog. |
| 37 | **Still treat it as a proxy when PluralKit omits the ids.** PluralKit's docs (models page, Message model) say `system`/`member` are null "if the member associated with this message was deleted". Privacy isn't documented to hide them. Delete the dead "Private" fallback, and keep the ids required on `ProxiedMessageLink`. When PluralKit confirms a proxy but the member is gone, the original's delete isn't logged as a member delete, and no link is saved. No migration. | LOG-3 and LOG-4 as proposed, plus this branch in `ToLink()`/the handler. |
| 38 | **Forget a post at once when Discord says it's gone.** Deleted, Gone and Refused are final. An outage is `Unavailable` and raises the tick's alert instead of using up tries. | LOG-6 as proposed. Two `LogCleanupTests` change. Fixes defect 4. |
| 39 | **Delete and edit logs use the stored avatar** (through the Q17d last-known cache), with no lookup. | LOG-5 as proposed. |
| 40 | **Keep the `option` choices on `/Reclaim` and `/Leaderboard` for now.** Inputs that the chosen option doesn't need (`option:Top user:@x`, `option:All amount:500`) are still ignored. Input that the action needs and is missing (`option:User` with no user) is still refused. The owner may revisit this in a later UX pass. | LVL-8 is declined. Defect 25 is withdrawn. The enum guard stays. |
| 41 | **Reward self-healing is intended.** Rewards are re-applied on every gain, so a manually removed reward role comes back. | LVL-5's `PlanRewards` keeps this behaviour. |
| 41b | **The minimum reward level is 1.** `/Rewards Add` refuses level 0. | — |
| 42 | **Already decided in project memory (custom command access):** allow and deny role rows stay. Combined rules come later through a modal-based editor, not through new slash options. | CC-3/CC-7's `CommandAccess(Allow, Deny)` type is fine. Don't replace `restricted_use` with an `access` slash choice now. |
| 43 | **`%Message` is passed through exactly as typed.** Only the first two words are split off. | CC-8 as proposed. |
| 44 | **`/Command` failure is an ephemeral "not found",** which doesn't reveal whether a restricted command exists. | CC-9 as proposed. Follows Q3. |
| 45 | **Keep `CustomCommandUsages` forever.** | No retention. |

## Where the decisions differ from the review

- **F-1** is rewritten. Outages are a typed `Unavailable` failure, not exceptions (Q1).
- **F-2** changes. `Validation` and `Result` stay separate, each with a defined job (Q2a), and F-6's converter is dropped (Q4).
- **Declined:** D-10 and D-11 (Q5), P-12 (Q9), LVL-2 (Q12), P-10's before/after skip (Q17), HOT-10 (Q22) and LVL-8 (Q40).
- **Withdrawn defect candidates:** 18 (Q36, deliberate) and 25 (Q40).
- **New work from the answers:**
  - the `PardonReasonHistory` model (Q8)
  - the last-known member cache (Q17d)
  - the non-retrying `DbContext` factory (Q28)
  - the PluralKit deleted-member branch (Q37)
  - the `/UserInfo` count changes (Q34)
  - the minimum reward level of 1 (Q41b)
- **Backlog:** partition `GuildLogPublisher` by channel (Q29); a DM-based `/SinLog` (Q36); a leaderboard/reclaim UX pass (Q40); report DSharpPlus's member-update before-values bug upstream (Q17).
