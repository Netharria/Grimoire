# Section 4: Settings and caching

Reviewed 2026-11-09 against the code as it will be once every fix in `review-decisions.md` is in. Read only, nothing run.

The biggest win is one cached `GuildConfig` snapshot per guild in place of 24 separately cached settings (SET-1). It
removes the `CachedSetting` hierarchy and the second leveling-settings key that `SetGuildSetting` never clears, and it
makes every write clear exactly one key. Next come two shared helpers, one for cached reads and one for
append-then-invalidate writes, which take most of the per-method boilerplate out of `SettingsModule` (SET-2, about −120
lines). Then per-guild override maps replace per-channel keys (SET-3), which ends the cold-cache database query that
every new thread costs today. Outside Settings, the largest per-message database read is `GainUserXp`'s timeout query.
An in-memory gate makes it free for messages inside the timeout and also closes a double-XP race (SET-4). Findings
SET-1 to SET-12 follow, then a verdict on the `SettingsModule` layout.

## Where the hot path stands today

Every settings read on a message or event path is already served from the cache. Nothing in `Grimoire.Settings` hits
the database on a warm cache, except `GetActiveMute` on member join, which is rare enough to leave alone. The uncached
reads that matter are all in `GrimoireDbContext`:

| Reader | Event, and how often | Settings reads (cache) | `GrimoireDbContext` reads | Finding |
|---|---|---|---|---|
| `GainUserXp.HandleEventAsync` | every message in a Leveling guild | module, ignores, level settings, rewards | timeout `MAX` on every message, plus `SUM` when the member gains XP | SET-4 |
| `AddMessageEvent` | every message in a MessageLog guild | module, plus one override per level of the channel tree | none (insert only) | SET-3 |
| `SpamTrackerModule.CheckSpam` | every message in an AntiSpam guild | one override per level of the channel tree | none | SET-3, SET-8 |
| `TextCustomCommandProcessor.FetchCommandAsync` | every `!` message from a non-bot | module | one `CustomCommands` lookup | SET-5 |
| `UpdatedNicknameEvent`, `UpdatedAvatarEvent`, `UpdatedUsernameEvent` | every `GuildMemberUpdated` in a UserLog guild, including role changes and the bot's own reward grants | module (×3) | three "latest row" queries | SET-6 |
| `LockedThreadEvents` | every message and reaction in a thread | module, thread-lock set | none | none |
| `GuildLogPublisher`, `RequireModuleEnabledCheck` | every log, every gated command | module, log channel | none | SET-1 |

A cold cache costs one query per key. With per-channel keys (SET-3), a message in a thread nobody has posted in yet
today costs two queries (log override and spam override for the thread), and a new channel costs up to six.

## Findings

#### SET-1 One cached `GuildConfig` per guild instead of one entry per setting  `perf` `remove` `state` · **high**
- **Where:** `SettingsModule.GetGuildSetting` (`Grimoire.Settings/Services/SettingsModule.cs:31`), `ToCachedSetting`
  (`:70`), the `CachedSetting` records (`:171-177`), `ParseChannelId`/`ParseRoleId` (`:158-169`);
  `SettingsModule.ModuleEnabled.cs` (`IsModuleEnabled`, `GetAllModuleState`, `ParseEnabled`);
  `SettingsModule.LogChannels.cs`; `SettingsModule.Moderation.AutoPardon.cs:19` (`GetAutoPardonDuration`);
  `SettingsModule.Moderation.Mutes.cs:18-32` (`GetEffectiveMuteRole`, `GetConfiguredMuteRole`);
  `SettingsModule.Leveling.Levelsettings.cs:26-70` (`GetLevelingSettings`, `GetLevelingSettingsCacheEntry`, and the
  `TapAsync` invalidation in `SetLevelingSettings`); `CacheKey.GuildSetting`/`CacheKey.LevelingSettings`.
