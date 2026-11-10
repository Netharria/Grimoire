# Section 3: Persistence and queries

Reviewed 2026-11-09, against the code as it will be once every fix in `review-decisions.md` is in, with the uncommitted
`DropRedundantIndexes` migrations and `SchemaIndexTests` counted as done. P-8 (pooled factories) is assumed done.

The biggest win is storing Discord ids as `bigint` instead of `numeric(20,0)` (P-9). Every id column and every index
in both databases uses `numeric`. Measured on PostgreSQL 18 with 2 M rows, the indexes were about three times larger and
grouping was about four times slower. Next come query shapes on frequent events. A member update runs three SELECTs
even when only a role changed (P-10). A reaction costs up to three round trips where one upsert would do (P-11). Third,
there is deploy-time work the undeployed migrations don't need to do: string discriminators that rewrite `XpHistory` and
update every `MessageHistory` row (P-12), and indexes that are built only to be dropped (P-13). Findings P-9 to P-20
follow, then the EF `closed` spike plan.

**Numbering.** `branch-review.md` already uses P-1 to P-8 for persistence, and section 1 cites them ("P-1's deny arm").
To avoid a clash, this section starts at P-9. Where a finding names P-1 to P-8, it means the branch-review finding.

## Database calls per event

This counts round trips to `GrimoireDbContext` in the common case: a regular member in a logged channel, every module
on, and settings served from the cache. Settings misses belong to section 4. Discord and PluralKit calls belong to
section 5.

| Event | How often | Round trips | Notes |
|---|---|---|---|
| `MessageCreated` → `AddMessageEvent` | every message | 1 | `Message`, its `MessageCreatedEntry` and its attachments go in one batched `SaveChanges`. Good. |
| `MessageCreated` → `GainUserXp`, in cooldown | most messages | 1 | Compiled `Max(TimeOut)` on the primary key |
| `MessageCreated` → `GainUserXp`, earning | once per member per timeout | 3 | `Max`, `Sum(Xp)` over the member's whole history (P-16), `INSERT` |
| `MessageCreated` → `TextCustomCommandProcessor` | `!` messages only | 2 | Lookup (P-17), usage `INSERT`. Non-commands cost 0 |
| `MessageCreated` → `SpamEvents`, `LockedThreadEvents` | every message | 0 | Except on a spam verdict (count plus insert) |
| `MessageUpdated` | every edit | 2 | Projection plus `INSERT` |
| `MessageDeleted` | every delete | 2 | Projection plus `INSERT` (or the proxy-link `INSERT`) |
| `MessageReactionAdded` | every reaction | **1 to 3** | `ExecuteUpdate`, `Any(Message)`, `INSERT` (P-11) |
| `MessageReactionRemoved*` | every removal | 1 | `ExecuteUpdate`. Good |
| `GuildMemberUpdated` | every role, nickname, avatar, timeout or boost change | **3**, plus up to 3 inserts | One context and one SELECT in each of three handlers, even for a role change (P-10) |
| `GuildMemberAdded` | every join | 3 to 4 | Three latest-value SELECTs plus one save |
| `CleanupLogsBackgroundTask` | every minute | 2 to 5 | Daily trim adds 3 |
| `LeaderboardRefreshBackgroundTask` | every 15 minutes | 1 | `REFRESH MATERIALIZED VIEW CONCURRENTLY` aggregates all of `XpHistory` (P-9, P-16) |

On a typical message the bot makes 2 round trips. Nothing on the message path is redundant except what P-16 notes.
The avoidable round trips are on reactions and member updates.

## Findings

#### P-9 Store snowflake ids as `bigint`, not `numeric(20,0)`  `perf` `remove` · **high**
- **Where:** `Grimoire.Data/Conventions/StronglyTypedIdConventions.cs:21-49` (`ConfigureStronglyTypedIds` and the eight
  `ValueConverter<XId, ulong>`), which is linked into `Grimoire.Settings` too. Also `ModerationActorConverter.cs:18-33`,
  plus `SinConfiguration.cs:37` and `SinReasonHistoryConfiguration.cs:35` (`HasColumnType("numeric(20,0)")`). Every id
  column in both model snapshots is `numeric(20,0)`.
