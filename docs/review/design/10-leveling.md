# Section 10: Leveling

Reviewed 2026-11-09, against the code as it will be once every fix in `review-decisions.md` is in (X-1 to X-13, S-1, S-4,
S-12, D-5, P-2, I-4, I-5 and the `RequiredGuild` ruling).

The biggest wins are three. First, cache the ignore list as three id sets instead of a bag of records (LVL-1). That
removes two `_` throws and two near-copies, and every message then does two set lookups instead of a type switch over
every item. Second, decide whether the leaderboard should stay a materialized view (LVL-2). Replacing it with one live
per-guild query removes a background task, an entity, the test-isolation gotcha and three round trips, but that depends
on production numbers. Third, putting the eleven leveling commands into the T-1 shape saves about 150 lines (LVL-3).
After those come smaller pure-function and invalid-state cleanups in reward granting, level math and command options
(LVL-4 to LVL-11). There are 12 findings: 0 high, 3 med, 8 low and 1 optional.

## Findings

#### LVL-1 Cache the ignore list as three id sets, not a set of records  `state` `perf` `remove` · **med**
- **Where:** `Grimoire.Settings/Services/SettingsModule.Leveling.Ignores.cs:17-48` (`IsMessageIgnored`,
  `IsMemberIgnored`, each with a `_ => throw new InvariantViolationException()` arm), `:51` (`GetAllIgnoredItems`,
  cached as `FrozenSet<XpIgnoredItem>`), `GainUserXp.HandleEventAsync` (`GainUserXp.cs:50-56`, which builds
  `member.Roles.Select(...).ToHashSet()` on every message), `UserInfoCommands.GetAndAddLevelInfo` (`:154`), and
  `IgnoreCommandGroup.BuildMessage` (`IgnoreCommandGroup.GetIgnoredItems.cs:48-63`, three `OfType<>` passes).
- **Now:** on every message in a guild with leveling on, `IsMessageIgnored` allocates a `HashSet<RoleId>` of the
  member's roles. DSharpPlus's `Roles` looks each role up in the guild dictionary to build it. The method then runs
  `Any` over every ignored item with a type switch whose `_` arm can't run. `IsMemberIgnored` is the same 15 lines
  without the channel arm. `/Ignore View` splits the same set again by type.
- **Proposed:** cache the shape the decisions need. The cache factory builds it once from the same query:
  ```csharp
  public sealed record IgnoreList(FrozenSet<ChannelId> Channels, FrozenSet<UserId> Members, FrozenSet<RoleId> Roles)
  {
      public bool CoversMember(UserId user, IEnumerable<RoleId> roles)
          => this.Members.Contains(user) || roles.Any(this.Roles.Contains);

      // X-12: a thread is covered by its parent channel.
      public bool CoversMessage(UserId user, IEnumerable<RoleId> roles, ChannelId channel, ChannelId? parent)
          => this.Channels.Contains(channel) || parent is { } p && this.Channels.Contains(p)
             || this.CoversMember(user, roles);
  }
  ```
  `IsMessageIgnored` and `IsMemberIgnored` become one-line `Map`s, or plain `bool`s under F-1. `GainUserXp` passes
  `member.Roles.Select(r => r.GetRoleId())` without the `HashSet`. `/Ignore View` lists the three sets directly.
  X-12's "report no-ops" filter becomes `set.Contains` per item. Nothing in the hot path uses `XpIgnoredItem` after
  this.
- **Size:** about −25 lines. `Ignores.cs` goes from 32 lines for the two methods to about 8, plus the 10-line record.
  `BuildMessage` goes from 16 lines to about 8. 4 files.
- **Needs:** nothing. F-1 shortens it further.
- **Interacts with:** X-12, S-4, KI-73 (the parent channel is the `parent` argument; categories stay a separate
  question). Section 4 owns cache invalidation, and the cache key and `RemoveAsync` call don't change. Section 2's
  `closed XpTrackedItem` still covers `BuildIgnoreList`'s `_` arm. These two arms go without waiting for cs15.