- **Now:** each of the 24 `GuildSettingType`s is a separate cache entry with its own key, built per call as
  `$"{type}_{guildId}"` plus an anonymous state object. A message in a guild with every module on makes 4 to 6 of these
  lookups across the handlers, each through `ExecuteSafelyAsync`, a `Result` and a `Map` task. The four leveling types
  are cached a second time under `LevelingSettings_{guild}`. So `SetGuildSetting` clears a `XpGainAmount_{guild}` key
  that nothing reads, and the key that is read is cleared only by `SetLevelingSettings`'s `TapAsync`, which skips
  `NotModified` (the leveling half of KI-103). `GetAllModuleState` needs `Result.WhenAll` over six lookups. Three
  getters re-check things the snapshot would hold, such as `GetEffectiveMuteRole` reading the Moderation flag and then
  the role. `GetAutoPardonDuration` has a `_ => throw new InvariantViolationException()` arm over `CachedSetting`.
- **Proposed:** one entry per guild, loaded with the query `GetGuildSettings` already runs (latest row per type, over
  the existing `IX_GuildSettings_GuildId_Type_SetAt`), parsed once into a typed record:
  ```csharp
  public sealed record GuildConfig(
      GuildModuleState Modules,
      FrozenDictionary<GuildLogType, ChannelId> LogChannels, // a disabled or unset log is absent
      ChannelId? UserCommandChannel,
      RoleId? MuteRole,
      TimeSpan AutoPardonDuration,
      LevelingSettingEntry Leveling)
  {
      public bool IsEnabled(Module module) => this.Modules.IsEnabled(module); // General => true
      public ChannelId? LogChannel(GuildLogType type) => this.LogChannels.TryGetValue(type, out var id) ? id : null;
      public RoleId? EffectiveMuteRole => this.Modules.Moderation ? this.MuteRole : null;
  }

  public Task<GuildConfig> GetGuildConfig(GuildId guildId, CancellationToken ct = default)
      => this.CachedAsync(CacheKey.Guild(guildId), guildId,
          async (db, id, innerCt) => GuildConfig.FromRows(await LatestSettings(db, id).ToListAsync(innerCt)), ct);
  ```
  `SetGuildSetting` clears `CacheKey.Guild(guildId)` on every path, including the redundant write, which also closes
  KI-103 for guild settings. Keep `IsModuleEnabled`, `GetLogChannelSetting` and the rest as one-line projections, so
  the 23 `IsModuleEnabled` callers don't change. A handler that needs two values reads the snapshot once (section 5
  decides where to share it). `CachedSetting`, `ToCachedSetting`, the leveling key and its `TapAsync`,
  `Result.WhenAll`'s only caller and the AutoPardon `_` arm all go, on net10 with no cs15 needed.
- **Size:** about −110 lines in Settings. The 5 files above shrink into one `SettingsModule.GuildSettings.cs` of about
  110 lines (`GuildConfig.FromRows` is about 30 of them). Call sites are unchanged. `GuildSettingType` keeps its
  `ToGuildSettingType` maps for the writers.
- **Needs:** nothing. With F-1, `GetGuildConfig` returns `Task<GuildConfig>`. If S-4's shape stands, it returns
  `Task<Result<GuildConfig>>` and the projections `Map` it. Either way there is one failure point per guild instead of
  one per setting.
