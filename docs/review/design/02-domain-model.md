# Section 2: Domain model

Reviewed 2026-11-09, against the code as it will be once every fix in `review-decisions.md` is in. Scope:
`Grimoire.Domain` (except the section 1 files) and `Grimoire.Settings/Domain`, about 2,700 lines, 392 of them in
`Obsolete/`.

The biggest structure that doesn't pay for itself is the append-only event log behind every Settings table. Nothing
reads the history. Every read has to filter for the latest row, and tombstone types (`*Unlocked`, `*Removed`,
`Watched*`, `Inherit`, `GuildSettingDefault`) exist only to mark that latest row as "off". Making these plain state
tables (D-10) removes about 250 lines and a whole class of latest-row bugs, and now is the cheapest time to do it,
because the Settings migrations haven't run in production. Next: once S-12 is in, most Settings factories have no rule
left, so they collapse into positional records (D-14, about −300 lines). The obsolete legacy types can leave the EF
model now while their tables stay (D-20, about −850 lines). `closed` on the 13 hierarchies removes 5 throws and 4
silent `_` arms (D-23). The strongly typed ids pay for themselves and stay. The findings are numbered from **D-10** so
they don't collide with branch-review D-1 to D-9, which section 1 and `review-decisions.md` cite.

## Verdict per structure

| Structure | Verdict | Finding |
|---|---|---|
| Strongly typed ids (`StronglyTypedIds.cs`, 9 types, 61 lines) | **Keep.** `ModeratorId` and `UserId` appear side by side in `Sin.ForX`, `MuteAdded.Create` and `AwardedXp.Create`, where swapping them is the likely bug. Conversions are rare: 46 `new XId(` sites across 166 files, almost all at the Discord boundary through the `GetXId()` extensions. P-4 already made the EF cost one convention. `AttachmentId` and `EmojiId` are barely used, but they cost 4 lines each | none |
| `ModerationActor` | **Keep, extend.** It's the right type, but Settings records "the bot did it" as a `ModeratorId` instead | D-16, D-23 |
| `MessageHistoryEntry` TPH (4 cases) | **Keep.** `Content` exists only on the content cases, which is exactly what the type should say. The delete rows are never read, but they cost one insert per delete, and KI-51 shows that you value `DeletedByModerator` | D-19 (its `GuildId`) |
| `XpHistoryEntry` TPH (4 cases) | **Keep the hierarchy, drop the read side.** Production code never materialises a row. It only sums `RawXp` and takes `Max(TimeOut)`. The typed `Xp` getters and `NegativeXpAmount` are dead | D-17 |
| `CustomCommand` Text/Embed and `CustomCommandRole` Allow/Deny | **Keep.** Versioning is read by `/Commands History` and Revert, so this history earns its place. Allow/Deny as types costs the same as an enum | D-23 |
| `Sin` + `SinReasonHistory` + `Pardon` histories | **Question.** Read only as "latest". Two readers combine fields from different rows | D-12 |
| Settings event logs (locks, mutes, rewards, ignores, overrides, guild settings) | **Doesn't pay.** Nobody reads the history | D-10, D-11 |
| `GuildSetting` (string value per type) | Keep the EAV shape for now. See question 3 | D-10 |
| `Obsolete/` (18 types, 392 lines, plus 454 configuration lines) | **Remove from the model now**, drop the tables later as P-7 decided | D-20 |
| Trusted string wrappers (`Username`, `AvatarFileName`, `InviteCode`, `InviteUrl`, `AttachmentUrl`) | Your call in D-6. Not reopened | none |
| `GuildModuleState` | **Remove** | D-21 |

## Findings