#### LVL-2 The leaderboard view: refresh every guild every 15 minutes, or query one guild on demand  `perf` `remove` · **med**
- **Where:** `LeaderboardRefreshBackgroundTask.RunTask` (`LeaderboardRefreshBackgroundTask.cs:20-26`, on a 15-minute
  timer), migration `20251025193658_LeaderboardView`, `Grimoire.Domain/LeaderboardView.cs`,
  `LeaderboardViewConfiguration.cs`, `GrimoireDbContext.LeaderboardView`, and `GetLeaderboard.GetLeaderboardAsync`
  (`GetLeaderboard.cs:74-98`).
- **Now:** every 15 minutes, `REFRESH MATERIALIZED VIEW CONCURRENTLY` aggregates every `XpHistory` row of every guild,
  ranks them, and diffs the result against the old view, whether or not anyone ran `/Leaderboard`. `XpHistory` is
  append-only, with one row per XP gain, so the cost only grows. `/Leaderboard` then makes three round trips: a
  `CountAsync`, a rank lookup, and the 15 rows. `AsNoTracking()` on a keyless view does nothing. Costs elsewhere: tests
  must `REFRESH` by hand around Respawn (the test-infra gotcha), the first refresh comes 15 minutes after startup
  (KI-99), the board lags by up to 15 minutes, and the branch review's pre-deploy `EXPLAIN ANALYZE` exists because of
  the refresh. `ROW_NUMBER` has no tie-break, so tied members can swap ranks between refreshes.
- **Proposed:** if the production numbers allow it, drop the view and compute one guild's board live in a single
  statement. A covering index serves it with an index-only scan:
  ```csharp
  // migration: DROP MATERIALIZED VIEW leaderboard_view;
  //            CREATE INDEX "IX_XpHistory_GuildId_UserId" ON "XpHistory" ("GuildId", "UserId") INCLUDE ("Xp");
  var rows = await dbContext.Database.SqlQuery<LeaderboardRow>($"""
      WITH ranked AS (
          SELECT "UserId", SUM("Xp") AS "TotalXp",
                 ROW_NUMBER() OVER (ORDER BY SUM("Xp") DESC, "UserId") AS "Rank",
                 COUNT(*) OVER () AS "Total"
          FROM "XpHistory" WHERE "GuildId" = {guildId.Value} GROUP BY "UserId"),
      start AS (SELECT COALESCE((SELECT GREATEST(1, LEAST("Rank" - 5, "Total" - 14))
                                 FROM ranked WHERE "UserId" = {centredOn?.Value}), 1) AS "First")
      SELECT ranked.* FROM ranked, start
      WHERE "Rank" >= "First" AND "Rank" < "First" + 15 ORDER BY "Rank"
      """).ToArrayAsync(ct);
  // X-8's window always contains the centred member, so "not found" is exactly: centredOn not among rows.
  ```
  This is one round trip instead of three, has no lag, and gives a stable order for ties. The cost moves from
  "every guild, 96 times a day" to "one guild, per `/Leaderboard`". `/Leaderboard` is an occasional user command, and
  the scan is limited to that guild's rows. If the numbers show a single large guild's aggregate is too slow to run
  per command, keep the view. In that case, the cheaper change is to fold the count into the rank query
  (`board.Where(x => x.UserId == u).Select(x => new { x.Rank, Total = board.Count() })`), which saves one round trip.
- **Size:** about −70 lines (the task 27, the entity and config 35, the DbSet, the registration, and the test refresh
  helpers), plus about +25 (the query and a row record). 6 files, 1 migration. The test
  `LevelAndLeaderboardTests.Leaderboard_PicksUpNewXp_AfterTheRefreshTask` pins the lag and goes.
- **Needs:** a migration, its own one (per the migration-status memory). Production numbers: the branch review's
  `EXPLAIN ANALYZE`, plus the same query filtered to the largest guild. Check how `SqlQuery<T>` maps the `numeric(20,0)`
  id columns, since the row record may need `decimal`.
