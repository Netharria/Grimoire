# Section 12: Roll-up

Written 2026-11-09 from sections 1 to 11, `review-decisions.md` and `docs/testing/known-issues.md`. No source code was
changed. Code was read only where two sections disagreed (see "Contradictions resolved").

## 1. Summary

- **145 findings were raised and 126 survive** after 19 duplicates were merged: 10 high, 38 med, 67 low and 11 optional.
- **Biggest wins.** (1) One error rule and one command boundary (F-1, F-2..F-5). Outages stay exceptions, `Result` gets two cases, and
  every command goes through `ctx.RunAsync`. That is about −670 lines in the foundations and about −1,200 more in the
  feature rewrites built on it. (2) The data model, cut while the post-`main` migrations still haven't run in production:
  Settings as plain state tables (D-10, D-11, D-14) and the obsolete types removed from the model (D-20). About −1,400 lines.
  (3) The hot path. HOT-1, HOT-2, SET-1 and SET-3 take an ordinary message from 3 round trips and 9 cache reads to 1 and 5.
  P-11 and LOG-1 remove most of the reaction and member-update queries. (4) `bigint` ids (P-9) make every index about three
  times smaller.
- **About 25 defect candidates** surfaced in the design review and aren't logged yet. SH-1 (environment variables lose to
  the tracked `appsettings.json`) and the tracked password are the most urgent.
- **Estimated net change: about −4,150 hand-written lines.** That is about −3,300 without D-20's obsolete types, with a
  plausible range of −3,500 to −4,500. A further −1,500 generated designer lines go with P-13. The new migrations' generated
  code isn't counted.

## 2. Merged findings index

**Phase** refers to the plan in part 5. **Needs** follows the section files: `net11.0`, migration, owner decision, spike,
or nothing. "Merged" lists the IDs folded into the canonical finding. The merged IDs keep their text in their own section
files, so read them there for detail.

### Merges