#### D-10 Settings tables are event logs that nobody reads  `remove` `state` `perf` · **high**
- **Where:** every Settings table except `DiagnosticsIgnoredItems`.
  - Tombstone types: `ChannelUnlocked` (`ChannelLock.cs:72`), `ThreadUnlocked` (`ThreadLock.cs:58`), `MuteRemoved`
    (`Mute.cs:74`), `RewardRemoved` (`Reward.cs:48`), `WatchedChannel`, `WatchedMember` and `WatchedRole`
    (`XpIgnoredItem.cs:43,85,127`), `GuildSettingDefault` (`GuildSetting.cs:18`), and the `Inherit` value of both
    override enums (`MessageLogChannelOverride.cs:14`, `SpamFilterOverride.cs:14`).
  - Latest-row readers: `ActiveChannelLocks` and `ActiveThreadLocks` (`SettingsModule.Moderation.Locks.cs:190-201`),
    the mute equivalent, `GetLevelingRewardsAsync`'s `GroupBy(RoleId)…First()` (`SettingsModule.Leveling.Rewards.cs:28-35`),
    `GetAllIgnoredItems`' `GroupBy(Id)` (`SettingsModule.Leveling.Ignores.cs:60-70`), `GetGuildSetting` and
    `GetGuildSettings` (`SettingsModule.cs:40-66`), and the `!= Inherit` filters (`SettingsModule.MessageLog.ChannelOverrides.cs:123`,
    `SettingsModule.Moderation.SpamFilterOverrides.cs:67`).
  - `RewardEntry` (`RewardEntry.cs:12`) exists only because `RewardAdded` carries audit fields.
- **Now:** every change appends a row, and "off" is a row too. I searched `Grimoire` for every read of `SetBy`,
  `SetAt` and `ModeratorId` on these records. Outside the latest-row selection, none exist. The only history the bot
  shows is custom command versions, which live in `GrimoireDbContext`. The cost shows up across the codebase:
  - S-5 found the latest-event filter written 13 times.
  - S-13 found that the 5-second expiry polls scan every lock and mute ever made.
  - KI-103 found that a "not modified" write never clears a stale cache.
  - P-5 and S-8 had to drop the `SetAt` indexes.
  - There are two ways to say "no override": no row, or an `Inherit` row.

  The audit trail people actually read is the mod log, which gets a post for each of these changes.
- **Proposed:** plain state tables, keyed without `SetAt`, holding the current value and who last set it. "Off" deletes
  the row, as `RemoveDiagnosticsIgnoredItems` already does (`SettingsModule.Moderation.DiagnosticsIgnores.cs:95`) and
  as `main` did for locks and mutes. Each hierarchy becomes one sealed record: `ChannelLock`, `ThreadLock`, `Mute`,
  `Reward` (which replaces `RewardEntry`), `IgnoredChannel`, `IgnoredMember` and `IgnoredRole` (D-11), and the two
  overrides without `Inherit`. A set becomes an upsert, an unset becomes `ExecuteDeleteAsync`, and a read becomes a
  plain `Where`. `GuildSetting` keeps `Disabled` and `CustomValue`, keyed `(GuildId, Type)`. Reset-to-default (S-9's
  reason for keeping `GuildSettingDefault`) becomes a delete.
- **Size:** I estimated this without writing it out. In `Grimoire.Settings/Domain`, about −150 lines on top of D-14
  (8 tombstone types and 5 abstract bases, plus `RewardEntry`). In `SettingsModule`, about −80 (5 `GroupBy`/anti-join
  readers become `Where`, and the redundant-write checks become an upsert). S-13 goes away entirely. Plus one
  data migration per table: keep the latest row per key, drop tombstones, and change the primary keys.
- **Needs:** an owner decision (`migration-strategy.md` says "Keep the append-only history behavior"), a Settings
  migration (cheap now: Settings' `Initial` hasn't run in production, per P-7), and D-14 first, so the types are
  already small.
- **Interacts with:** S-5, S-8, S-9, S-13, P-5, KI-103, X-12 (no-ops become "row already there"), S-10, F-9 (with
  rows deleted on expiry, "already done" is just "no row"). Section 4 (`CachedSetting` mirrors `GuildSetting`'s three
  cases).

#### D-11 The XP ignore list should store rows like the diagnostics ignore list  `remove` `state` · **med**
- **Where:** `Grimoire.Settings/Domain/XpIgnoredItem.cs` (146 lines, 6 types under 3 abstract levels),
  `XpTrackedItemsConfiguration.cs` (discriminator with 6 values, key `(GuildId, Id, SetAt)`),
  `SettingsModule.Leveling.Ignores.cs:60-70`, and `IgnoreCommandGroup.BuildIgnoreList` (`IgnoreCommandGroup.cs:72-82`,
  6 arms that are 3 pairs).