- **Interacts with:** X-8 and X-13 (the window logic is kept), KI-99, P-5/S-8 (`DropRedundantIndexes` removed
  `IX_XpHistory_GuildId_Xp`, and the new index is its replacement in the shape this query needs). The branch review
  called the lag deliberate, so this needs an owner decision (see Questions).

#### LVL-3 The leveling commands measured against T-1  `pipe` `state` · **med**
- **Where:** all eleven command methods under `Grimoire/Features/Leveling`. Each one defers first. Errors are
  reported through `SendErrorResponseAsync` (Rewards View, Ignore View, LevelSettings View), a public yellow
  `ReplyAsync` (`GetLevel.cs:52`, `IgnoreCommandGroup.cs:43`, `GetLeaderboard.cs:42`), a public red `ReplyAsync`
  (`RewardCommandGroup.OnFail`, `AddReward.cs:73`), or `ReplyResultAsync`. Reply and log pairs are written by hand in
  `OnAddSuccess`, `OnRemoveSuccess`, `ReportReclaimAsync` and `AwardAsync`.
- **Now:** five different error renderers in one feature. `/Rewards` errors are public (KI-80). `/Leaderboard`'s
  missing-user warning is public because it comes before the defer (KI-81). Award and Reclaim unwrap a validated XP
  amount with `InvariantViolationException` (F-7).