| Canonical | Merged into it | Why this one |
|---|---|---|
| HOT-1 in-memory XP cooldown | SET-4, and section 10's mention | HOT-1 also fixes KI-74 with a compare-and-swap, and reads `Max` and `Sum` in one query on a miss. Prune on any timer, not on `LeaderboardRefreshBackgroundTask`, because LVL-2 may delete that task |
| SET-8 in-process state out of `HybridCache` | SIN-11 | SET-8 also covers the audit-log counter race. Impact raised to med (SIN-11's). The choice of store stays open (Q21) |
| CC-1 per-guild custom command catalog | SET-5, P-17 | CC-1 caches whole definitions, which is a superset of SET-5's names. P-17's query shape survives only if CC-1 is declined |
| P-11 reaction upsert | HOT-6, LOG's section 5 note | Section 3 writes it with P-9's `long` parameters. HOT-6's EF-only fallback is the answer if Q17 is "no raw SQL" |
| LOG-1 one member-update handler with a pure diff | P-10, SET-6 | The `*Before` skip from P-10/SET-6 becomes `Diff`'s first step, and the merged handler then makes at most one query |
| SET-3 per-guild override maps | HOT-5 | Section 5 asked for section 4's version to be kept |
| SIN-2 one sin projection | P-18 | Same subqueries. SIN-2 also covers the reason subquery and `/Publish` |
| D-12 pardon and reason shape | none | The **shape** decision. SIN-2 projects whatever D-12 chooses. One owner question covers D Q2, SIN Q1 and P Q4 |
| D-19 drop write-only `GuildId` columns | P-14, LOG-10 | `Reaction.GuildId` is one question (D Q5, P Q3, LOG Q6) |
| LVL-2 leaderboard view or live query | P-19, and P-16's refresh half | P-19's `(GuildId, Rank)` index is the branch to take if the view stays. P-16 keeps only ledger compaction |
| SET-1 one `GuildConfig` per guild | D-21, HOT-3's `GetModuleStateAsync` entry | All three removed `GetAllModuleState`/`Result.WhenAll`. Modules live in the snapshot as an `IsEnabled(Module)` lookup, so adding a module edits one place, which was D-21's point |
| SET-2 cached-read and write helpers | HOT-9 | SET-2's sketch already passes tuple state and returns `ValueTask` |
| SET-10 cache configured once | SET-9 | It is the same one-line default (1-hour expiry) |
| LVL-5 pure reward plan | SET-11 | The batch orphan write is part of the `ApplyRewards` rewrite |
| D-23 `closed` on every hierarchy | SET-12, P-20, CC-13 | One table, one step after net11.0. SET-12's net10 tweak (`Key` throws `InvariantViolationException`) moves to phase (a) |
| F-1 outages as exceptions | none, but SET-10 is its enabler | F-1 is accepted or refused on its own. SET-10 is worth doing either way |

### Index

| ID | Finding | Tag | Impact | From | Merged | Needs | Phase |
|---|---|---|---|---|---|---|---|
| F-1 | Infrastructure failures stay exceptions; `Result` only for domain outcomes | pipe | high | 1 | | owner decision | b |
| F-2 | Merge `Validation<T>` into `Result<T>` | remove | med | 1 | | owner decision | b |
| F-3 | `Result<T>` with two cases, `Match` on each case | pipe state | med | 1 | | nothing (net11.0 for `closed`) | b |
| F-4 | Drop `Error.Code`/`Target`; the case carries the kind | state | med | 1 | | owner decision | b |
| F-5 | One command boundary: `ctx.RunAsync` and `Reply` | pipe state | high | 1 | | owner decision | b |
| F-6 | Validated parameter types through one converter base | state pipe | med | 1 | | spike | b |
| F-7 | Remaining unwraps: total conversions and typed producers | state | low | 1 | | nothing | b |
| F-8 | `CommandHandler` renders errors through KI-21's path | pipe | med | 1 | | nothing | a |
| F-9 | Background items stop dropping `Result` failures | pipe | med | 1 | | nothing (after F-3) | b |
| F-10 | "This message isn't for me" is not an error | perf pipe | med | 1 | | nothing | a |
| F-11 | CS8509 as a build error, in one `Directory.Build.props` | state cs15 | low | 1 | | nothing | a |
| F-12 | Background services log under their own category | remove | low | 1 | | nothing | a |
| F-13 | `ReplyAsync` overloads instead of mode parameters | state | optional | 1 | | nothing (after F-5) | d |
| D-10 | Settings tables hold state, not event logs | remove state perf | high | 2 | | owner decision, migration | c |
| D-11 | XP ignore list stored like the diagnostics ignores | remove state | med | 2 | | migration | c |
| D-12 | Sin reason and pardon: one row, or columns. Fixes the mixed-row display | remove state | med | 2 | | owner decision; migration for (b) | a (bug), c (b) |
| D-13 | S-12's zero-id rule in `Grimoire.Domain`: `Sin.ForX` returns `Sin` | state remove | med | 2 | | nothing | a |
| D-14 | Factories with no rule left become positional records | remove | med | 2 | | nothing (after S-12) | c |
| D-15 | Private constructors so factories can't be bypassed | state | low | 2 | | spike (EF materialisation) | a |
| D-16 | "The bot did it" as `ModerationActor` in Settings | state | low | 2 | | migration | c |
| D-17 | Drop the `XpHistory` typed getters and `NegativeXpAmount` | remove | low | 2 | | nothing | a |
| D-18 | `ReactionEmoji` trusted on read | state | low | 2 | | nothing | a |
| D-19 | Drop the write-only `GuildId` copies | remove perf | low | 2 | P-14, LOG-10 | migration, owner decision | c |
| D-20 | Obsolete types out of the EF model now; tables stay | remove | med | 2 | | owner decision, migration (snapshot only) | c |
| D-22 | `Nickname.Create` is dead | remove | optional | 2 | | nothing | a |
| D-23 | `closed` on every domain hierarchy | cs15 state | med | 2 | SET-12, P-20, CC-13 | net11.0, spike | f |
| P-9 | Snowflake ids as `bigint`, not `numeric(20,0)` | perf remove | high | 3 | | owner decision, migration | c |
| P-11 | A reaction is one upsert | perf | med | 3 | HOT-6 | owner decision | e |
| P-12 | Integer discriminators on `XpHistory` and `MessageHistory` | perf remove | med | 3 | | owner decision, migration | c |
| P-13 | Don't create the indexes `DropRedundantIndexes` drops | remove perf | low | 3 | | owner decision | c |
| P-15 | `AsAsyncEnumerable` doesn't stream under retry; buffer plainly | perf remove | low | 3 | | nothing | a |
| P-16 | XP ledger compaction, if the numbers call for it | perf | low | 3 | | spike | e |
| P-21 | `Add` where `AddAsync` isn't needed | remove | optional | 3 | | nothing | a |
| SET-1 | One cached `GuildConfig` per guild | perf remove state | high | 4 | D-21, HOT-3 part | nothing | e |
| SET-2 | Helpers for cached reads and append-then-invalidate writes | remove pipe | med | 4 | HOT-9 | nothing | e |
| SET-3 | Per-guild override maps and one channel-tree resolver | perf remove | med | 4 | HOT-5 | nothing | e |
| SET-7 | Delete `IsChannelLocked` and its cache key | remove | low | 4 | | nothing | a |
| SET-8 | Spam tracker and audit-log counter out of `HybridCache` | state perf | med | 4 | SIN-11 | owner decision | e |
| SET-10 | Configure the cache once (1 hour, fail-safe) | pipe remove | med | 4 | SET-9 | spike | a |
| HOT-1 | XP cooldown in memory, claimed atomically | perf state | high | 5 | SET-4 | owner decision | e |
| HOT-2 | Save the logged message without a separate `COMMIT` | perf | high | 5 | | spike | e |
| HOT-3 | One `MessageCreated` pipeline with a `MessageContext` | perf remove state | med | 5 | | owner decision | e |
| HOT-4 | Locked thread or spam: skip the other steps; `BotDeletions` handoff | perf | med | 5 | | owner decision | e |
| HOT-7 | Skip `MessageUpdated` events that aren't edits | perf | med | 5 | | spike | a |
| HOT-8 | No PluralKit call for messages it can't have proxied | perf | low | 5 | | nothing | a |
| HOT-10 | Write-behind batching for message rows | perf | optional | 5 | | owner decision | not planned |
| SH-1 | `Program.cs` re-adds `appsettings.json` above the environment variables | remove | high | 6 | | owner decision | a |
| SH-2 | PluralKit limiter queues instead of refusing | perf state | med | 6 | | owner decision | a |
| SH-3 | One `OrNullIfNotFound`; drop the unused overloads | remove | low | 6 | | nothing | a |
| SH-4 | `GetUserAvatar` without the second lookup | perf | low | 6 | | nothing | a |
| SH-5 | Audit-log parser: a claiming name, returns a moderator, no blanket catch | pipe state | low | 6 | | nothing | d |
| SH-6 | Interfaces with one implementation; singletons; redundant `AddHttpClient` | remove | low | 6 | | nothing | a |
| SH-7 | Shared helpers take the time or a `TimeProvider` | state | low | 6 | | nothing | a |
| SH-8 | Mentions as extension properties on the id types | remove | optional | 6 | | nothing | d |
| SH-9 | Pure `SplitForFields`, no regex, no static logger | remove pipe | low | 6 | | nothing | a |
| SH-10 | One `GuildLogMessage` record with factories | remove state | low | 6 | | owner decision | d |
| SH-11 | Warnings bypass the urgent alert queue | state perf | low | 6 | | nothing | a |
| SH-12 | Drop `GuildIssueThrottle`; write the merge rule once | remove | low | 6 | | nothing | a |
| SH-13 | `GuildAdded` and `UpdateAllGuilds` as one sync | remove | low | 6 | | nothing | d |
| SH-14 | `CommandHandler`: no zero guild id, one alert path, pure formatter | state | low | 6 | | nothing | a |
| SH-15 | Separate retry policies for idempotent and non-idempotent calls | state | low | 6 | | owner decision | a |
| SH-16 | Helpers with a single caller or an unused parameter | remove | low | 6 | | nothing | a |
| SH-17 | `DiscordImageEmbedService`: frozen set, pooled buffer, no flag | remove cs15 | low | 6 | | nothing (net11.0 for `[with]`) | d |
| MOD-1 | Action commands on T-1 | pipe | med | 7 | | owner decision (Q4) | d |
| MOD-2 | `ModerationRank` pure, including the self-check | pipe state | low | 7 | | owner decision | d |
| MOD-3 | One moderation embed and one sin save | remove | low | 7 | | nothing (after S-12) | d |
| MOD-4 | One optional-reason type from parameter to event | state | med | 7 | | spike (F-6), owner decision | d |
| MOD-5 | `ModerationDuration` instead of `(DurationType, int)` | state | med | 7 | | owner decision | d |
| MOD-6 | Expiry polls re-scan all history every 5 s | perf | med | 7 | | spike; mostly moot under D-10 | d |
| MOD-7 | Drop `ExpiredItemTriage`'s two bools | state remove | low | 7 | | nothing | a |
| MOD-8 | Background tasks: injected singletons, constant reasons | remove | low | 7 | | nothing (after S-12) | d |
| MOD-9 | `MuteRoleResolver` returns the role | remove | low | 7 | | nothing | a |
| MOD-10 | Mute lookups: parallel awaits, cached gate first | perf | low | 7 | | nothing | a |
| MOD-11 | `/Unban`: no hand-written outage reply; cancel the handoff on every failure | pipe | low | 7 | | nothing | a (leak), d (rest) |
| MOD-12 | `BanHandoff` as one generic table | remove | optional | 7 | | nothing | a |
| MOD-13 | Delete `OverwriteChannelResult` | state remove | low | 7 | | nothing | a |
| MOD-14 | Lock bits without `long.Parse(ToString())` | remove | optional | 7 | | nothing | a |
| SIN-1 | Sin admin commands on T-1, with `FindSinnerAsync` | pipe | med | 8 | | nothing (after F-5) | d |
| SIN-2 | One sin projection; pardon as one nullable value | state perf | med | 8 | P-18 | owner decision (Q10) | d |
| SIN-3 | `/SinLog`: `Mod` dispatched first; shared pager | pipe remove | med | 8 | | owner decision | d |
| SIN-4 | `/Publish`: two errors, no try/catch, one type value | pipe state | low | 8 | | owner decision | d |
| SIN-5 | "Count sins by type" written once | remove | low | 8 | | owner decision | a |
| SIN-6 | Diagnostics findings as cases, not kind plus ids | state cs15 | low | 8 | | nothing (net11.0 last step) | d |
| SIN-7 | `GuildSnapshot.MuteRole` as three cases | state | low | 8 | | nothing | a |
| SIN-8 | Effective permissions without per-pair rescans | perf | low | 8 | | nothing | a |
| SIN-9 | One shape for the diagnostics ignore list | remove | low | 8 | | nothing (after S-12) | d |
| SIN-10 | `CheckSpam` returns a verdict; a constant reason | state perf | low | 8 | | nothing | a |
| SIN-12 | Spam mute through `/Mute`'s act step | remove | low | 8 | | owner decision (after MOD-1) | d |
| SIN-13 | `ToggleableModule` instead of a mirror enum and a runtime refusal | state | low | 8 | | nothing | a |
| SIN-14 | `ChannelSettingCommand` without `null`/`bool` modes | state remove | low | 8 | | nothing (after F-5) | d |
| SIN-15 | `/UserInfo`: a `Profile` record; work only when needed | perf | low | 8 | | nothing | d |
| LOG-1 | One member-update handler with a pure diff | remove perf state | high | 9 | P-10, SET-6 | spike, owner decision | d |
| LOG-2 | Gate log building in `GuildLog` | perf remove | med | 9 | | owner decision (where it lives) | d |
| LOG-3 | One nullable `ProxyAuthor` | state | low | 9 | | nothing | a |
| LOG-4 | PluralKit returns the typed link | state pipe | low | 9 | | nothing | a |
| LOG-5 | Delete and edit embeds as pure functions; parallel fetch | pipe perf | low | 9 | | nothing (after LOG-2) | d |
| LOG-6 | Cleanup classifies each delete and stops swallowing outages | pipe state | med | 9 | | owner decision, spike (delete by id) | a |
| LOG-7 | Cleanup bounded per tick and recorded as it goes | perf state | low | 9 | | nothing (after LOG-6) | a |
| LOG-8 | `InviteService` replaces its snapshot; pure diff | state remove | med | 9 | | nothing | d |
| LOG-9 | One log table per module drives Set, View and names | remove | low | 9 | | nothing | d |
| LOG-11 | Join-log invite text as one expression | pipe | optional | 9 | | nothing | a |
| LOG-12 | Stop logging and rethrowing what the boundary logs | remove | low | 9 | | nothing | a |
| LVL-1 | Ignore list cached as three id sets | state perf remove | med | 10 | | nothing | d |
| LVL-2 | Leaderboard: materialized view or a live per-guild query | perf remove | med | 10 | P-19, P-16 (refresh) | owner decision, spike, migration | c |
| LVL-3 | Leveling commands on T-1 | pipe state | med | 10 | | nothing (after F-5) | d |
| LVL-4 | `GainUserXp`'s outage block goes | pipe remove | low | 10 | | nothing (after F-1) | b |
| LVL-5 | `ApplyRewards` as a pure plan, then I/O; batch orphan removal | pipe perf | low | 10 | SET-11 | nothing (after S-12) | d |
| LVL-6 | Rewards cached as a level-sorted table | state perf | low | 10 | | nothing | d |
| LVL-7 | One level-threshold function | state remove | low | 10 | | nothing | a |
| LVL-8 | The optional parameter carries the mode (`/Reclaim`, `/Leaderboard`) | state | low | 10 | | owner decision | d |
| LVL-9 | User-command-channel visibility written once | remove pipe | low | 10 | | nothing (after F-5) | d |
| LVL-10 | `/Level`: mention without fetching; parallel lookups | perf | low | 10 | | nothing | a |
| LVL-11 | Take `LevelSettings` directly with a choice provider | remove | low | 10 | | spike | d |
| LVL-12 | Field copies of primary-constructor parameters | remove | optional | 10 | | owner decision | a |
| CC-1 | Cache each guild's current command definitions | perf remove | high | 11 | SET-5, P-17 | owner decision (ranking) | e |
| CC-2 | `/Commands` subcommands on T-1 | pipe | med | 11 | | nothing (after F-5) | d |
| CC-3 | Learn: an `access` choice and one `CommandAccess` value | state | low | 11 | | owner decision (after C-2) | d |
| CC-4 | Revert takes a typed version | state remove | low | 11 | | spike (F-6) | d |
| CC-5 | `/Commands History` uses `ReplyPagedAsync` | remove | low | 11 | | nothing | a |
| CC-6 | Role rows stop restating their version's key | state remove | low | 11 | | spike (shadow keys) | d |
| CC-7 | Access policy on `CustomCommand` | state | low | 11 | | nothing (after CC-1) | d |
| CC-8 | The `!` processor as one straight-line method | pipe perf | med | 11 | | owner decision (`%Message`) | e |
| CC-9 | `/Command` without a defer | pipe | low | 11 | | owner decision (after CC-1) | e |
| CC-10 | Leaderboard and Stats round trips | perf | low | 11 | | nothing | a |
| CC-11 | Rendering moves to `CustomCommandRenderer` | remove | optional | 11 | | nothing (after C-3) | d |
| CC-12 | Version autocomplete in one query | perf | optional | 11 | | nothing | a |

### Contradictions resolved

- **F-3 says no caller passes `onNotFound` and nothing reads `NotModified`.** That is stale since D-3 (rank 25). The code
  now has four uses: `ReplyResultAsync` (`onNotModified`), `MuteAdminCommands.Create` (`onNotModified`), `UnlockChannel`
  (`onNotFound`) and `CustomCommandSettings.Stats` (`onNotFound`). The finding still holds. The two `onNotModified` sites
  already treat it as success, which F-3 makes automatic. The two `onNotFound` sites become `Error.NotFound`, which T-1
  renders yellow. F-3 has four more call sites to touch than it says.
- **`AddMessageEvent`'s save: 1 round trip (section 3) or 2 (section 5).** Both read the same single `SaveChangesAsync`.
  They disagree about whether Npgsql sends `COMMIT` separately, and the code can't settle that. HOT-2's spike decides it.
  Until then, use section 5's count.
- **D-21 deletes `GuildModuleState`, while SET-1 and HOT-3 keep it inside the snapshot.** The real target is
  `GetAllModuleState` and its `Result.WhenAll`, which all three remove. Merged into SET-1. Keep module flags as a set
  behind `IsEnabled(Module)` so a new module touches one place.
- **The `PositiveXpAmount` input.** F-6 lists it for the converter, but LVL-3 points out that the option is an `int`, not
  a string. Follow LVL-3: put `Create` in `RunAsync`'s input. F-7's `PositiveXpAmount.Negate()` isn't needed once D-17
  lands, because `ReclaimedXp.Create` takes the positive amount.
- **HOT-1 prunes on the leaderboard task's tick.** LVL-2 may delete that task. Prune on any existing timer, or not at all.
- **SET-8 and SIN-11 propose different stores** (a `ConcurrentDictionary` with a content hash, or `IMemoryCache`). This is
  left to the owner (Q21).

### Findings that assume streaming (P-15)

With `EnableRetryOnFailure` on, EF buffers every query, so `AsAsyncEnumerable` doesn't stream. **No surviving finding
depends on EF streaming.** MOD-13's `IAsyncEnumerable<DiscordChannel>` streams REST calls, not a query, so it is
unaffected. SET-3 deletes the two `IAsyncEnumerable` settings readers. Two things outside the findings need updating:
S-11's "stream rows as they arrive" comments over `ToListAsync`, and principle 8 in `feedback_coding_principles`, which
should say that EF queries buffer while retry is on (Q29).

## 3. Defects found during the design review (candidates for known-issues)

Not yet in `known-issues.md` or `review-decisions.md`. The owner decides what to log. Severity uses known-issues' scale.
"(decide)" marks behaviour that may be intended.

| # | Sev | Issue | Where | Finding |
|---|---|---|---|---|
| 1 | high | **Environment variables never override `appsettings.json`.** `Program.cs` builds a second configuration from the file and chains it last, so every key in the tracked file beats `TOKEN`, `CONNECTIONSTRINGS__GRIMOIRE` and the rest. The docker-compose setup as documented gets an empty token and a localhost database. Confirmed in `Program.cs:14-21` | `Grimoire/Program.cs` | SH-1 |
| 2 | high | **`Grimoire/appsettings.json` is tracked in git and contains a connection string with a password.** Confirmed with `git ls-files`. Rotate the password if it is used anywhere but a local dev database. Move the file to `appsettings.example.json` (closes I-18's missing-file half) | `Grimoire/appsettings.json` | SH-1 Q2 |
| 3 | med | **PluralKit lookups beyond 2 a second are refused, not queued.** The extra originals PluralKit deletes are logged as member deletes, and their `ProxiedMessageLink` is never saved, so the proxy's edit and delete logs show the webhook | `GrimoireServiceRegistration.cs:72-81` | SH-2 |
| 4 | med | **Log cleanup gives up during an outage.** Every exception, including a 5xx, a timeout or shutdown, counts as a failed try. After three ticks the row is forgotten, so a three-minute outage leaves those log posts up for good. No alert is raised | `CleanupLogsBackgroundTask.cs:68-117` | LOG-6 |
| 5 | med | **Background unlocks and unmutes drop a failed write.** A `Fail` from the unlock write is recorded as success and retried every 5 s with no backoff or alert. Once S-4 is in, a failed `RemoveMute` resends the "mute expired" DM and guild log on every tick | `LockBackgroundTasks.cs:89,116`, `MuteBackgroundTasks.cs:134` | F-9 |
| 6 | med | **(unconfirmed) Command exceptions after a public defer.** `CommandHandler.SendOrEditMessageAsync` calls `RespondAsync` on an interaction that has already been acknowledged. Either the "[ID …]" error lands publicly, or the user is left with "thinking…" | `CommandHandler.cs:148` | F-8 |
| 7 | low | **A second `/Pardon` mixes rows.** `/Pardon` doesn't refuse a sin that is already pardoned. `/SinLog` then shows the newest pardon's moderator with the oldest pardon's date, and `/Publish Unban` shows the newest reason with the oldest date | `SinLog.cs:107-125`, `PublishCommands.Unban.cs:35-46`, `PardonSin.cs` | D-12, SIN-2, P-18 |
| 8 | low | **`/Unban` leaks its handoff.** Any failure other than `ServerErrorException` (`UnauthorizedException`, for example) leaves `ExpectUnban` in place for 2 minutes, so an outside unban of the same user in that window is credited to this moderator | `RemoveBanCommand.cs:33-53` | MOD-11 |
| 9 | low | **The message content of a failed save goes into the logs at Error.** `AddMessageEvent` logs the whole message before rethrowing, so deleted members' text ends up in log files. `DeleteMessageEvent` logs expected races (duplicate or FK proxy link) at Error, and logs other exceptions twice | `AddMessageEvent.cs:85-97`, `DeleteMessageEvent.cs:94-110` | LOG-12 |
| 10 | low | **Bursts of warnings can drop urgent alerts.** Every alert shares one 256-slot queue that drops writes when full. A `CannotReadAuditLog` warning per delete can fill it while an urgent post waits on Discord | `AlertService.cs:32-64` | SH-11 |
| 11 | low | **The retry policy fits neither initial responses nor sends.** A retried initial `RespondAsync` waits about 3 s and hits Unknown Interaction. A `SendMessageAsync` that gets a 502 after Discord created the message posts a duplicate. One bad send holds up every guild's log queue for up to about 45 s | `DiscordRetryPolicy.cs:16-24` | SH-15 |
| 12 | low | **The audit-log parser hides outages.** `catch (Exception) { return null; }` with no log at all | `DiscordAuditLogParserService.cs:59` | SH-5 |
| 13 | low | **The audit-log count is read and written without atomicity.** Two deletes handled at once can claim the same audit entry. This race sits beside KI-64 | `DiscordAuditLogParserService.cs:68-79` | SET-8 |
| 14 | low | **(not reproduced) A cache fill that overlaps a write caches the old value for a day.** A module just enabled can read as off until the next day or the next write | `SettingsModule.cs:26, 92-94` | SET-9 (in SET-10) |
| 15 | low | **The spam filter scores system messages** ("X joined", "X pinned a message"), because `SpamEvents` doesn't check the message type | `SpamEvents.cs:28-41` | HOT-3 |
| 16 | low | **(decide) Messages in a locked thread still earn XP, get logged and run custom commands** before the lock deletes them. The delete then costs a PluralKit call and an audit-log read | `LockedThreadEvents.cs`, `DeleteMessageEvent.cs` | HOT-4 |
| 17 | low | **The edit log builds embeds and looks up avatars when no edit log is set.** L-7's gate missed it | `UpdateMessageEvent.cs:79` | LOG-2, HOT-7 |
| 18 | low | **A text-command `/SinLog` from a non-moderator gets no reply** | `SinLog.cs:47-48` | SIN-3 |
| 19 | low | **(decide) `/Publish` with no public ban log says to check channel access.** SIN-74 pins this text on purpose | `PublishCommands.cs:102-142` | SIN-4 |
| 20 | low | **(decide) Two sin-count rules.** `/SinLog type:Mod` counts all time and includes kicks. `/UserInfo` uses the auto-pardon window, leaves kicks out and counts pardoned sins | `SinLog.cs:64-76`, `UserInfoCommands.cs:177-189` | SIN-5 |
| 21 | low | **Reasons are handled inconsistently.** A blank reason is dropped by Kick and Mute, refused by Lock, and defaulted to `""` by Ban. A reason with leading spaces is stored trimmed but shown untrimmed. The embeds have drifted apart ("Mod" against "Moderator", "None" against no field) | `KickUser`, `MuteUser`, `AddBanCommand`, `LockChannel`, `BanAddedEvent` | MOD-3, MOD-4 |
| 22 | low | **Moderation refusals are public.** Ban, Kick, Mute, Unmute, Lock, Unlock and Unban refuse with a public `ReplyAsync`, so KI-21's ephemeral rule doesn't reach them. This is the moderation counterpart of KI-80 | `Moderation/{Ban,Kick,Mute,Lock}` | MOD-1 |
| 23 | low | **Leaderboard ties swap places** between refreshes, because `ROW_NUMBER` has no tie-break | `20251025193658_LeaderboardView` | LVL-2 |
| 24 | low | **The two level formulas can disagree at extreme settings.** `IsAbove` computes in `Int128` and `GetXpNeededForLevel` in `long` | `LevelingSettingEntry.cs:52-64` | LVL-7 |
| 25 | low | **Options that are silently ignored.** `/Reclaim option:All amount:500` ignores the amount. `/Leaderboard option:Top user:@x` ignores the user | `ReclaimUserXp.cs`, `GetLeaderboard.cs` | LVL-8 |
| 26 | low | **`DiagnosticsIgnores.Key` throws `ArgumentOutOfRangeException`.** The rank 26 `InvariantViolationException` ruling missed it | `SettingsModule.Moderation.DiagnosticsIgnores.cs:103-108` | SET-12 (in D-23) |
| 27 | low | **Log timestamps record when the post went out, not when the event happened.** A backed-up queue makes them seconds late | `GuildLogMessage.cs:33` | SH-10 |
| 28 | low | **A spam mute is stored two ways.** The sin's actor is `System`, but the `MuteAdded` moderator is the bot's user id | `SpamEvents.cs:66` | D-16 |

Questions that read like defects but are behaviour choices, so they stay in part 7: case-only nickname changes and edits
aren't logged (LOG Q1); history is written only while User Log is on (LOG Q2); `%Message` collapses runs of spaces (CC Q3);
manually removed reward roles come back on the next XP gain (LVL Q3); a reward can be set at level 0 (LVL Q4);
`CustomCommandUsages` is never trimmed (CC Q5).

## 4. Reconciliation with `review-decisions.md`

Each item is a decided fix that a design finding changes, makes moot or conflicts with. **Choose** marks a decision the
owner has to make. Items that only say "done as part of X" need no decision.

### Conflicts: choose

| Decided | Design finding | What to choose |
|---|---|---|
| **S-4** (rank 14, done): outages become `Result.Fail`, and a failed ignore lookup counts as ignored | **F-1** reverses it: outages throw to the three boundaries that alert. **SET-10**'s fail-safe keeps cached settings serving for a day, so commands rarely see the outage | Keep S-4, or adopt F-1 with SET-10. The XP outcome is the same either way. Under F-1, S-4's eight wrapped methods and its "+40 lines" are undone. F-1 also closes **KI-105**. LVL-4, MOD-10, SIN-15 and HOT-3's `ValueTask` shrink only under F-1. F-9 matters more if S-4 stands |
| **D-3** (rank 25, done): `Match` keeps `onNotFound`/`onNotModified`, and four callers pass them | **F-3** removes `NotModified`. Its `NotFound` becomes `Error.NotFound` | Adopt F-3, and update the four call sites listed under "Contradictions resolved". `error-result-validation-strategy.md` needs rewriting for F-2 and F-3 |
| **S-9** (rank 25, done): keep `GuildSettingDefault` for a planned reset-to-default | **D-10**: reset-to-default becomes a row delete, and the type goes | Is a delete an acceptable "reset"? If D-10 is declined, SET-1 still parses `GuildSettingDefault` as default |
| **migration-strategy.md**: keep the append-only history | **D-10**: no reader of the history exists | Keep the history, or move to state tables (Q6) |
| **P-5/S-8** (rank 28, "Do"; the `DropRedundantIndexes` migrations exist, uncommitted) | **P-13**: never create those indexes, and delete both migrations. **D-10**: the Settings `SetAt` keys go anyway | Keep the drop migrations, or edit the undeployed creators (Q7). Under D-10, the Settings half disappears either way |
| **P-7** (later release): drop the legacy tables only after Settings' `Initial` has run | **D-20**: take the types out of the model now, and keep the tables | Confirm that this keeps to P-7's intent (Q11) |
| **S-13** ("Do, needs a design first") | **MOD-6** supplies the measurement and the guild filter. **D-10** removes the problem, because expired rows are deleted | Under D-10, drop S-13. Otherwise, MOD-6 steps 1 and 2 are S-13's design, and step 3 needs Q6's answer |
| **D-2/M-7** (rank 23, done): "each caller keeps one throw" | **D-13** removes those 5 throws. **D-12(b)** undoes D-2's `SinReasonHistory` block. **MOD-4** changes `/Lock`'s blank-reason refusal into "no reason" | D-13 needs no decision. D-12 (Q10) and the `/Lock` blank-reason change (Q34) do |
| **Branch review**: the 15-minute leaderboard lag is deliberate | **LVL-2**: a live per-guild query | Q13 |
| **X-6/X-8** (done): window logic, Moderation log | LVL-2 and LVL-3 keep both | none |
| **C-2** (decided, not ranked): deny lists through `restricted_use` plus roles | **CC-3**: an `access` choice option | Do C-2 first, then decide whether its option should be renamed (Q37) |
| **C-1** (done): `/Command` deletes the response on failure | **CC-9**: answer without a defer, and maybe reply ephemerally | Q38 |
| **I-3** (done): a refused PluralKit lookup is logged at Information and the delete logged as ordinary | **SH-2**: queue instead | Q25. I-3's catch stays for real outages |
| **SIN-74 test** pins `/Publish`'s access text | **SIN-4**: a clear "no public ban log" message | Q32 |
| **KI-66** (open) and its pinned test text | **LOG-9** changes the Set replies to display names | Accept the test-text change |
| **L-2/L-10** (done) and two `LogCleanupTests` | **LOG-6/7**: forget gone posts at once; name the 30/31-day constants | Q35 |

### Made moot or absorbed: no decision needed

- **I-4** (done, `ReplyResultAsync`) is the tail of F-5's `RunAsync`. Build it once in that shape. **I-19** ("Do") becomes
  `Visibility.Private` in F-5. Don't do it separately.
- **I-5/X-5** (done): F-1 turns their `GetOrElse` tails into plain awaits.
- **KI-21** stays the private-reply rule. F-5 applies it to every refusal (MOD-1, SIN-1, LVL-3, CC-2), and F-8 puts
  `CommandHandler` on it. KI-80 and KI-81 are fixed by LVL-3.
- **S-12** ("Do"): D-13 extends it to `Grimoire.Domain`, and D-14 follows from it. MOD-3, MOD-8, SIN-9 and LVL-5 need it.
  Land S-12, D-13 and D-14 together.
- **D-5/D-8** (done): D-14 removes the forwarding `Create` methods that D-5 left returning `T`.
- **D-6/S-1/D-1** (done): D-22 removes the one wrapper D-6 left out. D-18 extends S-1 to `ReactionEmoji`. LOG-4 and F-7
  reuse D-6's trusted factory.
- **S-2** (done): SET-1, SET-10 and F-1 make it structurally impossible to bring back.
- **S-5** (done): the latest-row helper becomes moot under D-10.
- **S-10** (done): D-23 takes the last arms. SET-7 deletes `IsChannelLocked`.
- **S-11** (done): correct its streaming comments (P-15).
- **KI-103**: SET-1 closes it for guild settings, by clearing the key on every path. D-10 closes it for the rest.
- **P-2** (done): D-17 edits `ReclaimedXp` in the same place.
- **P-3** (done, 30-minute timeout): it must cover P-9's table rewrites. Run the size queries first.
- **P-4/P-6, KI-31** (done): P-9 makes the converters `long`, and KI-31's `decimal?` workaround and both
  `HasColumnType("numeric(20,0)")` lines go.
- **P-8** ("Do"): HOT-2 and the round-trip counts assume it.
- **X-2** (done): D-16 replaces `GetGrimoireModeratorId`. LVL-5 batches the orphan removal (SET-11).
- **X-9** (done): LVL-7 keeps the binary search. SIN-3 and CC-5 reuse its pager.
- **X-12** ("Do"): D-11 and LVL-1 turn "report no-ops" into a set lookup. HOT-3's `Ancestors` feed the thread rule.
- **M-3/M-11** (done): SIN-1 adds the lookup half (`FindSinnerAsync`).
- **M-6/KI-55**: SIN-12 decides whether spam mutes follow `/Mute`'s order (Q33).
- **L-7** (done): LOG-2 generalises it and covers the edit log it missed.
- **C-3** (decided, no rank): CC-11 waits for it. **C-6** (done): F-1 adopts its rule everywhere. **C-9** (done): CC-7
  moves the policy onto the type.
- **I-18** ("Do"): do it together with SH-1 (example file, real file ignored).
- **I-6/I-7** (done): SH-15 keeps the re-upload on retry.
- **InvariantViolationException ruling** (rank 26): SET-12 found the one site it missed (defect 26).
- **`RequiredGuild` ruling**: F-5 assumes it.
- **Open KIs fixed as a side effect:** KI-74 (HOT-1), KI-20's cost (HOT-4), KI-64's neighbour race (SET-8), KI-80 and
  KI-81 (LVL-3), KI-83 (CC-4), KI-89 and KI-90 (LOG-8, SH-13), KI-99 for the leaderboard (LVL-2), KI-105 (F-1), KI-86
  partly (CC-1). KI-69's behaviour is kept deliberately by HOT-7's `Length > 0` arm.

## 5. Implementation plan

Line deltas are hand-written lines, net, from the section estimates with overlaps removed. Tests aren't counted.

### (a) Defects and quick wins: about −500 lines, no prerequisites

Do these first. They depend on nothing below and touch little shared code, so they can run in parallel with the owner's
remaining `review-decisions` work.

1. **Defects:** SH-1 plus the example-file move (rotate the password) · SH-2 · LOG-6, then LOG-7 · F-8 · MOD-11's
   handoff leak (the `CancelUnban` half) · LOG-12 · SH-11 · D-12's reader fix (option (a) on its own, or the one-row
   readers if (b) comes later) · SET-12's `InvariantViolationException` tweak · SH-15 once Q26 is answered.
2. **Quick wins:** F-10 · F-11 (check the build for CS8509 first) · F-12 · SET-7 · SET-10 (with its spike) · D-13 ·
   D-15 · D-17 · D-18 · D-22 · P-15 · P-21 · SH-3, SH-4, SH-6, SH-7, SH-9, SH-12, SH-14, SH-16 · HOT-7 · HOT-8 ·
   MOD-7, MOD-9, MOD-10, MOD-12, MOD-13, MOD-14 · SIN-5, SIN-7, SIN-8, SIN-10, SIN-13 · LOG-3, LOG-4, LOG-11 · LVL-7,
   LVL-10, LVL-12 (once Q28 is answered) · CC-5, CC-10, CC-12.

### (b) Foundations: about −665 lines, after Q1 to Q5

F-3, then F-4, then F-2 (one rename sweep across 58 files), then F-1, then F-5 with Warn as its first user, then F-6 (after
its spike), F-7, F-9 and LVL-4. F-1 decides the signatures SET-1 and SET-2 use, so phase (e)'s settings work comes after
it. **This phase unblocks every T-1 rewrite in (d).** Under S-12, D-13 and D-14, the factories return `T` or `Result<T>`,
so land S-12 before F-2 to avoid renaming `Validation` in code that is about to be deleted.

### (c) Migrations before the first production deploy: about −1,555 lines (−1,660 with D-12(b) and LVL-2), plus about −1,500 generated

No migration after `main`'s `20250705185227_SpamFilterOverrides` has run in production. That makes now the cheapest time
for all of these. Project memory says the redesign migrations are finalised and new schema work gets its own migration.
P-12, P-13, LVL-2 and an edited Settings `Initial` each go against that, so Q7 comes first.

Order, and how the steps interact:

1. **D-14** (code only, after S-12). D-10 then has less to delete.
2. **D-10 + D-11 + D-16 in one Settings change.** Settings' `Initial` copies the legacy tables with raw SQL behind
   `IF EXISTS`. Editing it to copy only the current state is cheaper than shipping it and then a conversion migration.
   Either way, the seven `_SetAt` indexes and the Settings `DropRedundantIndexes` migration disappear, which removes
   P-13's Settings half. If `Initial` is edited, Settings can also be created with `bigint` columns from the start, so
   **P-9 costs nothing in the Settings context**.
3. **D-20** (snapshot-only migration in the Grimoire context). After it, P-9 generates no `ALTER` for the legacy tables,
   which `Initial` still reads by raw SQL. Check that `INSERT … SELECT` from `numeric` into `bigint` columns works
   through the assignment cast.
4. **Grimoire-context schema:** P-12 (integer discriminators: edit `ModelRedesign`, or add a migration), P-13's Grimoire
   half, D-19 (drop columns), D-12(b) if chosen, and LVL-2 if chosen. If LVL-2 is taken, neutralise
   `20251025193658_LeaderboardView` rather than create and later drop the view. P-19 goes, and P-9 no longer needs its
   drop-and-recreate-the-view step.
5. **P-9 last in the Grimoire context.** `main`'s tables are `numeric` in production, so one rewrite of each is
   unavoidable. With P-12, `ModelRedesign` no longer rewrites `XpHistory` itself, so each table is rewritten once. It must
   fit inside P-3's 30-minute timeout. Run section 3's size queries first.
6. **The squash option.** Because none of the post-`main` migrations has run in production, they could be squashed into
   one migration per context. That expresses steps 2 to 5 once. The cost: every development and test database is rebuilt
   (P Q6), and the reviewed step structure is lost. This is part of Q7.

### (d) Feature rewrites: about −1,200 lines, after (b), in parallel by feature

Each feature moves to T-1 together with its own findings. They share only `RunAsync`, `Reply` and the helpers named in
this list, so the features can run as parallel tracks:

- **Moderation actions:** MOD-2, MOD-3, MOD-4, MOD-5, then MOD-1. MOD-6 step 2. MOD-8. The rest of MOD-11. About −220.
- **Sins and diagnostics:** SIN-1, SIN-2 (after Q10), SIN-3, SIN-4, SIN-6, SIN-9, SIN-12 (after MOD-1), SIN-14, SIN-15.
  About −415.
- **Logging:** SH-10 with LOG-2, then LOG-5. LOG-1 (after the `MemberBefore` spike). LOG-8 with SH-13. LOG-9. SH-5.
  About −160.
- **Leveling:** LVL-1, LVL-3, LVL-5, LVL-6, LVL-8, LVL-9, LVL-11. About −235.
- **Custom commands:** CC-2, CC-3 (after C-2), CC-4, CC-6, CC-7, CC-11. About −120.
- **Shared, optional:** F-13, SH-8, SH-17's net10 half. About −60.

### (e) Hot path and caching: about −210 lines, after (b) for the Settings parts

1. **SET-2, then SET-1, then SET-3** (the Settings internals, with F-1's signatures). SET-1's per-guild entry is what
   HOT-3 reads.
2. **HOT-1** and **HOT-2** (after its spike). These are independent of each other and of the Settings work, so they can
   start right after phase (a).
3. **CC-1, then CC-8 and CC-9.** CC-8 needs F-10 from phase (a).
4. **HOT-3, then HOT-4.** HOT-3 needs SET-1 and F-10. HOT-4 part 2, the handoff, needs nothing.
5. **P-11** (after Q17; it uses P-9's `long` parameters if P-9 has landed). **SET-8.** **P-16** only if the numbers call
   for it.

### (f) After net11.0: about −20 lines

1. Move the TFM and packages (EF Core 11, Npgsql 11, `Microsoft.Extensions.*` 11), check DSharpPlus nightly on
   `net11.0`, and fix the compiler's breaking changes (`with(`, `union` and `closed` used as identifiers, `nameof(this.P)`
   in attributes). F-11 must already be in.
2. Run the EF `closed` spike from section 3, starting with `XpHistoryEntry` and `ChannelLock`.
3. Apply the cs15 findings in this order: T-3 (`Result` and `Error` become `closed`, and T-1's `ColorOf` loses its `_`
   arm), then D-23 (per hierarchy, as the spike allows; it includes `ModerationActorConverter`, `CustomCommand` and the
   Settings arms), then LOG-1's `UserChange`, SIN-6's last step and SH-17's `[with(...)]`.

### Dependencies

```
(a) ──────────────────────────────────────────────┐
(b) F-3 → F-4 → F-2 → F-1 → F-5 → F-6/F-7/F-9      │  (S-12 before F-2)
       │                    │                      │
       │                    └→ (d) features, in parallel
       └→ (e) SET-2 → SET-1 → SET-3 → HOT-3 → HOT-4
          HOT-1, HOT-2, CC-1 → CC-8/CC-9  (can start after (a))
(c) S-12 → D-14 → D-10/D-11/D-16 → D-20 → Grimoire schema (P-12, P-13, D-19, D-12b, LVL-2) → P-9
    runs alongside (b) and must finish before the first production deploy
(f) after .NET 11 ships and (b)'s T-3 net10 shape is in
```

### Line totals

| Phase | Net lines |
|---|---|
| (a) Defects and quick wins | about −500 |
| (b) Foundations | about −665 |
| (c) Pre-deploy migrations | about −1,555 hand-written (−850 of it D-20); about −1,500 generated (P-13). New migrations add generated code |
| (d) Feature rewrites | about −1,200 |
| (e) Hot path and caching | about −210 |
| (f) After net11.0 | about −20 |
| **Total** | **about −4,150 hand-written (about −3,300 without D-20)** |

Per section, after merges: F about −675, D about −1,500, P about −150, SET about −270, HOT about +75, SH about −190,
MOD about −300, SIN about −450, LOG about −225, LVL about −290, CC about −195.

## 6. Spikes and measurements

| Spike | How | What it decides |
|---|---|---|
| **Production size and `EXPLAIN (ANALYZE, BUFFERS)`** | Section 3's three queries on production. These are `main` tables, which exist there. Add the leaderboard aggregate filtered to the largest guild. MOD-6's three expiry queries target Settings tables that don't exist in production yet, so run them on a copy of production migrated forward, or skip them if D-10 is taken | P-9's deploy cost and timing, P-12, P-16 (compaction or not), LVL-2 (live query or view), MOD-6 step 3. It also covers the branch review's own pre-deploy queries |
| **The `COMMIT` round trip** | Npgsql command logging in an integration test around `AddMessageEvent`'s save. Check that one batch goes out with no separate `COMMIT`, and that a replay leaves no orphan `MessageCreatedEntry` | HOT-2, and the section 3/section 5 round-trip count |
| **DSharpPlus `MemberBefore`** | On 5.0.0-nightly-02614, check what `GuildMemberUpdatedEventArgs.*Before` holds for an uncached member. Against real Discord, also check `EditedTimestamp`/`MessageBefore` on an embed-only `MESSAGE_UPDATE` | LOG-1's skip (P-10/SET-6) and HOT-7. If "before" is built from the new payload, skip only when the member was cached |
| **HybridCache fail-safe adapter** | Confirm that FusionCache 2.8's `HybridCache` adapter applies `DefaultEntryOptions` (fail-safe, `FactorySoftTimeout`) when a call passes no `HybridCacheEntryOptions` | SET-10's shape: keep `HybridCache`, or inject `IFusionCache`. This in turn decides how cheap F-1 is (Q1) |
| **F-6 converter** | Check that `ArgumentFailedConversionResult.Error` carries the converter's exception through to `CommandHandler`, and that `[MinMaxLength]` still reaches Discord on a converted string option | F-6, MOD-4, CC-4 and CC-3's `name`. If it fails, keep `Create` in `RunAsync`'s input (T-1 works either way) |
| **EF `closed` hierarchies** (net11.0) | Section 3's five steps per hierarchy: build, `has-pending-model-changes`, integration tests, compiled query/`ExecuteUpdate`/`dbcontext optimize`, copy constructors | D-23 per hierarchy, CC-13's part, and T-3's final shape for persisted hierarchies |
| Smaller checks | LVL-11: does a choice provider on an enum parameter replace `EnumOptionChoiceProvider`? LOG-6: can DSharpPlus delete a message by id without the GET? SIN-2: does EF translate a positional record in a nested projection? CC-6: do shadow keys keep the converters? D-15: does EF materialise through private constructors? SH-2: what is PluralKit's published rate limit? | Each decides its own finding's form, not whether it's done |

## 7. Questions for the owner

Deduplicated across all eleven sections. Each question lists the findings it unblocks, and the IDs in brackets are the
section questions it replaces. The groups run from most blocking to least.

### A. The foundations: these block phases (b), (d) and (e)

1. **Outages as exceptions or `Result`?** Adopt F-1 together with SET-10's fail-safe, which reverses S-4 (done), or keep
   S-4? *Unblocks F-1, the signatures of SET-1 and SET-2, LVL-4, MOD-10, SIN-15, HOT-3's `ValueTask`, and the extent of
   F-9; closes KI-105.* [F Q1, SET Q1]
2. **The shape of `Result` and `Error`.** Merge `Validation<T>` into `Result<T>` (F-2, reversing the strategy doc)? Drop
   `Error.Code`, and change about 60 test assertions to check the case and the message (F-4)? Use two colours (Invalid
   red, NotFound yellow) or one? *Unblocks F-2, F-3, F-4 and therefore F-5 and every T-1 rewrite.* [F Q2, Q3, Q4]
3. **Refusals under T-1.** Should every refusal become ephemeral and pre-defer? That covers the public moderation
   refusals, "is not locked", "not banned", the sin-not-found message (one colour, not three), and `/Rewards`. Is the Warn
   DM before the reply acceptable? *Unblocks F-5, MOD-1, SIN-1, LVL-3, CC-2.* [F Q5, MOD Q1]
4. **F-6 fallback.** If the converter spike fails, accept DSharpPlus's generic parse message, or keep `Create` in each
   command's input? *Decides F-6, MOD-4, CC-4.* [F Q6]

### B. The schema before the first deploy: these block phase (c)

5. **Settings tables as state (D-10).** Drop append-only Settings (and `migration-strategy.md`'s rule), with "off" as a
   delete and reset-to-default as a delete (S-9)? *Unblocks D-10, D-11, D-16; makes S-5, S-13 and MOD-6 steps 1 and 3
   moot; closes KI-103.* [D Q1, MOD Q3]
6. **Editing migrations that haven't been deployed.** May `ModelRedesign`, `SyncIndexesWithModel`, `LeaderboardView` and
   Settings `Initial` be edited, against project memory's "new schema work gets its own migration"? Or should the
   undeployed migrations be squashed into one per context? Do development databases that already ran them exist?
   *Decides the form of P-12, P-13, D-10, LVL-2, and whether P-9 is free in Settings.* [P Q6]
7. **`bigint` ids (P-9).** Do it with the first deploy, later, or skip it after the size query? *Unblocks P-9, and P-11's
   parameter types.* [P Q1]
8. **Pardon and reason shape.** Choose (a): keep the histories, refuse a second `/Pardon`, and read one row. Or choose (b):
   `Sin.Reason` plus a 1:1 `Pardon`. Which date and which moderator should a pardon show? *Unblocks D-12, SIN-2 and
   defect 7.* [D Q2, SIN Q1, P Q4]
9. **Integer discriminators (P-12):** readable strings in pgAdmin, or `main`'s integers with no deploy rewrite? [P Q2]
10. **Obsolete types out of the model now (D-20),** with the tables kept until P-7's release? [D Q4]
11. **`Reaction.GuildId`:** keep it for the planned reaction features, or drop it with the other two copies (D-19)?
    [D Q5, P Q3, LOG Q6]
12. **Leaderboard (LVL-2):** trade the view and its 15-minute lag (called deliberate) for a live per-guild query, if the
    largest guild comes in at tens of milliseconds? Should members with 0 XP stay on the board? [LVL Q1]
13. **`GuildSetting` key-value strings:** keep them? This is information only, not a finding. [D Q3]

### C. Hot path and caching: these block phase (e)

14. **One `MessageCreated` pipeline (HOT-3)** instead of five handlers, with errors reported by step name? Should system
    messages (joins, pins) leave the spam filter? *Unblocks HOT-3 and HOT-4 part 1.* [HOT Q1, Q3]
15. **Messages in a locked thread (HOT-4):** keep the log row with the bot as deleter, and skip XP and custom commands?
    [HOT Q2]
16. **Raw SQL on the reaction path (P-11):** one `INSERT … ON CONFLICT`, or the EF-only fallback? [P Q5, HOT Q4]
17. **Trust DSharpPlus's "before" values (LOG-1)** when the member was cached? Should case-only nickname changes and edits
    be logged (ordinal compare)? Should history always be recorded, with only the logs gated? [SET Q3, LOG Q1, Q2]
18. **The in-memory XP cooldown (HOT-1)** relies on a single bot instance. Is that OK? Should the running total be cached
    too? [SET Q2, HOT Q6]
19. **A 1-hour settings expiry (SET-10):** acceptable? [SET Q4]
20. **The spam-tracker store (SET-8):** `IMemoryCache` with a sliding expiry, or a `ConcurrentDictionary` with immutable
    state and an idle prune? [SIN Q4]
21. **Custom command autocomplete (CC-1):** in-memory Levenshtein, or prefix-then-contains (which lets `fuzzystrmatch`
    go)? SET-5 is moot either way. [CC Q1, SET Q5]
22. **Write-behind batching (HOT-10):** recommended no. Confirm? [HOT Q5]

### D. Configuration and infrastructure

23. **Production configuration (SH-1):** does production use a deployed `appsettings.json` or environment variables? Is
    moving the tracked file to `appsettings.example.json` OK? Is the password used anywhere but a local dev database?
    [SH Q1, Q2]
24. **PluralKit (SH-2):** may a delete handler wait up to about 10 s in a burst? [SH Q3]
25. **Retries (SH-15):** a rare duplicate post or a rare lost one? [SH Q4]
26. **The log message shape (SH-10):** one record, or three `closed` cases? Should LOG-2's gate live in `GuildLog` or in
    a `LogGate`? [SH Q5, LOG Q7]
27. **Primary-constructor parameters (LVL-12):** use them directly or copy them to fields? One repo-wide rule, plus an
    editorconfig change. [LVL Q5]
28. **Principle 8:** amend it to say EF queries buffer while retry is on (P-15)?
29. **`GuildLogPublisher`'s single reader:** track partitioning by channel for later? [SH Q6]

### E. Feature behaviour (each unblocks one finding)

30. **MOD-4:** `/Lock` treats a blank reason as no reason, like `/Kick` and `/Mute`. [MOD Q2]
31. **MOD-5:** lock and mute factories take `setAt + ModerationDuration` instead of an end time. [MOD Q4]
32. **MOD-2:** drop `ModerationRank`'s self-exemption (every caller refuses self first). [MOD Q5]
33. **SIN-4:** `/Publish` with no ban log names `/ModSettings PublicBanLog` (changes SIN-74). [SIN Q2]
34. **SIN-5:** one sin-count rule for `/SinLog Mod` and `/UserInfo`, and which one? [SIN Q3]
35. **SIN-12:** spam mutes through `/Mute`'s act step, with KI-55's order? [SIN Q5]
36. **SIN-3:** text `/SinLog` for non-moderators shows their own log, or requires Manage Messages? [SIN Q6]
37. **LOG-3/4:** has a PluralKit private system ever left out `system`/`member`? If so, should the ids become optional
    (with a migration) rather than `OrPrivate` being deleted? [LOG Q3]
38. **LOG-6:** forget a post Discord says is gone straight away (changes two tests)? [LOG Q4]
39. **Avatar thumbnails from history** instead of a lookup (after Q17)? [LOG Q5]
40. **LVL-8:** drop `option` from `/Reclaim` and `/Leaderboard`? [LVL Q2]
41. **Reward self-healing** on every gain: is it intended? Should the minimum reward level be 1? [LVL Q3, Q4]
42. **CC-3:** an `access` choice instead of `restricted_use`? [CC Q2]
43. **CC-8:** keep `%Message` spacing verbatim? [CC Q3]
44. **CC-9:** `/Command` failure as an ephemeral "not found", or a silent delete? [CC Q4]
45. **`CustomCommandUsages` retention:** keep it forever, or trim it in `LogCleanup`? [CC Q5]