- **Interacts with:** S-2 (the "failure caches defaults" bug can't come back, since the snapshot factory throws), S-6,
  S-9 (`GuildSettingDefault` still parses as "default"), S-11 (`GetGuildSetting`'s dead `NotFound` arm and its
  `.AsTask()` callers go), I-14, KI-103, KI-105, F-1, F-3 (`NotModified` from `SetGuildSetting`).

#### SET-2 Two private helpers for cached reads and append-then-invalidate writes  `remove` `pipe` · **med**
- **Where:** each cached read: `GetLevelingRewardsAsync` (`SettingsModule.Leveling.Rewards.cs:17`),
  `GetAllIgnoredItems` (`SettingsModule.Leveling.Ignores.cs:51`), `GetDiagnosticsIgnoredItemsAsync`
  (`SettingsModule.Moderation.DiagnosticsIgnores.cs:17`), `GetChannelLogOverride`
  (`SettingsModule.MessageLog.ChannelOverrides.cs:48`), `GetSpamFilterOverrideAsync`
  (`SettingsModule.Moderation.SpamFilterOverrides.cs:17`), `IsThreadLocked` (`SettingsModule.Moderation.Locks.cs:118`)
  and `GetGuildSetting`. Each append-then-invalidate write: `SetRewardAsync` (`Rewards.cs:41`),
  `AppendIgnoredItemsEvent` (`Ignores.cs:76`), `AddThreadLock`/`RemoveThreadLock` (`Locks.cs:137`, `:149`),
  `AddChannelLock`/`RemoveChannelLock` (`Locks.cs:55`, `:77`), and the two Diagnostics writers.
- **Now:** each read is 15 to 22 lines of the same scaffold: `ExecuteSafelyAsync(async ct => Result.Ok(await
  this._cache.GetOrCreateAsync(key, state, async (s, innerCt) => { await using var dbContext = ...; return ...; },
  this._cacheEntryOptions, cancellationToken: ct)), new Error(...), ct)`. Only the key and the query differ. Some
  lambdas capture `this` (`Rewards.cs:26`, `Ignores.cs:59`) and others pass the factory through an anonymous state
  object. Each write repeats create-context, `Add`, `SaveChangesAsync`, `RemoveAsync` and `Result.Ok`.
- **Proposed:**
  ```csharp
  private ValueTask<T> CachedAsync<TState, T>(string key, TState state,
      Func<SettingsDbContext, TState, CancellationToken, Task<T>> query, CancellationToken ct)
      => this._cache.GetOrCreateAsync(key, (state, query, this._dbContextFactory),
          static async (s, innerCt) =>
          {
              await using var dbContext = await s._dbContextFactory.CreateDbContextAsync(innerCt);
              return await s.query(dbContext, s.state, innerCt);
          }, this._cacheEntryOptions, cancellationToken: ct);

  private async Task<T> AppendAsync<T>(T row, string invalidates, CancellationToken ct) where T : class
  {
      await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
      dbContext.Add(row);
      await dbContext.SaveChangesAsync(ct);
      await this._cache.RemoveAsync(invalidates, ct);
      return row;
  }
  // e.g.
  public Task<IReadOnlySet<RewardEntry>> GetLevelingRewardsAsync(GuildId guildId, CancellationToken ct = default)
      => this.CachedAsync(CacheKey.LevelingRewards(guildId), guildId, LoadRewardsAsync, ct).AsTask();
  ```
  `AppendRangeAsync` covers the two multi-row writers. A write that reads first (`RemoveChannelLock`, the Diagnostics
  pair) keeps its own body and calls a shared `SaveAndInvalidateAsync(dbContext, key)`.
- **Size:** about −120 lines across 7 files (each read from about 18 lines to about 7 plus its query, each write from
  about 9 lines to 1). With F-1 the wrapper also loses its `ExecuteSafelyAsync` layer. Without F-1, the helpers take an
  `Error` and wrap it once, which saves the same lines.
- **Needs:** nothing. Shape depends on owner question 1 of section 1 (F-1 against S-4).
- **Interacts with:** S-4, S-5 (the active-lock queries stay as they are), F-1, F-3.

#### SET-3 Per-guild override maps, and one resolver for both channel-tree walks  `perf` `remove` · **med**
- **Where:** `SettingsModule.ShouldLogMessage` and `GetChannelLogOverride`
  (`SettingsModule.MessageLog.ChannelOverrides.cs:18-69`), `GetAllOverriddenChannels` (`:100`);
  `GetSpamFilterOverrideAsync` and `GetAllSpamFilterOverrideAsync` (`SettingsModule.Moderation.SpamFilterOverrides.cs:17-69`);
  the walk in `SpamTrackerModule.CheckSpam` (`Grimoire/Features/Moderation/SpamFilter/SpamTrackerModule.cs:47-62`);
  `CacheKey.LogOverride`/`SpamFilterOverride`.
- **Now:** both overrides are cached per channel. Most channels have no override, so most entries hold `Inherit`, and
  each one costs a database query the first time a channel, thread or category is seen each day. A message in a
  thread walks thread → channel → category, which is up to three lookups per table per message. The two walks are
  written differently: `ShouldLogMessage` uses the `BuildChannelTree()` dictionary that `AddMessageEvent` builds, and
  `CheckSpam` follows `DiscordChannel.Parent`. The two View commands read the database through
  `IAsyncEnumerable` methods that each have a `catch (Exception)` returning nothing (F-1), and their comment says "EF
  Core cannot translate a .Where() after GroupBy", which the in-memory filter then works around.
- **Proposed:** one entry per guild per table, a `FrozenDictionary<ChannelId, TOption>` of the non-`Inherit` latest
  rows (the View query, already written, minus its catch). `Set…Override` clears the guild key. One pure resolver
  serves both walks:
  ```csharp
  public static TOption Resolve<TOption>(FrozenDictionary<ChannelId, TOption> overrides,
      IEnumerable<ChannelId> channelAndAncestors, TOption inherit) where TOption : struct, Enum
      => channelAndAncestors.Select(id => overrides.GetValueOrDefault(id, inherit))
          .FirstOrDefault(option => !option.Equals(inherit), inherit);
  ```
  `ShouldLogMessage` and `CheckSpam` become a module check plus `Resolve`. The View commands read the same map, sorted.
  The `Set…` methods compare against the map, not a second cached read.
- **Size:** about −60 lines across 3 files, and both `try/catch` `IAsyncEnumerable` methods go. In the steady state,
  one query per guild per day replaces one per channel seen. A new thread costs no query instead of two.
- **Needs:** nothing.
- **Interacts with:** L-12 and M-12 (the Override commands' merged switch reads the map), KI-71 (the map is where
  overrides on deleted channels could be filtered out), F-1. Section 5 should cite this for the per-message lookups.

#### SET-4 Gate `GainUserXp` on an in-memory "next eligible" time per member  `perf` `state` · **high**
- **Where:** `GainUserXp.HandleEventAsync` (`Grimoire/Features/Leveling/Events/GainUserXp.cs:58-62`) and
  `_getUserXpInfoQuery` (`:27`).
- **Now:** every message from a non-ignored member in a Leveling guild opens a context and runs the compiled
  `MAX(TimeOut)` query, even though most messages fall inside the timeout and return. Only `GainUserXp` moves the
  timeout forward: `AwardUserXp` and `ReclaimUserXp` write `TimeOut = UtcNow`, and the bot is a single instance. So the
  answer is already known in process for every member who gained XP since the last restart. There's also a race. Two
  messages from one member that arrive together both read the old timeout before either inserts, and both earn XP.
- **Proposed:** a singleton `XpGate` holding `ConcurrentDictionary<(GuildId, UserId), DateTimeOffset>`.
  `TryEnter(key, now, nextEligible)` atomically claims the slot (`AddOrUpdate`, keeping the old value when it is
  still in the future) before any database work. On a miss after a restart, the compiled query seeds it. Entries are
  dead once their time passes, so the hourly tick of an existing background task prunes `value < now`. If the insert
  fails, the member skips one gain, which is the same as today's outcome when the insert throws.
- **Size:** about +30 lines (a new `XpGate.cs` and its registration), −5 in `GainUserXp`. Messages inside the timeout
  go from one database round trip to none. The `SUM` on a gain stays. Caching the total too would need invalidation in
  Award, Reclaim and the import path, so leave it out.
- **Needs:** nothing.
- **Interacts with:** X-7..X-10 (unchanged). Sections 5 and 10 should cite this rather than re-report the timeout query.

#### SET-5 Cache each guild's custom command names  `perf` · **low**
- **Where:** `TextCustomCommandProcessor.FetchCommandAsync` (`Grimoire/Features/CustomCommands/TextCustomCommandProcessor.cs:87-100`).
  Writers: `CustomCommandSettings.Learn`, `.Revert`, `.Forget` (`CustomCommandSettings.Forget.cs:44`).
- **Now:** every `!` message from a non-bot member in a Commands guild runs one indexed `CustomCommands` query. In
  guilds that also run `!`-prefixed music or utility bots, nearly all of those are misses.
- **Proposed:** cache a `FrozenSet<CustomCommandName>` of the guild's latest, non-forgotten names (one key per guild,
  one day). Query the full command only on a hit. Learn, Revert and Forget clear the key after they save. Caching only
  names keeps the entry small, and the authorisation check still runs on the fresh row.