- **Now:** the two ignore lists in Settings are built two different ways. `DiagnosticsIgnoredItem` has 2 types, and
  removing an item deletes its row. `XpTrackedItem` adds a `Watched*` row to un-ignore, then
  `GetAllIgnoredItems` groups by `Id`, takes the newest row and filters with `OfType<XpIgnoredItem>()`. The `Watched*`
  types are only ever written, and only so the reader can throw them away.
- **Proposed:** do it the diagnostics way. Keep `XpIgnoredItem` with `IgnoredChannel`, `IgnoredMember` and
  `IgnoredRole`, keyed `(GuildId, Id)`. `/Ignore Remove` becomes `ExecuteDeleteAsync`. Delete `XpTrackedItem`,
  `XpWatchedItem` and the three `Watched*` types. `BuildIgnoreList` keeps 3 arms. X-12's "report no-ops" becomes a
  check for whether the row is already there.
- **Size:** about −110 lines in `XpIgnoredItem.cs` (about −45 on top of D-14), −4 in the reader, −3 in the
  configuration, −3 arms, and about +6 for the delete. Five files plus a migration that keeps the newest row per
  `(GuildId, Id)` if it's an `Ignored*` and deletes the rest.
- **Needs:** a Settings migration. This is part of D-10. If you decline D-10, this one still stands on its own.
- **Interacts with:** X-12, KI-73, D-23.

#### D-12 Sin reason and pardon histories are read only as "latest", and two readers mix rows  `remove` `state` · **med**
- **Where:**
  - `Sin.ReasonHistory`/`SinReasonHistory.cs`, `Sin.Pardons`/`Pardon.cs`.
  - Readers: `SinLog.cs:107-125`, `PublishCommands.cs:51-55` and `PublishCommands.Unban.cs:35-46`.
  - Writers: `PardonSin.cs:56`, `UpdateSinReason.cs:57` and `Sin.Create` (`Sin.cs:74-76`).
- **Now:** every reader takes the newest reason with a correlated subquery. No command shows older reasons or older
  pardons. Pardons became a history in `ModelRedesign` step 4 (on `main` the key was `SinId`), and `/Pardon` doesn't
  refuse a sin that's already pardoned. So a second `/Pardon` adds a row, and then:
  - `/SinLog` shows "Pardoned by" the **newest** pardon's moderator, but "on" the **oldest** pardon's date
    (`SinLog.cs:116-125`).
  - `/Publish Unban` shows the newest pardon's reason with the oldest pardon's date (`PublishCommands.Unban.cs:35-46`).

  These are three correlated subqueries per sin for one row's worth of data.
- **Proposed:** pick one of these.
  - **(a) Keep the histories, fix the readers.** `/Pardon` refuses a sin that's already pardoned, so `Pardon` really
    is one row (key `SinId`), and the two readers project from that single row. About −8 lines, no migration.
  - **(b) Make them columns.** `Sin.Reason` (`ModerationReason?`) and a 1:1 `Pardon` keyed by `SinId`. `/Reason`
    becomes an `ExecuteUpdate`. `SinReasonHistory` goes (type, configuration, DbSet), along with D-2's history block
    in `Sin.Create`, the `SinId = default` placeholder and 4 correlated subqueries. About −70 lines across 8 files plus
    a migration. You lose the record of who changed a reason, and when. Only the database has it today, and the
    `/Reason` mod-log post already records it.
- **Size:** (a) about −8 lines in 3 files. (b) about −70 lines in 8 files, plus a `GrimoireDbContext` migration.
- **Needs:** an owner decision (question 2). (b) also needs a migration. `ModelRedesign` isn't deployed, so it can
  still be edited instead.
- **Interacts with:** D-2 (b undoes its history block), P-5 (`IX_SinReasonHistory_*` and `IX_Pardons_*` go with b),
  M-3 and M-11 (the shared "find the sin" path).

#### D-13 Extend S-12 to `Grimoire.Domain`, so `Sin.ForX` and `AwardedXp.Create` return the type  `state` `remove` · **med**
- **Where:**
  - Zero-id checks: `Sin.Create` (`Sin.cs:57-65`), `AwardedXp.Create` (`XpHistory.cs:90-92`), `MuteAdded.Create`'s
    `sinId.Value <= 0` (`Mute.cs:66-68`) and `ReactionEmoji.Custom.Create`'s `(0, _)` arm (`Reaction.cs:74-75`, which
    `ToReactionEmoji` can't reach, because it sends id 0 to `Standard`).
  - Callers that unwrap: `Warn.cs:52`, `KickUser.cs:57`, `MuteUser.cs:89`, `SpamEvents.cs:61` and
    `BanAddedEvent.cs:71`, each with `.Match(s => s, _ => throw new InvariantViolationException("Discord ids are never zero."))`.