- **Proposed:** T-1, following F-1's Rewards Add sketch. Two notes specific to leveling:
  - **Integer inputs need no converter.** F-6's `ValidatedConverter<T>` is string-based, but `PositiveXpAmount` comes
    from an `int` option. Put `PositiveXpAmount.Create(xpToAward)` in `RunAsync`'s input. `[MinMaxValue(1)]` means its
    failure arm never runs in practice, but it's an ordinary `Error.Invalid`, not a throw. F-7's unwrap at
    `AwardUserXp.cs:40` goes with no new type. Reclaim does the same with F-7's `PositiveXpAmount.Negate()`.
  - Paged replies (Rewards View, Ignore View) keep `ReplyPagedAsync`. Only their F-1 error branch and the empty-list
    reply move into the shape.

  | File | Lines today | Target | Main source of the cut |
  |---|---|---|---|
  | `Awards/AwardUserXp.cs` | 54 | ~40 | unwrap, Reply/Log pair |
  | `Awards/ReclaimUserXp.cs` | 90 | ~62 (~50 with LVL-8) | `ReportReclaimAsync`, `MatchAsync` tail |
  | `Rewards/RewardCommandGroup.AddReward.cs` | 93 | ~50 | `OnFail`, `OnAddSuccess` (F-1's sketch) |
  | `Rewards/RewardCommandGroup.RemoveReward.cs` | 58 | ~38 | `OnRemoveSuccess` |
  | `Rewards/RewardCommandGroup.GetRewards.cs` | 50 | ~42 | F-1 error branch |
  | `Settings/LevelSettingsCommandGroup.SetLevelSettings.cs` | 94 | ~85 (~75 with LVL-11) | `ReplyResultAsync` → `RunAsync` |
  | `Settings/LevelSettingsCommandGroup.GetLevelSettings.cs` | 52 | ~42 | F-1 error branch |
  | `Settings/IgnoreCommandGroup.cs` | 83 | ~68 | empty check becomes input validation, Reply/Log |
  | `Settings/IgnoreCommandGroup.GetIgnoredItems.cs` | 64 | ~48 (with LVL-1) | F-1 error branch, `BuildMessage` |
  | `UserCommands/GetLevel.cs` | 92 | ~72 (with LVL-9, LVL-10) | settings error branch |
  | `UserCommands/GetLeaderboard.cs` | 101 | ~78 (with LVL-8, LVL-9) | pre-defer warning, `Match` tail |
  | **Total** | **831** | **~625** | about −150 from the shape, the rest from the findings named |
- **Size:** about −200 lines across 11 files, of which about −150 come from F-5 itself.
- **Needs:** F-5 (and F-3, F-4). F-1 for the error branches.
- **Interacts with:** KI-80 and KI-81 (both fixed by the shape), X-6 (Rewards logs go to `Moderation`), X-7 (Remove's
  lookup stays as the first `Bind`), I-4, I-5, X-5. The Rewards hierarchy error prefix is pinned by
  `RewardCommandsTests.Add_ARoleAboveTheBot_IsRefused` (as F-5 says).

#### LVL-4 `GainUserXp`'s settings-outage block goes under F-1  `pipe` `remove` · **low**
- **Where:** `GainUserXp.HandleEventAsync` (`GainUserXp.cs:68-82`), `LogSettingsUnavailable` (`:225-226`), and the
  `IAlertSender alerts` constructor parameter (`:23`).
- **Now:** 15 lines turn a `Result.Fail` from `GetLevelingSettings` back into a log line and a hand-built `Alert`. They
  read `error.Code` (F-4's second production use). They run after the cooldown and `Sum` queries, so an outage still
  costs two queries per message.
- **Proposed:** with F-1, `var settings = await this._settingsModule.GetLevelingSettings(guildId);`. The outage
  throws, and `IClientErrorHandler` logs and alerts it once. If F-1 isn't taken, at least read the settings before
  opening the `DbContext`, because the value is cached and the two queries are wasted when it fails.
- **Size:** about −19 lines, 1 file.
- **Needs:** F-1.
- **Interacts with:** F-4, S-4.

#### LVL-5 `ApplyRewards`: a pure reward plan, then the I/O  `pipe` `perf` · **low**
- **Where:** `GainUserXp.ApplyRewards` (`GainUserXp.cs:116-180`), `RemoveRewardsForDeletedRolesAsync` (`:186-200`) and
  `SendRewardMessageAsync` (`:202-219`). These run on every XP gain, once per cooldown per active member.
- **Now:** one 65-line method looks up the rewards, finds orphans, computes the due rewards with
  `member.Roles.All(...)` per reward (which re-resolves every member role through the guild dictionary each time),
  grants them, reports the refusals, sends DMs and logs. The role is looked up three times: `ContainsKey` at :128,
  `TryGetValue` at :147 with a `continue` that can't run after the orphan filter, and `TryGetValue` again in
  `SendRewardMessageAsync`. Orphans are removed with one `SaveChangesAsync` and one cache invalidation each.
- **Proposed:** a pure, unit-testable step that does every lookup once. The I/O then reads straight down:
  ```csharp
  internal sealed record RewardPlan(RewardEntry[] Orphaned, (RewardEntry Reward, DiscordRole Role)[] Due);

  internal static RewardPlan PlanRewards(IEnumerable<RewardEntry> rewards,
      IReadOnlyDictionary<ulong, DiscordRole> guildRoles, IReadOnlySet<RoleId> memberRoles, int level)
  {
      var lookup = rewards.ToLookup(r => guildRoles.TryGetValue(r.RoleId.Value, out var role) ? role : null);
      return new(lookup[null].ToArray(),
          [.. lookup.Where(g => g.Key is not null).SelectMany(g => g.Select(r => (r, g.Key!)))
              .Where(x => x.r.RewardLevel <= level && !memberRoles.Contains(x.r.RoleId))]);
  }
  ```
  `granted` holds `(reward, role)` pairs, so `SendRewardMessageAsync` takes the role and loses its `TryGetValue`.
  `memberRoles` is the set `HandleEventAsync` already has (or builds once, after LVL-1). Orphans go through one
  `SetRewardsAsync(IEnumerable<Reward>)`: one save, one invalidation. That's a rare path, but it also closes the window
  in which a second message removes them again.
- **Size:** about −10 lines in `GainUserXp.cs`, about +5 in Settings for the batch write. New unit tests replace
  scenarios that today need the fake gateway.
- **Needs:** S-12 (factories no longer return `Validation`, so the removal loop has no `.ToResult()`).
- **Interacts with:** X-1, X-2, KI-41, KI-75 (the `catch (UnauthorizedException)` is unchanged here).

#### LVL-6 Cache rewards as a level-sorted table  `state` `perf` · **low**
- **Where:** `SettingsModule.GetLevelingRewardsAsync` (`SettingsModule.Leveling.Rewards.cs:17-39`, cached as
  `IReadOnlySet<RewardEntry>`). Its consumers: `GetLevel.cs:62-64` (`OrderBy` then `FirstOrDefault`),
  `RewardCommandGroup.DescribeRewards` (`OrderBy`), `GainUserXp.cs:134` and `UserInfoCommands.cs:149`
  (`Where(RewardLevel <= level)`), and `RemoveReward.cs:34` (`Any` by role).
- **Now:** a set of records, which are unique by role anyway because the query keeps the latest row per role. Set
  equality buys nothing. Every reader sorts or scans it again.
- **Proposed:** the cache factory sorts once, into
  `sealed record RewardTable(ImmutableArray<RewardEntry> ByLevel)` with `Due(int level)` (a `TakeWhile`),
  `Next(int level)` and `Contains(RoleId)`. `/Level`'s Next Reward is `rewards.Next(level)`, and UserInfo's earned list
  is `rewards.Due(level)`.
- **Size:** about ±0 lines (+10 for the record, −8 at the call sites). 5 files.
- **Needs:** nothing. Pairs with LVL-5.
- **Interacts with:** KI-42 (the order fix becomes structural), section 4 (cache shape only).

#### LVL-7 Level math: one threshold function, and no offset parameter  `state` `remove` · **low**
- **Where:** `LevelingSettingEntry.IsAbove` (`LevelingSettingEntry.cs:52-53`) and `GetXpNeededForLevel(int level,
  int levelModifier = 0)` (`:55-64`). The callers are `GetLevel.cs:57-58` and `GetLevelFromXp`.
- **Now:** the curve `Base + round(Base·Modifier/100·s)·s` is written twice. `IsAbove` computes it in `Int128` because
  it overflows at the extremes, while `GetXpNeededForLevel` computes it in `long`, so the two can disagree exactly
  where X-9's fix made the search safe. The `0 => Base.Value` arm is what the formula already gives at step 0.
  `levelModifier` is an offset added to `level` that has one caller, with `1`.
- **Proposed:**
  ```csharp
  private Int128 Threshold(long step)
      => this.Base.Value + (Int128)(long)Math.Round(this.Base.Value * (this.Modifier.Value / 100.0) * step) * step;
  /// <summary>Total XP needed to reach <paramref name="level" />. Level 1 needs none.</summary>
  public long XpToReach(int level) => level <= 1 ? 0 : (long)Int128.Min(this.Threshold(level - 2), long.MaxValue);
  public LevelProgress Progress(long xp) { var level = this.GetLevelFromXp(xp); /* XpToReach(level), (level + 1) */ }
  ```
  `GetLevelFromXp` calls `Threshold(step) > xp`. `/Level` takes one `Progress(xp)` value instead of three calls and a
  subtraction.
- **Size:** about −6 lines, 2 files. The domain tests for `GetXpNeededForLevel` are renamed.
- **Needs:** nothing.
- **Interacts with:** X-9 (keeps its binary search).

#### LVL-8 An option enum that decides whether another parameter counts  `state` · **low**
- **Where:** `ReclaimUserXp.XpOption` with `int amount = 0` (`ReclaimUserXp.cs:18-25`, `:38-41`), `ValidateOption`
  (`:54-57`), and `GetLeaderboard.LeaderboardOption` with `DiscordUser? user` (`GetLeaderboard.cs:19-24`, `:40-44`,
  `:56-62`, including one of T-4's six enum `_` guards).
- **Now:** both commands accept combinations that mean nothing. `option:All amount:500` silently ignores the amount.
  `option:Amount` with no amount, and `option:User` with no user, are refused at run time. `option:Top user:@x`
  silently ignores the user. Each combination needs a runtime check or an enum switch.
- **Proposed:** let the optional parameter carry the mode. `/Reclaim user [amount]`, where a blank amount takes all
  (`[MinMaxValue(1)] int? amount`, then `Math.Min(memberXp, amount ?? memberXp)`). `/Leaderboard [user]`, where a
  blank user shows the top. "Me" means picking yourself, which Discord's user picker offers first. This removes both
  enums, `ValidateOption`, the missing-user branch and the enum guard.
- **Size:** about −25 lines, 2 files. `ReclaimUserXpTests`, `GetLeaderboardTests` and `CommandMatrixTests` name the
  enums.
- **Needs:** an owner decision, because it changes the commands' options (see Questions).
- **Interacts with:** KI-81 (the branch it reports goes), X-13 (the `xpToTake <= 0` check stays).

#### LVL-9 The "user command channel" visibility rule is written twice  `remove` `pipe` · **low**
- **Where:** `GetLevel.LevelAsync` (`GetLevel.cs:32-40`) and `GetLeaderboard.LeaderboardAsync`
  (`GetLeaderboard.cs:46-54`).
- **Now:** both make the same nine-line decision. A member without Manage Messages, outside the user-command channel,
  gets an ephemeral slash reply or no reply at all to a text command. Each copy calls `GetOrElse(() => null)` twice.
- **Proposed:** one extension that returns what T-1 needs:
  ```csharp
  /// <summary>Private outside the user-command channel, unless a moderator asks. Null: a text command there gets no reply.</summary>
  public async Task<Visibility?> UserCommandVisibilityAsync(SettingsModule settings) { ... }
  ```
  Each command becomes `if (await ctx.UserCommandVisibilityAsync(...) is { } visibility) await ctx.RunAsync(..., visibility);`.
- **Size:** about −12 lines, 3 files.
- **Needs:** F-5 for `Visibility` (or a `bool ephemeral` before it).
- **Interacts with:** F-1 (`GetOrElse` goes).

#### LVL-10 `/Level` fetches the reward role it only mentions, and waits on independent lookups in turn  `perf` · **low**
- **Where:** `GetLevel.LevelAsync` (`GetLevel.cs:44-47`, `:66-68`, `:73-75`), once per `/Level`.
- **Now:** `GetRoleOrDefaultAsync(nextReward.RoleId)` exists only to print `roleReward.Mention`. If the role isn't
  cached it can mean a REST call, and if the role was deleted it shows "None" (KI-79). The XP `Sum` and
  `GetMemberOrDefaultAsync`, which can also be a REST call, are independent but run one after the other.
- **Proposed:** `RoleExtensions.Mention(nextReward.RoleId)`, as X-10 did for Rewards View. Run the sum and the member
  fetch with `Task.WhenAll`. KI-79's "None" then shows a deleted role's mention instead. After X-2 that only lasts until
  the next XP gain in the guild removes the reward.
- **Size:** about −4 lines, 1 file.
- **Needs:** nothing.
- **Interacts with:** KI-79, X-10.

#### LVL-11 `LevelSettingsOptions` duplicates `LevelSettings` only to carry display names  `remove` · **low**
- **Where:** `LevelSettingsCommandGroup.LevelSettingsOptions` and `ToLevelSettings`
  (`LevelSettingsCommandGroup.SetLevelSettings.cs:22-45`, one of T-4's six enum `_` guards). The second enum is
  `Grimoire.Settings/Enums/LevelSettings.cs`.
- **Now:** a Grimoire enum with `[ChoiceDisplayName]` and a switch that maps it onto the Settings enum, because
  Settings can't reference DSharpPlus. The two enums have the same underlying values (0 to 3).
- **Proposed:** take `LevelSettings` directly, with `[SlashChoiceProvider<LevelSettingChoices>]`, a 10-line
  `IChoiceProvider` in Grimoire that returns the four display names with `(int)LevelSettings.X`. DSharpPlus's
  `EnumConverter<T>` converts the value. The registered choices keep the same names and values, so Discord sees no
  change. The reply says `XpTimeoutPeriod` instead of `TextTime` unless the provider's name is reused.
- **Size:** about −10 lines and one enum guard, 1 file plus one new file.
- **Needs:** a spike to confirm the slash processor uses a choice provider on an enum parameter instead of
  `EnumOptionChoiceProvider`. `LevelSettingsTests` uses the old enum and pins `"Updated {option} level setting…"`.
- **Interacts with:** none.

#### LVL-12 Field copies of primary-constructor parameters  `remove` · **optional**
- **Where:** `GainUserXp.cs:33-36`, `AwardUserXp.cs:17-18`, `ReclaimUserXp.cs:27-28`, `RewardCommandGroup.cs:17-18`,
  `IgnoreCommandGroup.cs:20-21`, `LevelSettingsCommandGroup.cs:17-18`, `GetLevel.cs:19-20`, `GetLeaderboard.cs:26-27`
  and `LeaderboardRefreshBackgroundTask.cs:18`.
- **Now:** 19 lines copy primary-constructor parameters into `private readonly` fields, presumably so they can be
  used with `this.`. `GainUserXp` already mixes the two styles: `alerts` and `issueReporter` are used directly.
- **Proposed:** pick one rule repo-wide. Either use the parameters directly, and let the `this.` editorconfig rule
  exempt them, or always copy them. This is a style call, which is why it's optional. Section 6 or the roll-up should
  decide it once.
- **Size:** −19 lines here if parameters are used directly.
- **Needs:** an editorconfig decision.
- **Interacts with:** none.

### Checked, no finding

- **cs15.** No leveling-specific win. `BuildIgnoreList`'s `_` arm goes with section 2's `closed XpTrackedItem`. The
  `SnowflakeObject` switches in `/Ignore Add` and `/Ignore Remove` are over a DSharpPlus type. A
  `union Mentionable(DiscordUser, DiscordRole)` would remove their two `_` arms, but it needs a custom slash converter
  for a variadic mentionable option, which is more code than the two arms. The four enum `_` guards stay, per T-4, or
  go with LVL-8 and LVL-11.
- **Streaming.** Nothing in leveling has an unbounded result set. The leaderboard reads 15 rows. The ignore and reward
  queries run on a cache miss, once per day per guild. Their `ToListAsync` loads `Watched`/`RewardRemoved` tombstones
  and then drops them, which doesn't matter at that rate.
- **The level search.** About 70 iterations, twice per XP gain, in pure arithmetic. A closed form would bring back X-9's
  estimate problem.
- **Re-granting on every gain.** `ApplyRewards` runs whether or not the level changed. That's how missed and
  bot-offline rewards get caught up, and with LVL-6 it costs one cache hit. See Questions for the side effect.
- **The per-message `Max` and `Sum` queries** belong to section 5.

## Questions for the owner

1. **LVL-2, the leaderboard.** The branch review called the 15-minute lag deliberate. Would you trade the view for a
   live per-guild query, if `EXPLAIN ANALYZE` on the largest guild comes in at tens of milliseconds? If so, should a
   member with 0 XP (after `/Reclaim` takes everything) still appear on the board? The live query can add
   `HAVING SUM("Xp") > 0` at no cost. The view lists them today.
2. **LVL-8, changing command options.** Dropping `option` from `/Reclaim` and `/Leaderboard` changes what moderators
   and members type. Is that acceptable, or should the options stay for familiarity?
3. **Reward self-healing.** Because `ApplyRewards` runs on every XP gain, a reward role that a moderator removes from a
   member by hand comes back the next time that member earns XP. Is that intended, as on `main`? If not, running it
   only on level-up is a one-line change, but rewards missed while the bot was offline would then wait for the next
   level.
4. **Reward level 0.** `/Rewards Add` accepts `level:0`, which behaves exactly like level 1, because levels start at 1.
   Should the minimum be 1, with `RewardAdded.Create`'s check matching it?
5. **LVL-12.** Use primary-constructor parameters directly, or keep the field copies? This needs one repo-wide answer.