- **Size:** about +20 lines, 4 files. A `!` message that names no command costs no query.
- **Needs:** nothing. Do it after F-10, which moves the lookup out of the `Validation` chain.
- **Interacts with:** C-6, C-9, D-8, F-10. Section 11 owns the processor itself.

#### SET-6 Identity "latest row" reads on every `GuildMemberUpdated`  `perf` · **med**
- **Where:** `UpdatedNicknameEvent.HandleEventAsync` (`Grimoire/Features/Logging/UserLogging/UpdatedNicknameEvent.cs:27-33`),
  `UpdatedAvatarEvent` (`UpdatedAvatarEvent.cs:31-37`), `UpdatedUsernameEvent` (`UpdatedUsernameEvent.cs:28-35`).
- **Now:** `GuildMemberUpdated` fires for any member change, including roles, timeouts, boosts and every reward role
  `GainUserXp` grants. In a UserLog guild, each event runs three module lookups and three database queries to find
  that the nickname, avatar and username didn't change. These rows change rarely and are written in only five places
  (the three handlers, `MemberAdded`, and the `AddMissing…HistoryAsync` bulk sync in `GuildAdded`/`UpdateAllGuilds`).
- **Proposed:** first, skip with the event's own before and after values (`NicknameBefore`/`NicknameAfter`,
  `AvatarHashBefore`/`After`, `UsernameBefore`/`After`) when DSharpPlus had the member cached, and query only when the
  "before" side is unknown. That's about −0 lines and removes nearly all of the queries. If those values turn out to
  be unreliable, cache the last-known triple per (guild, user), written by the five writers. That's about +30 lines,
  but invalidation has to be added to the bulk sync too, which is why it's the second choice.