- **Now:** S-12 removes the zero-id checks from the Settings factories, but its text names only Settings. D-2's note
  says "each caller keeps one throw, for ids that can't be zero". So after the decided fixes, these 5 throws remain.
  Section 1's Warn sketch and T-4 already assume they're gone.
- **Proposed:** apply S-12's rule to these four factories too. `Sin.ForWarn(...) : Sin`,
  `AwardedXp.Create(...) : AwardedXp`. `Custom.Create` keeps only its name rule. `MuteAdded` keeps its time and
  self-mute rules.
- **Size:** about −12 lines in `Sin.cs`, −4 in `XpHistory.cs`, −3 in `Mute.cs`, −2 in `Reaction.cs`, and −1 throw
  at each of the 5 callers. About −25 lines and 5 throws in 9 files.
- **Needs:** nothing.
- **Interacts with:** S-12, D-2, D-5, F-7 and T-4, which counted these as removed.

#### D-14 After S-12, factories with no rule left become positional records  `remove` · **med**
- **Where:**
  - `ChannelUnlocked.Create` (`ChannelLock.cs:84`) and `ThreadUnlocked.Create` (`ThreadLock.cs:70`).
  - `MuteRemoved.Create` (`Mute.cs:85`) and `RewardRemoved.Create` (`Reward.cs:59`).
  - `MessageLogChannelOverride.Create` (`MessageLogChannelOverride.cs:39`) and `SpamFilterOverride.Create`
    (`SpamFilterOverride.cs:39`).
  - The six `XpIgnoredItem.cs` factories, the two `DiagnosticsIgnoredItem.cs` factories, and
    `GuildSettingDisabled`/`GuildSettingDefault.Create` (`GuildSetting.cs:25,45`).
- **Now:** each one is a private constructor plus a static `Create` returning `Validation<T>`. Once S-12 deletes the
  zero checks, there's nothing left to fail. Following D-5's rule, they would then return `T`. That still leaves a
  private constructor and a forwarding `Create` of 10 to 20 lines, which is a constructor written twice.