- **Now:** Npgsql maps `ulong` to `numeric(20,0)`. Every primary key, foreign key and index in both databases compares
  variable-length `numeric` values, including `Messages`, `MessageHistory`, `XpHistory`, `Reactions` and the
  leaderboard view's `GROUP BY "GuildId", "UserId"`. I measured this on `postgres:18-alpine`. The table had 2 M rows of
  three snowflake columns plus a timestamp, with a primary key and a `(usr, guild, ts)` index. On average a value took
  13.0 bytes against 8. The heap was 145 MB against 115 MB, and the indexes were **172 MB against 57 MB**.
  `count(DISTINCT guild)` took 815 ms against 190 ms. A point lookup read 6 buffers against 4, and both took well under
  1 ms. So single lookups barely change. Size, cache residency, sorts and aggregates do.
- **Proposed:** convert to `long` in the shared convention. Snowflakes stay below 2^63 until about 2084, so the cast
  keeps both order and equality.
  ```csharp
  internal sealed class GuildIdConverter()
      : ValueConverter<GuildId, long>(id => (long)id.Value, value => new GuildId((ulong)value));
  // ModerationActorConverter becomes ValueConverter<ModerationActor, long?>. The decimal workaround and its comment
  // go, along with both HasColumnType("numeric(20,0)") lines. AlertOccurrence.GuildIds already stores long[].
  ```
  One migration per context runs `ALTER COLUMN ... TYPE bigint USING "X"::bigint` on every id column. Postgres
  refuses to change a column that a view reads, so the migration drops `leaderboard_view` first and recreates it with
  its unique index afterwards.
- **Size:** about ±0 lines of C#, with about −8 lines in the converters. Two generated migrations, each long, plus about
  15 hand-written lines for the view. Every table is rewritten once at deploy.
- **Needs:** a migration in both contexts, and an owner decision on the deploy cost (see Questions). Run the size
  queries at the end first.