- **Size:** first option ±0 lines, 3 files. Three queries per event drop to none in the common case.
- **Needs:** a check of what DSharpPlus 5.0.0-nightly-02614 fills in for the "before" values on an uncached member.
- **Interacts with:** L-7..L-9, M-3 (`UsernameHistory.GetDisplayNameAsync` unchanged). Section 9 owns these handlers.
  Section 5 counts the role-grant echo from `GainUserXp`.

#### SET-7 Delete `IsChannelLocked` and its cache entry  `remove` · **low**
- **Where:** `SettingsModule.IsChannelLocked` (`SettingsModule.Moderation.Locks.cs:18-35`), `CacheKey.ChannelLocks`, and
  the `RemoveAsync(CacheKey.ChannelLocks(...))` calls in `AddChannelLock` (`:73`) and `RemoveChannelLock` (`:92`).
- **Now:** no production code calls it. Channel locks are enforced by Discord permission overwrites, and only thread
  locks need a per-message check. Its only users are four tests in `Grimoire.Settings.Tests/Moderation/ChannelLockTests.cs`.
  So every channel lock and unlock clears a key that is never read.
- **Proposed:** delete it, the key and the two invalidations. The tests assert through `GetActiveChannelLockAsync`.
- **Size:** about −22 lines, 2 files plus 4 test edits.
- **Needs:** nothing.
- **Interacts with:** S-9 (another unused method), S-10.