- **Proposed:** where no rule remains, the type is its constructor. Where a rule remains (`ChannelLocked`,
  `ThreadLocked` and `MuteAdded`: end after set, no self-mute; `RewardAdded`: level ≥ 0; `GuildSettingCustomValue`:
  value length), keep the private constructor and a `Create` that returns `Result<T>`. Make the leftover
  `Validation` handling at the callers go too: `IgnoreCommandGroup.cs:50-55`, `DiagnosticsCommands.cs:129-141`, and
  the `.Map(XpTrackedItem (x) => x)` casts in Ignore Add/Remove.
  ```csharp
  public abstract record ChannelLock(ModeratorId ModeratorId, ChannelId ChannelId, GuildId GuildId,
      DateTimeOffset SetAt, ModerationReason? Reason);

  public sealed record ChannelUnlocked(ModeratorId ModeratorId, ChannelId ChannelId, GuildId GuildId,
      DateTimeOffset SetAt, ModerationReason? Reason = null) : ChannelLock(ModeratorId, ChannelId, GuildId, SetAt, Reason);

  public sealed record SpamFilterOverride(SpamFilterOverrideOption ChannelOption, ChannelId ChannelId,
      GuildId GuildId, ModeratorId SetBy, DateTimeOffset SetAt);
  ```
  EF binds positional records through their constructors. The P-4 note ("get-only settings properties keep a bare
  `Property(...)`") already covers this mapping.
- **Size:** estimated. `Grimoire.Settings/Domain` goes from about 885 lines (excluding `Values/` and
  `LevelingSettingEntry`) to about 300. S-12 alone accounts for about −280 of that, and this finding for about −300
  more. About −15 lines at callers. 9 domain files plus about 8 callers.
- **Needs:** S-12. Do this before D-10, which then has less to delete.
- **Interacts with:** S-12, D-5, S-10 (its public add/remove methods take these types), F-2 (the `Validation` type
  goes either way).

#### D-15 Entities with factories can still be built with object initializers  `state` · **low**
- **Where:** `Sin` (`Sin.cs:21`, public `required init` on every member), `EarnedXp`, `AwardedXp`, `ReclaimedXp` and
  `MigratedXp` (`XpHistory.cs:72-123`: `RawXp` is `internal init` and not `required`, so `new EarnedXp { UserId = …,
  GuildId = …, TimeOut = … }` compiles in `Grimoire` and stores an `Xp` of 0), and `TextCustomCommand` and
  `EmbedCustomCommand` (`CustomCommand.cs:111,131`).
- **Now:** the factories carry real rules: the XP sign by subtype, the reason history's actor and time matching the
  sin's, and role rows sharing the command's name, guild and time. But any assembly can skip them. Nothing does today,
  so this is a principle 10 gap, not a bug.
- **Proposed:** add `private X() { }` to each type that has a factory. EF materialises through private parameterless
  constructors, `with` still works, and the factories' own initializers still compile. Leave plain `required init`
  records (such as `Message`, `Attachment` and `Pardon`) alone. They have no rule beyond "every field is set", and
  `required` already enforces that.
- **Size:** about +8 lines, 3 files. No test builds these with an initializer.
- **Needs:** a check that EF still materialises them (the integration tests cover all four XP types and both command
  types).
- **Interacts with:** S-12's note on `RewardAdded`'s public positional constructor, which D-14 leaves public on
  purpose only if its level rule moves to the command's `[MinMaxValue(0, …)]`.

#### D-16 "The bot did it" is recorded two ways  `state` · **low**
- **Where:**
  - `Sin.Actor` uses `ModerationActor.System`, but the Settings records use `ModeratorId`, so the bot's own user id
    stands in for "the system".
  - That id is spelled three ways: `new ModeratorId(args.Guild.CurrentMember.Id)` (`SpamEvents.cs:66`),
    `new ModeratorId(discordClient.CurrentUser.Id)` (`MuteBackgroundTasks.cs:140`), `GetGrimoireModeratorId()`
    (`LockBackgroundTasks.cs:128,141`, `DiscordClientExtensions.cs:87`), plus the X-2 reward removal
    (`GainUserXp.cs:191`).
- **Now:** a spam mute writes a `Sin` with `System` and a `MuteAdded` with the bot as moderator, so one act is stored
  two ways. Anything that mentions the moderator shows "System" for the sin and a bot mention for the mute.
- **Proposed:** `ModerationActor` on `ChannelLock`, `ThreadLock`, `Mute` and `Reward` (`ModeratorId`/`SetBy`), using
  the existing `ModerationActorConverter` (a nullable column, with `System` stored as null). `MuteAdded`'s self-mute
  rule becomes `actor is Moderator(var id) && id.Value == userId.Value`. Delete `GetGrimoireModeratorId`.
- **Size:** about ±0 lines, about 10 files, plus a Settings migration (columns become nullable; existing bot-id rows
  stay readable as `Moderator`).
- **Needs:** a Settings migration (fold it into D-10's if you take that one).
- **Interacts with:** P-6 (the shared converter), X-2, D-10.

#### D-17 Nothing reads `XpHistory` rows by type: drop the typed getters and `NegativeXpAmount`  `remove` · **low**
- **Where:** the `Xp` getters on `EarnedXp`, `AwardedXp`, `ReclaimedXp` and `MigratedXp` (`XpHistory.cs:74,84,108,122`),
  `PositiveXpAmount.FromDatabase` and `NegativeXpAmount.FromDatabase` (`:23,:45`), `NegativeXpAmount` (`:35-54`), and
  its one caller, `ReclaimUserXp.cs:71`.
- **Now:** production code sums `RawXp` and takes `Max(TimeOut)` (`GainUserXp.cs:27-31`, `ReclaimUserXp.cs:64`). It
  never materialises an entry, so the typed getters are read only by 5 test assertions. `NegativeXpAmount` exists so
  `ReclaimedXp.Create` can take a negative number. F-7 then proposes `PositiveXpAmount.Negate()` to produce one.
- **Proposed:** `ReclaimedXp.Create(PositiveXpAmount taken, …)` stores `-taken.Value`, and `NegativeXpAmount` goes.
  With F-7's `FetchXpToTakeAsync` returning `Result<PositiveXpAmount>`, no conversion is needed. Delete the four
  getters and both `FromDatabase` methods. The tests read `RawXp`.
- **Size:** about −32 lines in `XpHistory.cs`, −2 in `ReclaimUserXp.cs`, and 5 test lines.
- **Needs:** nothing.
- **Interacts with:** F-7 (replaces its `Negate()`), P-2 (`ReclaimedXp` gains its moderator in the same edit).

#### D-18 `ReactionEmoji` revalidates stored values and throws  `state` · **low**
- **Where:** `ReactionEmoji.Custom.ParseStored` and `Standard.ParseStored` (`Reaction.cs:82-89`, `:119-122`), called
  from `ReactionConfiguration.cs:20`.
- **Now:** a read reruns `Create` and throws `ArgumentException` on failure. That's the trap D-1 and S-1 removed
  everywhere else: one row that fails today's rules breaks every query that loads it. Nothing materialises reactions
  yet, but the features you plan will. Discord's emoji rules have changed before.
- **Proposed:** private trusted constructors for both cases, with `ParseStored` calling them directly, following the
  "stored data is trusted" rule in `CustomCommand.cs:57`.
- **Size:** about −6 lines and 2 throws, 1 file.
- **Needs:** nothing.
- **Interacts with:** D-1, S-1.

#### D-19 Two `GuildId` columns are written and never read  `remove` `perf` · **low**
- **Where:** `MessageHistoryEntry.GuildId` (`MessageHistory.cs:16`, set at `AddMessageEvent.cs:67`,
  `UpdateMessageEvent.cs:65`, `DeleteMessageEvent.cs:123,130` and `BulkMessageDeletedEvent.cs:68`) and
  `Pardon.GuildId` (`Pardon.cs:16`, set at `PardonSin.cs:59`).
- **Now:** both rows hang off a parent that has the guild (`Message.GuildId`, `Sin.GuildId`) through a required
  foreign key. Neither column is indexed, filtered on or projected. `MessageHistory` is the biggest table and gets a
  row on every logged message.
- **Proposed:** drop both properties and columns. Leave `Reaction.GuildId`: the planned reaction features may want
  per-guild queries without a join (question 5).
- **Size:** about −8 lines, 7 files, plus a `GrimoireDbContext` migration.
- **Needs:** a migration.
- **Interacts with:** D-12 (option b rewrites `Pardon` anyway).

#### D-20 Take the obsolete types out of the EF model now; drop the tables later  `remove` · **med**
- **Where:** `Grimoire.Domain/Obsolete/` (18 types and `IModule`, 392 lines), `Grimoire.Data/Configuration/Obsolete/`
  (17 files, 454 lines per P-7), and the 18 `[Obsolete]` DbSets in `GrimoireDbContext.cs:29-111`, including
  `using Lock = Grimoire.Domain.Obsolete.Lock` (`:11`) and the `IgnoredChannel` name clash with
  `Grimoire.Settings.Domain.IgnoredChannel` (the using-alias problem in project memory).
- **Now:** P-7 rightly says not to drop the tables until Settings' `Initial` has run in production. But nothing in
  `Grimoire` or the tests reads these DbSets. The types exist only so EF's model includes the tables. `Initial` copies
  from them with raw SQL behind `IF EXISTS`, which doesn't need the model.
- **Proposed:** delete the types, configurations and DbSets now. Generate the migration, then empty its `Up`/`Down`,
  so only the snapshot forgets the tables and the database keeps them. In the later release P-7 describes, ship
  `DROP TABLE IF EXISTS` as raw SQL. Before doing this, check that no foreign key from a legacy table still points at
  a live table (`DropOrphanedLegacyForeignKeys` should have removed them all). The drift check in project memory will
  then show these tables as expected extras.
- **Size:** about −850 lines across 36 files, plus one near-empty migration.
- **Needs:** owner agreement that this keeps to P-7's intent (question 4).
- **Interacts with:** P-7.

#### D-21 `GuildModuleState` exists for one embed  `remove` · **low**
- **Where:** `Grimoire.Settings/Domain/GuildModuleState.cs` (6 `bool`s), `SettingsModule.GetAllModuleState`
  (`SettingsModule.ModuleEnabled.cs:52-73`, the only caller of the 6-argument `Result.WhenAll`), and
  `ModuleCommands.View.cs:24-38`.
- **Now:** a record with six named booleans is built from six reads, then printed line by line. Adding a module means
  editing all three.
- **Proposed:** `/Modules View` maps over the `Module` enum (skipping `General`) with `Task.WhenAll` of
  `IsModuleEnabled`, and prints each module's display name. Delete the record and `GetAllModuleState`.
- **Size:** about −40 lines, 3 files (one Settings test goes).
- **Needs:** F-1, if `IsModuleEnabled` is still to return a plain `bool`. It works either way.
- **Interacts with:** F-1 (which already removes `Result.WhenAll`).

#### D-22 `Nickname.Create` is dead  `remove` · **optional**
- **Where:** `Nickname.Create` (`NicknameHistory.cs:32-40`). The bot calls only `CreateIfNotEmpty` (`DiscordMemberExtensions.cs:21`,
  `UpdatedNicknameEvent.cs:35`) and `FromDatabase`.
- **Now:** a validating factory with a 32-character limit that nothing calls. D-6 changed the other trusted wrappers
  but left this one out.
- **Proposed:** delete it. `CreateIfNotEmpty` is already the trusted factory for Discord data.
- **Size:** −9 lines, 1 file.
- **Needs:** nothing.
- **Interacts with:** D-6.

#### D-23 `closed` on every domain hierarchy  `cs15` `state` · **med**
- **Where:** the table below. Following T-3's rule, every one is `closed`. We own every case, and the cases share
  state through the base. None needs a `union`. The only `_` arms over types we don't own are the `SnowflakeObject`
  switches in Ignore Add/Remove (`IgnoreCommandGroup.AddIgnore.cs:33`, `.RemoveIgnore.cs:33`) and
  `DiagnosticsCommands.cs:127`. DSharpPlus picks those parameter types, so they stay.
- **Now:** 13 public `abstract record` bases in scope. The arms below hide the missing case or throw.
- **Proposed:**

  | Hierarchy | Declared at | Change | Arms that go |
  |---|---|---|---|
  | `ModerationActor` | `ModerationActor.cs:10` | `closed` | `UserExtensions.Mention` throw (`UserExtensions.cs:27`); `ModerationActorConverter.ToColumn`'s `_ => null` becomes `ModerationActor.System => null` (`ModerationActorConverter.cs:26`) |
  | `ReactionEmoji` | `Reaction.cs:39` | `closed` replaces the private constructor (−3 lines) | none today |
  | `MessageHistoryEntry`, `MessageHistoryContentEntry` | `MessageHistory.cs:13,21` | `closed` at **both** levels (it isn't transitive) | none (readers use `OfType`) |
  | `XpHistoryEntry` | `XpHistory.cs:57` | `closed` | none in production; `IgnoreAndAwardTests.cs:241` gets exhaustiveness |
  | `CustomCommand`, `CustomCommandRole` | `CustomCommand.cs:14`, `CustomCommandRole.cs:13` | `closed` | none (D-8 took the switch) |
  | `ChannelLock`, `ThreadLock`, `Mute`, `Reward` | Settings `Domain/*.cs:10-12` | `closed` (or gone with D-10) | none (S-10 took the dispatchers) |
  | `XpTrackedItem`, `XpIgnoredItem`, `XpWatchedItem` | `XpIgnoredItem.cs:10,18,20` | `closed` at all three levels, or one `closed XpIgnoredItem` after D-11 | `IsMessageIgnored` and `IsMemberIgnored` throws (`SettingsModule.Leveling.Ignores.cs:31,47`); `BuildIgnoreList` throw (`IgnoreCommandGroup.cs:81`) |
  | `DiagnosticsIgnoredItem` | `DiagnosticsIgnoredItem.cs:14` | `closed` | `Key`'s `ArgumentOutOfRangeException` (`SettingsModule.Moderation.DiagnosticsIgnores.cs:107`); `ReportAsync`'s silent `_ => string.Empty` (`DiagnosticsCommands.cs:158`) |
  | `GuildSetting` (internal) | `GuildSetting.cs:12` | `closed` | `ToCachedSetting`'s `_ => new CachedDefaultSetting()` becomes `GuildSettingDefault or null =>` (`SettingsModule.cs:75`); `GetLevelingSettingsCacheEntry`'s `_ => null` becomes `GuildSettingDisabled or GuildSettingDefault => null` (`SettingsModule.Leveling.Levelsettings.cs:47`). `IsRedundantWrite`'s tuple `_ => false` (`:125`) stays |

  For records with a primary constructor, `closed` goes in the same place as `abstract`:
  `public closed record ChannelLock(ModeratorId ModeratorId, …);`. Drop `abstract`, because `closed` implies it and
  using both is an error. Leaves stay `sealed`. All subtypes are public, except `GuildSetting`'s, which are switched
  on only inside `Grimoire.Settings`, so every switch can see every case.
- **Size:** 13 modifier changes, 5 throws and 4 silent arms removed, and −3 lines in `Reaction.cs`. About −12 lines
  in 9 files. With D-11, two of the throws are already gone.
- **Needs:** `net11.0`, F-11 (CS8509 as an error, or a missing case only warns), and section 3's EF spike on `closed`
  TPH bases (the compiler adds `[CompilerFeatureRequired]` to their constructors, and EF instantiates the leaves, not
  the base).
- **Interacts with:** T-3 and T-4 (their "cs15 takes the other 4" count; I found two more throws and four silent
  arms), S-10, D-8, D-11, section 4 (`CachedSetting`, the `AutoPardon` arm).

## Checked, no finding

- **Invalid states still reachable**, apart from D-15 and D-16. `Message` can in principle have both
  `ProxiedMessageLink` and `OriginalMessageLink` set. These are EF navigations from separate foreign keys, so the
  type can't stop it, and only `DeleteMessageEvent` writes links. `Reaction.RemovedAt` as a nullable column with
  `IsRemoved` is the right shape for `ExecuteUpdate`, and the soft delete was your choice. `MuteAdded.MuteRoleId?` and
  `CustomCommand.ModeratorId?` are nullable only for legacy rows, and their comments say so.
- **`XpHistoryEntry.TimeOut`** is the cooldown for `EarnedXp`, but `AwardUserXp` and `ReclaimUserXp` set it to "now",
  so for those rows it's a timestamp. It's harmless, because `Max(TimeOut)` is only compared with now, and
  `DomainReview.md` #23 settled the name. Not worth a migration.
- **Value types in `Settings/Domain/Values`** are lean after S-1 and S-7. The three integer types could share a
  generic base, but that would save about 20 lines at the cost of `static abstract` noise.
- **`LevelingSettingEntry`** is pure and well-commented since X-9.
- **Principle 9:** `Grimoire.Domain` references only `JetBrains.Annotations`, and `InternalsVisibleTo` lists only
  Data, Settings and the tests.

## Questions for the owner

1. **D-10.** Should Settings stop being append-only? `migration-strategy.md` says to keep the event history, but
   nothing reads it, and the mod log already holds the trail people read. If you want "who set this" answerable,
   each state row keeps its last `SetBy`/`SetAt`. Whatever you choose, now is the cheap time, before Settings'
   `Initial` runs in production.
2. **D-12.** For sin reasons and pardons, do you want (a) the histories kept with readers that use one row and
   `/Pardon` refusing a pardoned sin, or (b) plain columns? Either way, the "newest moderator, oldest date" pairing in
   `/SinLog` and `/Publish Unban` should go. Which date did you mean to show?
3. **`GuildSetting` stores every value as a string**: channel and role ids, `TimeSpan`s, `bool`s and `int`s, each
   parsed on read (`ParseId`, `TimeSpan.Parse`, `ParseEnabled`, `FromDatabase`). The typed alternative is one wide
   row per guild, the way the obsolete `Guild*Settings` tables were laid out. I didn't make this a finding because
   the cache hides the parse cost and the change is large. Is the key-value shape something you want to keep?
4. **D-20.** Is taking the obsolete types out of the model now, while the tables stay until P-7's later release, in
   line with what you decided for P-7?
5. **D-19.** Should `Reaction.GuildId` stay for the reaction features you're planning, or go with the other two?