- **Interacts with:** P-3 (the 30-minute timeout must cover these rewrites), P-6, KI-31 (whose `decimal?` workaround
  this removes), and the materialized-view gotcha in project memory (the tests' `REFRESH` still applies). Do it before
  P-12, so each table is rewritten only once.

#### P-10 `GuildMemberUpdated` queries all three histories on every member update  `perf` · **med**
- **Where:** `UpdatedUsernameEvent.HandleEventAsync` (`UpdatedUsernameEvent.cs:28-35`),
  `UpdatedNicknameEvent.HandleEventAsync` (`UpdatedNicknameEvent.cs:27-33`) and `UpdatedAvatarEvent.HandleEventAsync`
  (`UpdatedAvatarEvent.cs:31-37`).
- **Now:** each handler opens a context and reads the latest stored value whenever User Log is on. A role grant,
  timeout, boost or pending-flag change therefore costs three contexts and three SELECTs, and none of them can find a
  change. Grimoire's own reward grants in `GainUserXp.ApplyRewards` trigger this too.
- **Proposed:** before touching the database, skip when the event itself shows no change in the handler's field:
  `UsernameBefore == UsernameAfter`, `NicknameBefore == NicknameAfter`, `AvatarHashBefore == AvatarHashAfter`. The
  stored row stays the source of truth whenever the field did change. Startup sync (`AddMissing*HistoryAsync`) still
  repairs changes the bot missed while offline.
  ```csharp
  if (args.MemberBefore is { } before && before.Nickname == args.NicknameAfter) // cached and unchanged
      return;
  ```
- **Size:** about +3 lines in each of 3 files.
- **Needs:** a check of what DSharpPlus 5.0.0-nightly-02614 puts in `MemberBefore`/`*Before` when the member wasn't
  cached. If it builds `MemberBefore` from the new payload, "unchanged" can't be told from "unknown", and the skip must
  apply only when the member was in the cache. Section 5 or 9 can merge the three handlers into one, which would also
  share a single context. That is feature structure, so it isn't proposed here.
- **Interacts with:** KI-40, KI-49, KI-68 (whose per-guild username rows this doesn't change).

#### P-11 A reaction is one upsert, not up to three round trips  `perf` · **med**
- **Where:** `ReactionEvents.SaveReactionAsync` and `ReviveAsync` (`ReactionEvents.cs:103-130`).
- **Now:** for every reaction on a logged guild, the handler runs `ExecuteUpdate` (revive), then `Any(Message)`, then
  `INSERT`, and catches the unique and foreign-key races. A new reaction on a logged message costs 3 round trips. A
  reaction on a message that isn't stored costs 2.
- **Proposed:** one statement covers revive, insert, the stored-message check and the duplicate race:
  ```csharp
  dbContext.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO "Reactions" ("MessageId", "UserId", "Emoji", "GuildId", "ReactedAt", "RemovedAt")
      SELECT {messageId}, {userId}, {emoji}, {guildId}, {reactedAt}, NULL
      WHERE EXISTS (SELECT 1 FROM "Messages" WHERE "Id" = {messageId})
      ON CONFLICT ("MessageId", "UserId", "Emoji")
      DO UPDATE SET "ReactedAt" = EXCLUDED."ReactedAt", "RemovedAt" = NULL
      """);
  ```
  The parameters are the raw column values (after P-9, `long`), with the emoji as `ToString()`. The
  `UniqueConstraintException` arm goes. The FK arm stays for a message purged between `EXISTS` and the insert.
- **Size:** about −12 / +10 lines, 1 file. This is the first hand-written SQL on a hot path, and a column rename would
  only show up as a test failure. The reaction tests cover all three cases.
- **Needs:** nothing (P-9 changes the parameter types).
- **Interacts with:** none.

#### P-12 Keep integer discriminators on `XpHistory` and `MessageHistory`  `perf` `remove` · **med**
- **Where:** `XpHistoryConfiguration.cs:20` (`HasDiscriminator<string>("Type")`), `MessageHistoryConfiguration.cs:20`
  (`HasDiscriminator<string>("Discriminator")`), and `ModelRedesign` phase 5 (`20260524033203_ModelRedesign.cs:245-286`)
  and phase 7 (`:369-381`).
- **Now:** `main` stored `XpHistory.Type` and `MessageHistory.Action` as integers. `ModelRedesign` converts `XpHistory`
  with `ALTER COLUMN "Type" TYPE character varying(21)`, which rewrites the whole table and its indexes on deploy. It
  also adds a `MessageHistory.Discriminator` string and backfills it with an `UPDATE` of every row. After that, every
  new XP gain and history row stores `'Earned'`, `'Created'` or `'DeletedByModerator'` (7 to 19 bytes) where 4 bytes
  did before. These are the two tables that get the most writes.
- **Proposed:** map the discriminators to the integers `main` already stores:
  `HasDiscriminator<int>("Type").HasValue<EarnedXp>(0).HasValue<AwardedXp>(1).HasValue<ReclaimedXp>(2).HasValue<MigratedXp>(3)`.
  `ModelRedesign` then drops the `ALTER COLUMN`. For `MessageHistory`, keep `Action` as the discriminator column
  (0 Created, 1 Edited, 2 Deleted) and add `3` for `DeletedByModerator`. The backfill then becomes
  `UPDATE ... SET "Action" = 3 WHERE "Action" = 2 AND "DeletedByModeratorId" IS NOT NULL`, which touches only moderator
  deletes. EF still writes `IN (0, 1)` for `OfType<MessageHistoryContentEntry>()`.
- **Size:** about ±0 lines in the configurations. About −25 lines in `ModelRedesign` and its `Down`. The snapshot is
  regenerated.
- **Needs:** an edit to an undeployed migration (project memory allows small fixes before prod), and an owner decision
  against the readability of string values in SQL (see Questions).
- **Interacts with:** P-3 (less deploy work under the timeout), P-9 (if both are taken, rewrite `XpHistory` once).

#### P-13 Don't create the indexes that `DropRedundantIndexes` drops  `remove` `perf` · **low**
- **Where:** `ModelRedesign` (`20260524033203_ModelRedesign.cs:52`, `:83`, `:233`, `:386`) creates
  `IX_SinReasonHistory_SinId_SetAt`, `IX_Pardons_SinId_SetAt`, `IX_CustomCommandUsages_GuildId_Name_UserId` and
  `IX_XpHistory_GuildId_Xp`. `SyncIndexesWithModel` (`20261025042846_SyncIndexesWithModel.cs:174`) creates
  `IX_Sin_Id_GuildId`. `20261109022034_DropRedundantIndexes` drops all five. The Settings `Initial` and
  `20261109022050_DropRedundantIndexes` do the same with the seven `_SetAt` indexes.
- **Now:** no migration after `main`'s `20250705185227_SpamFilterOverrides` has run in production. So the first deploy
  builds an index over all of `XpHistory`, and then drops it a few migrations later.
- **Proposed:** remove the `CreateIndex` calls (and the matching `Down` drops) from `ModelRedesign`,
  `SyncIndexesWithModel` and Settings `Initial`. Delete both `DropRedundantIndexes` migrations, and keep the current
  snapshots. `SchemaIndexTests` still passes, because the indexes never exist.
- **Size:** about −110 lines of migration code and two generated designers (about −1,500 lines).
- **Needs:** every developer and test database that already ran these migrations would keep the five indexes. Either
  rebuild those databases, or keep the drops as `DROP INDEX IF EXISTS` in a raw-SQL step.
- **Interacts with:** P-5, S-8 (the decided drops stay done, just earlier).

#### P-14 Drop the `GuildId` copies that nothing reads  `remove` `state` · **low**
- **Where:** `MessageHistoryEntry.GuildId` (`Grimoire.Domain/MessageHistory.cs:16`), `Pardon.GuildId`
  (`Grimoire.Domain/Pardon.cs:16`), and their writers in `AddMessageEvent`, `UpdateMessageEvent`, `DeleteMessageEvent`,
  `BulkMessageDeletedEvent` and `PardonSin`. `Reaction.GuildId` (`Reaction.cs:23`) is the same case (see Questions).
- **Now:** on `main`, these columns existed to carry a foreign key to `Guilds`. `DropOrphanedLegacyForeignKeys`
  removed those keys. No query in `Grimoire` filters or projects them, because every read goes through the parent
  `Message` or `Sin`. They cost a numeric column on every history row (1 or 2 per message), and a history row can now
  carry a different guild from its message.
- **Proposed:** remove the properties and the columns. The parent's `GuildId` is the only copy.
- **Size:** about −12 lines in 7 files, plus a small migration (`DROP COLUMN` is metadata-only in Postgres).
- **Needs:** a migration.
- **Interacts with:** section 2 (domain model). P-7 doesn't depend on it, because no copy into the settings database
  reads these columns.

#### P-15 `AsAsyncEnumerable` doesn't stream here; buffer plainly  `perf` `remove` · **low**
- **Where:** `GrimoireServiceRegistration.cs:102-105` and `SettingsServiceRegistration.cs:23-26`
  (`EnableRetryOnFailure()`). The detours are in `MemberDatabaseQueryHelpers.AddMissingNickNameHistoryAsync`/
  `AddMissingAvatarsHistoryAsync` (`MemberDatabaseQueryHelpers.cs:28`, `:67`),
  `UserDatabaseQueryHelpers.AddMissingUsernameHistoryAsync` (`UserDatabaseQueryHelpers.cs:27`),
  `CleanupLogsBackgroundTask.RunTask` (`CleanupLogsBackgroundTask.cs:41`) and
  `SettingsModule.Moderation.Mutes.cs:141`.
- **Now:** with a retrying execution strategy, EF Core buffers every query result before handing back the first row,
  so a retry can't return a partial set. `QueryCompilationContextDependencies.IsRetryingExecutionStrategy` feeds
  `QueryCompilationContext.IsBuffering` (EF 10.0.12 API docs, and the "Buffering and streaming" page). Every
  `AsAsyncEnumerable` in the code therefore reads the full result into memory first. The three history helpers use it
  only to map to a tuple before `ToHashSetAsync`.
- **Proposed:** project in the query and call `ToListAsync`/`ToHashSetAsync` directly, then do the tuple mapping in
  memory. Treat principle 8's "`IAsyncEnumerable` for streaming" as not available for EF queries while retry is on. If a
  real streaming need comes up, such as a bulk export, run that one query under
  `dbContext.Database.CreateExecutionStrategy()` with a non-retrying strategy. The cleanup pipeline gains from the
  buffering, because no reader stays open across its Discord calls, so it only needs a comment.
- **Size:** about −6 lines, 3 files.
- **Needs:** nothing.
- **Interacts with:** S-11 ("stream rows as they arrive" comments over `ToListAsync`), KI-97.

#### P-16 XP totals are recomputed from an ever-growing ledger  `perf` · **low** (until measured)
- **Where:** `GainUserXp.HandleEventAsync` (`GainUserXp.cs:64-66`, `SumAsync` on each gain), `GetLevel`,
  `UserInfoCommands.GetAndAddLevelInfo` (`UserInfoCommands.cs:137`), and the `leaderboard_view` refresh every
  15 minutes (`LeaderboardRefreshBackgroundTask.cs:23`).
- **Now:** every gain adds a row, and nothing ever folds rows together. A member's total is a `Sum` over every gain
  they ever made. It is served index-only by `IX_XpHistory_UserId_GuildId_Xp`, so it is cheap per row but linear in
  history. The view refresh aggregates the whole table four times an hour. `main` worked the same way, so this isn't a
  regression.
- **Proposed:** measure first with the production queries below. If the heaviest members have tens of thousands of
  rows, or the refresh takes seconds, add a monthly compaction. It would fold each member's `Earned` rows older than N
  days into one `MigratedXp` row, which the hierarchy already has. All readers only sum, so nothing reads individual
  old rows. Otherwise leave it.
- **Size:** about +25 lines, 1 background task, if taken.
- **Needs:** production numbers.
- **Interacts with:** P-9 (makes the refresh's grouping cheaper), X-9.

#### P-17 Custom command lookups: walk the key, and project only what's shown  `perf` · **low**
- **Where:** `CustomCommandDatabaseQueryHelpers.GetCustomCommandQuery` (`CustomCommandDatabaseQueryHelpers.cs:18-24`),
  used on every `!` command and `/Command`. `GetCustomCommandOptions.GetChoicesAsync` (`GetCustomCommandOptions.cs:33-47`)
  runs on every autocomplete keystroke.
- **Now:** the single lookup filters with `LatestVersions()`, a `NOT EXISTS` anti-join over the command's other
  versions. The autocomplete loads `Content`, up to 2,000 characters for each of 5 rows, only to test it for
  `%mention` and `%message`.
- **Proposed:** for one command, use `.Where(GuildId, Name).OrderByDescending(c => c.CreatedAt).Take(1)`, which is a
  backward scan of the primary key `(Name, GuildId, CreatedAt)` with `LIMIT 1`. Keep `LatestVersions` for the lists.
  In the autocomplete, project
  `HasMention = EF.Property<string>(x, "Content").ToLower().Contains("%mention")` and the same for `%message`.
- **Size:** about ±0 lines, 2 files.
- **Needs:** nothing.
- **Interacts with:** the branch-review section 8 bullet on the doubled `AsNoTracking` and latest-version filter, KI-87.
  The cache question for custom commands is section 4's.

#### P-18 One lateral read for a sin's pardon  `perf` · **low**
- **Where:** `SinLog` projection (`SinLog.cs:115-125`) and `PublishCommands.Unban` (`PublishCommands.Unban.cs:35-47`).
- **Now:** `SinLog` runs three correlated subqueries per sin against `Pardons`: `Any`, the newest pardon's moderator
  and the oldest pardon's date. `Unban` nests one subquery inside another. The moderator shown comes from the newest
  pardon, but the date comes from the oldest.
- **Proposed:** one subquery, `Pardon = dbContext.Pardons.Where(p => p.SinId == x.Id).OrderByDescending(p => p.SetAt)
  .Select(p => new { p.ModeratorId, p.SetAt, p.Reason }).FirstOrDefault()`, with `Pardon != null` standing in for
  `Any`. Whether the date should be the first pardon's or the latest's is a question below.
- **Size:** about −8 lines, 2 files.
- **Needs:** the owner's answer on pardon dates.
- **Interacts with:** none.

#### P-19 `leaderboard_view` has no index for the rank range  `perf` · **low**
- **Where:** `GetLeaderboard.GetLeaderboardAsync` (`GetLeaderboard.cs:74-98`) and `20251025193658_LeaderboardView.cs:23`.
- **Now:** the view's only index is the unique `(GuildId, UserId)` index that `CONCURRENTLY` needs. The page query
  filters `GuildId` with a `Rank` range, so Postgres reads every row of the guild to find 15. Each `/Leaderboard` also
  makes three round trips: count, the member's rank, and the rows.
- **Proposed:** add `CREATE INDEX ON leaderboard_view ("GuildId", "Rank")` in raw SQL. Fold the count into the rank
  lookup with one `GroupBy(_ => 1)`, or keep three queries. That is a command-rate cost.
- **Size:** about +5 lines of migration.
- **Needs:** a migration. The refresh rebuilds both indexes, which is a small cost every 15 minutes.
- **Interacts with:** X-8, P-9 (recreates the view anyway, so add the index there).

#### P-20 `ModerationActorConverter.ToColumn` without the `_` arm  `cs15` `state` · **low**
- **Where:** `Grimoire.Data/Conventions/ModerationActorConverter.cs:22-27`.
- **Now:** `actor switch { ModerationActor.Moderator m => m.Id.Value, _ => null }`. A third actor kind would be stored
  as `NULL` and read back as `System`, with no warning.
- **Proposed:** once section 2 makes `ModerationActor` a `closed record`, write
  `ModerationActor.Moderator m => m.Id.Value, ModerationActor.System => null`, with no `_`, so CS8509 (an error after
  F-11) flags a new case. EF never constructs `ModerationActor`, because the converter does. So this part doesn't
  depend on the spike below.
- **Size:** ±0 lines.
- **Needs:** `net11.0`, F-11, and section 2's `closed` decision for `ModerationActor`.
- **Interacts with:** P-6, P-9 (the type becomes `long?`).

#### P-21 `AddAsync` where `Add` is meant  `remove` · **optional**
- **Where:** 21 `AddAsync`/`AddRangeAsync` calls in `Grimoire` and `Grimoire.Settings`, for example
  `AddMessageEvent.cs:74` and `GainUserXp.cs:85`.
- **Now:** the async form exists only for value generators that reach the database, such as HiLo. None are configured,
  so these complete synchronously behind an extra state machine.
- **Proposed:** use `Add` and `AddRange`. Several files already do (`Warn`, `PardonSin`, `ReactionEvents`).
- **Size:** ±0 lines, about 15 files.
- **Needs:** nothing.
- **Interacts with:** none.

## Spike: EF Core and Npgsql with `closed` hierarchies (run once the .NET 11 SDK is installed)

I haven't guessed the outcome. This is what to test, and why each item could break.

**Which hierarchies it touches.** These are the EF-mapped TPH roots: `CustomCommand`, `CustomCommandRole`,
`MessageHistoryEntry` (plus the intermediate `MessageHistoryContentEntry`) and `XpHistoryEntry` in `Grimoire.Domain`,
and `ChannelLock`, `ThreadLock`, `Mute`, `Reward`, `XpTrackedItem` (plus `XpIgnoredItem`/`XpWatchedItem`),
`DiagnosticsIgnoredItem` and `GuildSetting` in `Grimoire.Settings`. `ModerationActor` and `ReactionEmoji` are stored
through value converters, so EF never constructs or reflects on them. They don't need the spike (P-20).

**What `closed` changes that EF might see.** The base becomes implicitly `abstract` (all the roots above already are).
It gets `[IsClosedType]`, and every base constructor gets `[CompilerFeatureRequired("ClosedClasses")]`. EF only calls
constructors on concrete leaves. Records generate a protected copy constructor on the base, and each leaf's
constructor chains to the base constructor.

**Steps.** For one hierarchy per context, start with `XpHistoryEntry` and `ChannelLock`:
1. Mark the root `closed`, and the intermediate levels `closed` where there are any. Build.
2. `dotnet ef migrations has-pending-model-changes` for both contexts. Expect no change. A change means the attributes
   altered model discovery.
3. Run the integration tests for that table: insert, `OfType<>`, materialize each leaf, and project through a
   correlated subquery.
4. Check `EF.CompileAsyncQuery` (`GainUserXp`), `ExecuteUpdate`/`ExecuteDelete` on the root set, and `dotnet ef
   dbcontext optimize` (compiled model). Compiled models generate code that touches constructors and may be the place
   where `CompilerFeatureRequired` matters.
5. Check change-tracking snapshots and `with` copies (`AlertOccurrenceStore.UpsertAsync`'s
   `CurrentValues.SetValues(existing with { ... })`), which go through the base copy constructor.
If all five pass, apply `closed` to the rest. If one fails, record which step failed and the EF/Npgsql version, and keep
`abstract` plus a private constructor for that hierarchy (T-3's net10 shape).

## Checked, no finding

- **Indexes against query shapes.** `Messages`: the primary key serves every lookup, and `CreatedTimestamp` serves the
  per-minute purge. The cascade to `MessageHistory`, `Attachments`, `Reactions` and `ProxiedMessages` is indexed on
  each child's leading column. `IX_XpHistory_UserId_GuildId_Xp` pays for itself, because it makes the per-gain `Sum`
  index-only. `SchemaIndexTests` doesn't flag it, because its third column differs from the key's. `IX_Sin_UserId_GuildId_SinOn`
  serves `/SinLog`, `/UserInfo` and the spam escalation count. `IX_Sin_ModeratorId_GuildId_SinType` makes `/SinLog type:Mod`
  index-only. Both `CustomCommandUsages` indexes are used: `(GuildId, Name, UsedAt)` by the overall leaderboard and the
  key by per-command stats. `CustomCommands (GuildId, Name)` serves `LatestVersions` and the autocomplete.
  `AlertOccurrences` has a partial unique index for open rows and a `LastSeen` index for the daily delete. No missing
  index was found.
- **Compiled queries.** Only `GainUserXp`'s `Max` runs often enough to matter, and it is compiled. Compiling the `Sum`,
  the edit/delete projections or the member-update reads would save microseconds of expression hashing per call, while
  each call already waits on a round trip. That isn't worth the extra lines.
- **Split queries.** The only `Include`s load one command's roles (`GetCustomCommandQuery`, `Revert`). The only
  collection projections are one message's attachments. There is no cartesian blow-up, so there is nothing to split.
- **`AsNoTracking`.** Every read-only query on a frequent path already has it. The tracked reads (`RemoveChannelLock`,
  `AlertOccurrenceStore.UpsertAsync`) are tracked on purpose or belong to section 4.
- **`ExecuteUpdate`/`ExecuteDelete`.** They are already used wherever rows change in bulk: reactions, publish ids, log
  cleanup, alerts, Forget. No load-modify-save loop is left on a frequent path.
- **Value converters.** After D-6 and S-1, every `FromDatabase`/`Create` used in a converter is a plain constructor.
  `ReactionEmoji.ParseFromDatabase` uses a regex, but nothing reads `Reactions` rows yet. `XpHistoryEntry.Xp` wraps
  `RawXp` without checks. None of these costs anything noticeable.
- **`AddMessageEvent`.** The message, its first history row and its attachments go in one batched round trip. Replays
  hit the unique key and are logged at Debug.
- **Daily history trim.** The `Distinct` plus lateral `Skip(3)` delete scans each table once a day. A window function
  would read better but EF can't express it, and once a day is fine.
- **Startup sync.** One context and one save for every guild is a startup cost. KI-97 already covers its correctness.

## Run on production before deciding

These size P-9, P-12 and P-16.

```sql
SELECT relname, pg_size_pretty(pg_total_relation_size(oid)) AS total, reltuples::bigint AS rows
FROM pg_class WHERE relname IN ('Messages', 'MessageHistory', 'XpHistory', 'Attachments', 'Sins', 'UsernameHistory',
    'NicknameHistory', 'Avatars') ORDER BY pg_total_relation_size(oid) DESC;
SELECT "GuildId", "UserId", count(*) FROM "XpHistory" GROUP BY 1, 2 ORDER BY 3 DESC LIMIT 5;
EXPLAIN (ANALYZE, BUFFERS) SELECT "GuildId", "UserId", sum("Xp") FROM "XpHistory" GROUP BY 1, 2;
```

## Questions for the owner

1. **P-9, `bigint` ids.** This is the biggest storage and cache win, but it rewrites every table in both databases
   once. Do it with the first deploy, since the timeout and downtime are already planned for `ModelRedesign`? Do it in a
   later release? Or skip it if the size query shows small tables?
2. **P-12, integer discriminators.** Strings make the tables readable in pgAdmin. Integers keep `main`'s columns, skip
   the `XpHistory` rewrite and the full `MessageHistory` update at deploy, and make every row smaller. Which matters
   more to you? Custom command and settings discriminators are low-volume and can stay strings either way.
3. **`Reaction.GuildId` (P-14).** It is new and nothing reads it yet. Is it meant for the planned reaction-usage
   features, so it doesn't need a join to `Messages`? If so, keep it and drop only the `MessageHistory` and `Pardon`
   copies.
4. **Pardon dates (P-18).** A sin can be pardoned more than once. `/SinLog` shows the latest pardon's moderator with the
   first pardon's date. Should both come from the latest pardon?
5. **P-11, raw SQL.** Is one `ON CONFLICT` statement on the reaction path acceptable? The alternative is keeping three
   EF round trips for type safety.
6. **P-13 and existing databases.** Do you have a development database that already ran `ModelRedesign` and the
   `DropRedundantIndexes` migrations? If so, P-13 needs the `DROP INDEX IF EXISTS` variant, or that database rebuilt.