#### SET-8 `HybridCache` used as a mutable in-process store  `state` `perf` · **low**
- **Where:** `SpamTrackerModule.CheckSpam` (`SpamTrackerModule.cs:64-104`, the `SpamTracker` record at `:107`);
  `DiscordAuditLogParserService.ParseAuditLogForDeletedMessageAsync` (`Grimoire/Utilities/DiscordAuditLogParserService.cs:68-79`).
- **Now:** `SpamTrackerModule` gets a mutable `record` from `HybridCache` and `lock`s on it. That works only because
  FusionCache without a second level returns the same reference. The `HybridCache` contract allows a copy per call,
  so adding a distributed level would reset every tracker silently. The tracker also keeps each member's whole last
  message (up to 4,000 characters) for 30 minutes, just to test for a repeat. The audit-log parser reads a count with
  `GetOrCreateAsync` and writes it with `SetAsync`. That's a read-modify-write with no atomicity, so two deletes
  handled at once can both claim the same audit entry, which is the race beside KI-64. Both pay for an async
  `ValueTask` path on a lookup that never does I/O.
- **Proposed:** keep this state out of the settings cache. For spam, use a pure `SpamScore.Next(SpamState?, MessageFacts)
  → (SpamState, bool)` over an immutable `readonly record struct SpamState(double Points, ulong LastMessageId, int
  ContentHash, DateTimeOffset At)`, kept in a `ConcurrentDictionary<(GuildId, UserId), SpamState>` and updated with an
  `AddOrUpdate` retry loop, so no `lock` is needed. Store `string.GetHashCode(content)` rather than the content. Prune
  states idle for more than 30 seconds, by which time decay has emptied them, on the message tick. For the audit log,
  use `ConcurrentDictionary<ulong, int>.AddOrUpdate(id, count, (_, old) => Math.Max(old, count))` and compare the
  returned value. With a `CheckSpamResult` that becomes `ModerationReason?` (F-7), the scorer is unit-testable without
  Discord types.
- **Size:** about ±0 lines, 2 files. No async on these paths, an atomic update, and a few bytes per tracked member
  instead of the message text.
- **Needs:** nothing.
- **Interacts with:** KI-53 (the reset stays inside `Next`), KI-64, F-7. Section 8 owns the scoring constants.

#### SET-9 A cache fill in flight across a write caches the old value for a day  `state` · **low**
- **Where:** every write that does `SaveChangesAsync` then `RemoveAsync`: `SetGuildSetting` (`SettingsModule.cs:92-94`)
  and the writers listed in SET-2. The one-day entry options are at `SettingsModule.cs:26`.
- **Now:** if a reader's factory query starts before a write commits and finishes after the `RemoveAsync`, the factory
  stores the old value, and it then lives for a day. FusionCache's stampede lock covers factories, not `Remove`. The
  window is a few milliseconds, but a module that was just enabled can read as off until the next day or the next
  write. Not reproduced. This is the general cache-aside race, and the branch review's "one-day expiry is safe" didn't
  consider it.
- **Proposed:** bound the damage instead of adding locking. With SET-1 and SET-3, each guild has about four settings
  keys, so an hour's expiry costs about four queries per guild per hour. Set `Expiration = 1 hour` once as FusionCache's
  default entry option (see SET-10) and delete the three `_cacheEntryOptions` fields.
- **Size:** about −4 lines, 3 files.
- **Needs:** nothing.
- **Interacts with:** SET-1, SET-10.

#### SET-10 Configure the cache once, and let fail-safe cover outages  `pipe` `remove` · **med**
- **Where:** `GrimoireServiceRegistration.cs:238` (`services.AddFusionCache().AsHybridCache()`, with no options), the
  per-class `HybridCacheEntryOptions` in `SettingsModule.cs:26`, `SpamTrackerModule.cs:32` and
  `DiscordAuditLogParserService.cs:27`.
- **Now:** every call passes its own entry options, and the defaults are FusionCache's (30 seconds, no fail-safe).
  During a database outage, any entry that expires stops serving and every read fails. Under F-1 that throws. Under
  S-4 it becomes `GetOrElse`.
- **Proposed:** `AddFusionCache().WithDefaultEntryOptions(o => { o.Duration = TimeSpan.FromHours(1);
  o.IsFailSafeEnabled = true; o.FailSafeMaxDuration = TimeSpan.FromDays(1); o.FactorySoftTimeout =
  TimeSpan.FromSeconds(2); }).AsHybridCache()`. An outage then serves the last good value for up to a day, and only
  guilds never seen since startup fail. That shrinks F-1's "commands show the generic error during an outage" to
  almost nothing. S-2's trap can't come back, because fail-safe reuses a value that loaded successfully and never
  caches a factory failure.
- **Size:** about +6 / −6 lines, 4 files.
- **Needs:** a spike: confirm that FusionCache 2.8's `HybridCache` adapter starts from `DefaultEntryOptions` when a
  call passes no `HybridCacheEntryOptions`, so fail-safe applies. If it doesn't, inject `IFusionCache` in
  `SettingsModule` instead (one constructor change).
- **Interacts with:** F-1 (makes it cheap to accept), S-2, S-4, SET-9.

#### SET-11 Rewards for deleted roles: one batch write, one invalidation  `perf` · **low**
- **Where:** `GainUserXp.RemoveRewardsForDeletedRolesAsync` (`GainUserXp.cs:186-200`), which calls
  `SettingsModule.SetRewardAsync` (`SettingsModule.Leveling.Rewards.cs:41`) once per role.
- **Now:** N orphaned rewards cost N contexts, N saves and N cache removals. Between removals, a concurrent message in
  the same guild can reload the half-updated set, still see orphans and write duplicate `RewardRemoved` rows. Those
  are harmless to the latest-per-role read, but they're noise.
- **Proposed:** `SetRewardsAsync(IReadOnlyCollection<Reward>)` through SET-2's `AppendRangeAsync`: one save and one
  removal. The single-reward method becomes a one-element call.
- **Size:** about −4 lines, 2 files.
- **Needs:** SET-2 (or nothing, written by hand).
- **Interacts with:** X-2. Section 10 owns the rest of `ApplyRewards`.

#### SET-12 Remaining `_` arms in Settings  `cs15` `state` · **low**
- **Where:** `IsMessageIgnored`/`IsMemberIgnored` (`SettingsModule.Leveling.Ignores.cs:31`, `:47`, over
  `XpIgnoredItem`); `Key` (`SettingsModule.Moderation.DiagnosticsIgnores.cs:103-108`, which throws
  `ArgumentOutOfRangeException` over `DiagnosticsIgnoredItem`); `CreateLevelingSetting` (`Levelsettings.cs:81`, an
  enum).
- **Now:** T-4 counts the two Ignores arms. The Diagnostics `Key` arm throws `ArgumentOutOfRangeException`, not
  `InvariantViolationException`, so the 45-count and the S-10/S-26 ruling ("every `UnreachableException` is
  `InvariantViolationException`") both missed it. The AutoPardon and `ToCachedSetting` arms go with SET-1 on net10.
- **Proposed:** after `net11.0`, with `closed` on `XpIgnoredItem` and `DiagnosticsIgnoredItem` (section 2's call),
  drop all three arms. Until then, make `Key` throw `InvariantViolationException` like the rest. The enum arm in
  `CreateLevelingSetting` stays (T-4).
- **Size:** about −3 lines, 2 files.
- **Needs:** `net11.0`, section 2's closed decision, F-11.
- **Interacts with:** S-10, the `InvariantViolationException` ruling (rank 26), T-4.

## Is the `SettingsModule` layout the right size?

**Verdict: keep one class, and cut what's inside the methods.** Most of the length is the per-method scaffold, not the
number of files: 12 partial files, 1,265 lines, about 44 async members (38 public). Splitting it into per-feature
services would add DI registrations and constructor parameters, and remove no code. Feature folders already map one
partial each. A generic `Setting<T>` per `GuildSettingType` was considered and rejected. The 24 types parse into six
shapes (bool, channel, role, duration, four level values), and SET-1's record says that in one place with names. A
generic key object would bring the stringly typed lookup back. The two pairs that are truly the same shape (log and
spam overrides, channel and thread locks) are handled by SET-3's resolver and S-5/S-10.

| File today | Lines | After SET-1/2/3/7 (and F-1) |
|---|---|---|
| `SettingsModule.cs` | 178 | ~90: constructor, the two helpers, `SetGuildSetting`, `ApplyGuildSetting` |
| `.LogChannels`, `.ModuleEnabled`, `.Moderation.AutoPardon`, `.Leveling.Levelsettings`, mute-role half of `.Moderation.Mutes` | 34 + 75 + 47 + 83 + ~40 | one `SettingsModule.GuildSettings.cs` ~110, plus `GuildConfig` ~45 |
| `.MessageLog.ChannelOverrides`, `.Moderation.SpamFilterOverrides` | 126 + 101 | ~120 for both, in one `.ChannelOverrides.cs` |
| `.Leveling.Ignores`, `.Leveling.Rewards`, `.Moderation.DiagnosticsIgnores` | 106 + 52 + 109 | ~180 |
| `.Moderation.Locks` | 202 | ~120 (with S-5, S-10) |
| `.Moderation.Mutes` (rest) | ~112 | ~80 |
| **Total** | **1,265 in 12 files** | **about 865 in 8 files, about −400** (about −250 without F-1's catches) |

## Checked, no finding

- **Invalidation after every write.** Rewards, XP ignores, Diagnostics ignores, thread locks and both overrides clear
  or replace their key after `SaveChangesAsync`, including the multi-row `AppendIgnoredItemsEvent` and the Diagnostics
  pair (one save, one removal). The only gaps are KI-103's `NotModified` paths and the leveling key in SET-1.
- **Thread-lock enforcement.** `IsThreadLocked`'s per-guild set is cleared by both writers, and the 5-second expiry task
  goes through `RemoveThreadLock`, so an expired lock stops being enforced on the next tick.
- **Mutes.** These aren't cached and don't need to be. `GetActiveMute` runs on member join and in `/Unmute`, and the
  expiry polls are S-13's business.
- **Key collisions.** Override keys hold only a channel id. Snowflakes are global, so two guilds can't collide.
- **Memory.** FusionCache's memory cache has no size limit. Per-guild keys keep it at a few entries per guild. After
  SET-3 and SET-8, nothing grows with channel or member count.
- **Commands.** `RequireModuleEnabledCheck` and `GuildLogPublisher` read only cached settings. The publisher's second
  `GetLogChannelSetting` in its `UnauthorizedException` arm (`GuildLogPublisher.cs:87`) is a cache hit and runs only on
  failure.

## Questions for the owner

1. **F-1 against S-4 (section 1, question 1).** SET-1 and SET-2 work either way, but their signatures follow your
   answer. SET-10's fail-safe makes F-1 much cheaper to accept, because an outage keeps serving cached settings for a
   day instead of failing commands. Does that change your answer?
2. **SET-4's gate.** It holds XP timeouts in memory, so it relies on the bot staying a single instance, which the
   cache already assumes. It also closes the double-XP race for messages that arrive together. Is that fine?
3. **SET-6.** May the user-log handlers trust DSharpPlus's "before" values when the member was cached, or do you want
   the database to stay the only source of truth for "did it change"?
4. **SET-9/SET-10.** Is a one-hour expiry for settings acceptable? It bounds a stale entry to an hour, at about four
   queries per guild per hour.
5. **SET-5.** Is caching custom command names worth 20 lines? It only pays in guilds where people type other bots'
   `!` commands.
