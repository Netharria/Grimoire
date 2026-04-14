# Settings Module Testing Plan

## Overview

Integration tests for `Grimoire.Settings` using a real PostgreSQL database via Testcontainers.
Each test class gets a fresh `HybridCache` instance to prevent cross-test cache pollution.
Respawn resets only the `Settings` schema between tests.

### Libraries

| Library                               | Purpose                                            |
|---------------------------------------|----------------------------------------------------|
| xunit                                 | Test framework                                     |
| Testcontainers.PostgreSql             | Real PostgreSQL container per test run             |
| Respawn                               | Fast schema reset between tests                    |
| NSubstitute                           | Stub `IDbContextFactory<SettingsDbContext>`        |
| Shouldly                              | Assertions                                         |
| ZiggyCreatures.FusionCache            | FusionCache-backed HybridCache for each test class |
| EntityFramework.Exceptions.PostgreSQL | Exception handling on the real DB context          |

### General Test Class Shape

```csharp
[Collection("Settings collection")]
public sealed class SomeTests(SettingsTestsFactory factory) : IAsyncLifetime
{
    private readonly SettingsModule _sut = SettingsModuleFactory.Create(factory.ConnectionString);

    public async Task InitializeAsync()
    {
        // seed data specific to this class
    }

    public Task DisposeAsync() => factory.ResetDatabase();
}
```

- `SettingsModuleFactory.Create(connectionString)` wires a fresh FusionCache-backed `HybridCache`
  (via `AddFusionCache().AsHybridCache()`) + NSubstitute `IDbContextFactory<SettingsDbContext>`
  returning real contexts on each call.
- `DisposeAsync` resets only the `Settings` schema via Respawn.

---

## Step 1 — Project Bootstrap

**Goal:** Get a compiling, runnable test project with zero tests but all infrastructure in place.

### Files to create

#### `Grimoire.Settings.Tests.csproj`

Packages:

- `xunit`
- `xunit.runner.visualstudio`
- `Microsoft.NET.Test.Sdk`
- `Testcontainers.PostgreSql`
- `Respawn`
- `NSubstitute`
- `NSubstitute.Analyzers.CSharp`
- `Shouldly`
- `ZiggyCreatures.FusionCache`
- `EntityFramework.Exceptions.PostgreSQL`
- `coverlet.collector`

Project reference: `Grimoire.Settings`

#### `SettingsTestsFactory.cs`

Implements `IAsyncLifetime`:

- Spins up `PostgreSqlContainer`
- Creates `SettingsDbContext` and runs `MigrateAsync()`
- Opens a `NpgsqlConnection` and creates a `Respawner` with
  `SchemasToInclude = ["Settings"]` and `DbAdapter = DbAdapter.Postgres`
- Exposes `ConnectionString` (string) and `ResetDatabase()` (Task)

#### `SharedSettingsTestCollection.cs`

```csharp
[CollectionDefinition("Settings collection")]
public sealed class SharedSettingsTestCollection : ICollectionFixture<SettingsTestsFactory> { }
```

#### `SettingsModuleFactory.cs`

Static helper:

- Accepts a `connectionString`
- Creates a `ServiceCollection`, calls `AddHybridCache()`, builds a `ServiceProvider`
- Resolves `HybridCache` from the provider
- Creates an `NSubstitute` mock of `IDbContextFactory<SettingsDbContext>` configured so
  `CreateDbContextAsync(...)` returns a new `SettingsDbContext` pointed at the connection string
  on every call
- Returns a `new SettingsModule(mockFactory, hybridCache)`

---

## Step 2 — Module State Tests

**File:** `ModuleState/SetModuleStateTests.cs`

Seed: nothing (tests operate on a clean schema).

| # | Test name                                    | What it verifies                                                                                                                                                     |
|---|----------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1 | `EnableModule_WritesCustomValueTrueRow`      | `SetModuleState(enable=true)` returns `SettingsWritten`; `IsModuleEnabled` returns `true`                                                                            |
| 2 | `DisableModule_WritesDisabledRow`            | `SetModuleState(enable=false)` returns `SettingsWritten`; `IsModuleEnabled` returns `false`                                                                          |
| 3 | `NoRowInDb_IsModuleEnabled_ReturnsFalse`     | With no rows, `IsModuleEnabled` returns `false` for all non-General modules                                                                                          |
| 4 | `GeneralModule_AlwaysEnabled`                | `IsModuleEnabled(Module.General)` returns `true`; `SetModuleState(Module.General)` returns `SettingsInvalid`                                                         |
| 5 | `RedundantEnable_ReturnsUnchanged_NoNewRow`  | Enable when already enabled returns `SettingsUnchanged`; only one DB row exists                                                                                      |
| 6 | `RedundantDisable_ReturnsUnchanged_NoNewRow` | Disable when already disabled returns `SettingsUnchanged`                                                                                                            |
| 7 | `GetAllModuleState_ReflectsAllSixModules`    | Enable three modules, disable two; `GetAllModuleState` returns correct state for all six including AntiSpam                                                          |
| 8 | `CacheInvalidatedAfterStateChange`           | Read `IsModuleEnabled` (populates cache), write directly to DB to change state, read again — still stale; call `SetModuleState` to re-enable, read again — now fresh |

---

## Step 3 — Guild Settings & Log Channel Tests

**File:** `GuildSettings/UserCommandChannelTests.cs`

| # | Test name                               | What it verifies                                                         |
|---|-----------------------------------------|--------------------------------------------------------------------------|
| 1 | `SetChannel_StoresValue_GetReturnsIt`   | Set a channel ID; `GetUserCommandChannel` returns the same ID            |
| 2 | `SetNull_WritesDisabled_GetReturnsNull` | `SetUserCommandChannelSetting(null)` writes Disabled; get returns `null` |
| 3 | `RedundantWrite_ReturnsUnchanged`       | Set same channel twice; second call returns `SettingsUnchanged`          |
| 4 | `NoRow_GetReturnsNull`                  | No DB row → `GetUserCommandChannel` returns `null`                       |

**File:** `GuildSettings/LogChannelTests.cs`

| # | Test name                                             | What it verifies                                                                                               |
|---|-------------------------------------------------------|----------------------------------------------------------------------------------------------------------------|
| 1 | `ModuleDisabled_GetEffective_ReturnsNull`             | `GetEffectiveLogChannelSetting` returns `null` when the corresponding module is disabled                       |
| 2 | `ModuleEnabled_ChannelSet_GetEffectiveReturnsChannel` | Module enabled + channel configured → correct `ChannelId` returned                                             |
| 3 | `ModuleEnabled_NoChannel_GetEffectiveReturnsNull`     | Module enabled but no channel row → returns `null`                                                             |
| 4 | `SetNull_WritesDisabled_GetConfiguredReturnsNull`     | `SetLogChannelSetting(null)` → `GetConfiguredLogChannelSetting` returns `null`                                 |
| 5 | `AllGuildLogTypes_MapWithoutException`                | Iterate all `GuildLogType` values; calling `ToGuildSettingType()` and `GetLogTypeModule()` throws no exception |

---

## Step 4 — Leveling Settings Tests

**File:** `Leveling/LevelingSettingsTests.cs`

Seed: nothing.

| #  | Test name                                             | What it verifies                                                                |
|----|-------------------------------------------------------|---------------------------------------------------------------------------------|
| 1  | `NoRows_ReturnsAllDefaults`                           | TextTime=3min, Base=15, Modifier=50, Amount=5                                   |
| 2  | `SetTextTime_RoundTrips`                              | Set TextTime=10; stored as `"00:10:00"`; get returns `TimeSpan.FromMinutes(10)` |
| 3  | `SetBase_RoundTrips`                                  | Set Base=20; get returns 20                                                     |
| 4  | `SetModifier_RoundTrips`                              | Set Modifier=100; get returns 100                                               |
| 5  | `SetAmount_RoundTrips`                                | Set Amount=10; get returns 10                                                   |
| 6  | `TextTime_BelowRange_ReturnsInvalid`                  | `SetLevelingSettings(TextTime, 0)` returns `SettingsInvalid`                    |
| 7  | `TextTime_AboveRange_ReturnsInvalid`                  | `SetLevelingSettings(TextTime, 61)` returns `SettingsInvalid`                   |
| 8  | `Amount_BelowRange_ReturnsInvalid`                    | `SetLevelingSettings(Amount, 0)` returns `SettingsInvalid`                      |
| 9  | `Amount_AboveRange_ReturnsInvalid`                    | `SetLevelingSettings(Amount, 101)` returns `SettingsInvalid`                    |
| 10 | `Base_AboveRange_ReturnsInvalid`                      | `SetLevelingSettings(Base, 501)` returns `SettingsInvalid`                      |
| 11 | `Modifier_AboveRange_ReturnsInvalid`                  | `SetLevelingSettings(Modifier, 201)` returns `SettingsInvalid`                  |
| 12 | `MultipleWrites_LatestWins`                           | Write Base=20, then Base=30; `GetLevelingSettings` returns 30                   |
| 13 | `RedundantWrite_ReturnsUnchanged_CacheNotInvalidated` | Write same value twice; second returns `SettingsUnchanged`; only one DB row     |
| 14 | `NewValue_ReturnsWritten_CacheInvalidated`            | Write Base=20, then Base=30; second returns `SettingsWritten`                   |

---

## Step 5 — Rewards & XP Ignores Tests

**File:** `Leveling/RewardsTests.cs`

| # | Test name                           | What it verifies                                                                  |
|---|-------------------------------------|-----------------------------------------------------------------------------------|
| 1 | `NoRewards_ReturnsEmptySet`         | `GetLevelingRewardsAsync` returns empty when no rows                              |
| 2 | `ModuleDisabled_ReturnsEmptySet`    | Rewards exist but module disabled → empty                                         |
| 3 | `SingleReward_ReturnedCorrectly`    | One row; returned with correct RoleId, level, message                             |
| 4 | `MultipleRoles_AllReturned`         | Three different RoleIds each with one row; all three returned                     |
| 5 | `MultipleHistoricalRows_LatestWins` | Same RoleId has two rows; only the latest row used                                |
| 6 | `LatestRowDisabled_NotReturned`     | Latest row has `Enabled=false`; not in result set                                 |
| 7 | `ReEnable_AppearsAgain`             | Disable then re-enable a reward (new row with `Enabled=true`); appears in results |

**File:** `Leveling/XpIgnoresTests.cs`

| #  | Test name                                     | What it verifies                                                     |
|----|-----------------------------------------------|----------------------------------------------------------------------|
| 1  | `ModuleDisabled_IsMessageIgnored_ReturnsTrue` | Module off → short-circuits to `true`                                |
| 2  | `UserIgnored_IsMessageIgnored_ReturnsTrue`    | UserId in ignored set → `true`                                       |
| 3  | `ChannelIgnored_IsMessageIgnored_ReturnsTrue` | ChannelId in ignored set → `true`                                    |
| 4  | `RoleIgnored_IsMessageIgnored_ReturnsTrue`    | One of the user's roles in ignored set → `true`                      |
| 5  | `NoneIgnored_IsMessageIgnored_ReturnsFalse`   | No match → `false`                                                   |
| 6  | `ChannelIgnored_IsMemberIgnored_ReturnsFalse` | `IsMemberIgnored` does not consider channel ignores                  |
| 7  | `AppendEmpty_ReturnsUnchanged`                | `AppendIgnoredItemsEvent` with empty set returns `SettingsUnchanged` |
| 8  | `AppendMismatchedGuild_ReturnsInvalid`        | Item with wrong GuildId returns `SettingsInvalid`                    |
| 9  | `DisabledItem_NotInActiveSet`                 | Latest row `Enabled=false`; item absent from `GetAllIgnoredItems`    |
| 10 | `ReIgnore_AppearsAgain`                       | Un-ignore then re-ignore; item re-appears in active set              |

---

## Step 6 — Message Log Override Tests

**File:** `MessageLog/ChannelOverrideTests.cs`

| #  | Test name                                      | What it verifies                                                        |
|----|------------------------------------------------|-------------------------------------------------------------------------|
| 1  | `ModuleDisabled_ShouldLogMessage_ReturnsFalse` | Module off → `false` regardless of overrides                            |
| 2  | `AlwaysLog_Override_ReturnsTrue`               | Channel has `AlwaysLog` → `true`                                        |
| 3  | `NeverLog_Override_ReturnsFalse`               | Channel has `NeverLog` → `false`                                        |
| 4  | `Inherit_ParentAlwaysLog_ReturnsTrue`          | Channel inherits; parent has `AlwaysLog` → `true`                       |
| 5  | `Inherit_ParentNeverLog_ReturnsFalse`          | Channel inherits; parent has `NeverLog` → `false`                       |
| 6  | `Inherit_NoParent_ReturnsTrue`                 | Channel inherits; not in `channelNodes` → default `true`                |
| 7  | `DeepHierarchy_Inherit_Inherit_AlwaysLog`      | Three levels; leaf and mid inherit; root has `AlwaysLog` → `true`       |
| 8  | `RedundantWrite_ReturnsUnchanged_NoNewRow`     | Set same override twice; second returns `SettingsUnchanged`; one DB row |
| 9  | `NewValue_InsertsRow_CacheUpdated`             | Change override; new row written; cache reflects new value immediately  |
| 10 | `GetAllOverriddenChannels_ExcludesInherit`     | Three channels: AlwaysLog, NeverLog, Inherit; only first two returned   |

---

## Step 7 — Spam Filter Override Tests

**File:** `Moderation/SpamFilterOverrideTests.cs`

| # | Test name                                  | What it verifies                                                |
|---|--------------------------------------------|-----------------------------------------------------------------|
| 1 | `NoRow_ReturnsInherit`                     | No override → `Inherit`                                         |
| 2 | `AlwaysFilter_ReturnsAlwaysFilter`         | Row with `AlwaysFilter` → `AlwaysFilter`                        |
| 3 | `NeverFilter_ReturnsNeverFilter`           | Row with `NeverFilter` → `NeverFilter`                          |
| 4 | `MultipleHistoricalRows_LatestWins`        | Two rows for same channel; latest used                          |
| 5 | `RedundantWrite_ReturnsUnchanged_NoNewRow` | Same value twice; second `SettingsUnchanged`; one DB row        |
| 6 | `NewValue_InsertsRow_CacheUpdated`         | Change override; new row; cache reflects change                 |
| 7 | `GetAll_ExcludesInherit`                   | Two channels: `AlwaysFilter` and `Inherit`; only first returned |
| 8 | `GetAll_EmptyGuild_ReturnsEmpty`           | No overrides for guild → empty                                  |

---

## Step 8 — Mutes & AutoPardon Tests

**File:** `Moderation/MuteTests.cs`

| #  | Test name                                           | What it verifies                                                         |
|----|-----------------------------------------------------|--------------------------------------------------------------------------|
| 1  | `NoMute_IsMemberMuted_ReturnsFalse`                 | No row → `false`                                                         |
| 2  | `ActiveMute_IsMemberMuted_ReturnsTrue`              | Row with future `EndTime` → `true`                                       |
| 3  | `ExpiredMute_IsMemberMuted_ReturnsFalse`            | Row with past `EndTime` → `false`                                        |
| 4  | `AddMute_NewMember_InsertsRow`                      | No existing mute; row inserted                                           |
| 5  | `AddMute_ExistingMember_UpdatesRow`                 | Existing mute updated with new `EndTime` and `SinId`; only one row in DB |
| 6  | `RemoveMute_NotMuted_ReturnsUnchanged`              | No row → `SettingsUnchanged(null)`                                       |
| 7  | `RemoveMute_Muted_ReturnsWrittenAndDeletes`         | Row exists → `SettingsWritten(mute)` and row is gone                     |
| 8  | `GetAllExpiredMutes_OnlyReturnsPastEndTime`         | Three mutes: one past, one future, one exactly now; only past returned   |
| 9  | `GetAllMutes_ReturnsAllForGuild_IgnoresOtherGuilds` | Two guilds with mutes; only correct guild's returned                     |
| 10 | `ModuleDisabled_GetEffectiveMuteRole_ReturnsNull`   | Module off → `null`                                                      |
| 11 | `SetMuteRole_ModuleEnabled_GetEffectiveReturnsRole` | Set role; module enabled; get returns role                               |
| 12 | `DisableMuteRole_GetEffectiveReturnsNull`           | Disable role; get returns `null`                                         |

**File:** `Moderation/AutoPardonTests.cs`

| # | Test name                                    | What it verifies                                                                            |
|---|----------------------------------------------|---------------------------------------------------------------------------------------------|
| 1 | `NoRow_ReturnsDefault`                       | No row → 10950 days                                                                         |
| 2 | `CustomDuration_RoundTrips`                  | Set 30 days; get returns `TimeSpan.FromDays(30)`                                            |
| 3 | `ZeroDurationInDb_ReturnsDefault`            | Value stored as `"00:00:00"` → fallback to default                                          |
| 4 | `ResetAutoPardon_WritesDefaultAsCustomValue` | `ResetAutoPardonDuration` writes a `CustomValue` row containing the default duration string |

---

## Step 9 — Locks & Trackers Tests

**File:** `Moderation/LockTests.cs`

| # | Test name                                    | What it verifies                                                                            |
|---|----------------------------------------------|---------------------------------------------------------------------------------------------|
| 1 | `NoLock_IsChannelLocked_ReturnsFalse`        | No row → `false`                                                                            |
| 2 | `AddLock_IsChannelLocked_ReturnsTrue`        | Lock added; `IsChannelLocked` returns `true`                                                |
| 3 | `AddLock_ExistingLock_UpdatesInPlace`        | Re-lock same channel; `EndTime`, `Reason`, `ModeratorId` updated; one row in DB             |
| 4 | `RemoveLock_NotLocked_ReturnsUnchanged`      | No row → `SettingsUnchanged(null)`                                                          |
| 5 | `RemoveLock_Locked_ReturnsWrittenAndDeletes` | Row exists → `SettingsWritten(lock)`; row gone; `IsChannelLocked` returns `false`           |
| 6 | `GetAllExpiredLocks_OnlyReturnsPastEndTime`  | Two locks: one past, one future; only past returned                                         |
| 7 | `CacheInvalidatedAfterAddAndRemove`          | `IsChannelLocked` (populates cache); remove lock; `IsChannelLocked` returns `false` (fresh) |

**File:** `MessageLog/TrackerTests.cs`

| # | Test name                                           | What it verifies                                                                                        |
|---|-----------------------------------------------------|---------------------------------------------------------------------------------------------------------|
| 1 | `NoTracker_GetTrackerChannel_ReturnsNull`           | No row → `null`                                                                                         |
| 2 | `AddTracker_GetTrackerChannel_ReturnsChannel`       | Tracker added; correct `ChannelId` returned                                                             |
| 3 | `AddTracker_ExistingTracker_UpdatesInPlace`         | Re-track same member; `LogChannelId`, `EndTime`, `ModeratorId` updated; one row                         |
| 4 | `RemoveTracker_NotTracked_ReturnsUnchanged`         | No row → `SettingsUnchanged(null)`                                                                      |
| 5 | `RemoveTracker_Tracked_ReturnsWrittenAndDeletes`    | Row exists → `SettingsWritten(tracker)`; row gone                                                       |
| 6 | `RemoveAllExpiredTrackers_DeletesOnlyExpired`       | Two trackers: one past, one future; only past deleted; active remains                                   |
| 7 | `RemoveAllExpiredTrackers_InvalidatesCachePerGuild` | Expired tracker for guild A, active for guild B; after call, cache for A reflects removal; B unaffected |

---

## Coverage Summary

| Step      | File(s)                                                          | Tests                   |
|-----------|------------------------------------------------------------------|-------------------------|
| 1         | Bootstrap + factory                                              | — (infrastructure only) |
| 2         | `ModuleState/SetModuleStateTests.cs`                             | 8                       |
| 3         | `GuildSettings/UserCommandChannelTests.cs`, `LogChannelTests.cs` | 9                       |
| 4         | `Leveling/LevelingSettingsTests.cs`                              | 14                      |
| 5         | `Leveling/RewardsTests.cs`, `XpIgnoresTests.cs`                  | 17                      |
| 6         | `MessageLog/ChannelOverrideTests.cs`                             | 10                      |
| 7         | `Moderation/SpamFilterOverrideTests.cs`                          | 8                       |
| 8         | `Moderation/MuteTests.cs`, `AutoPardonTests.cs`                  | 16                      |
| 9         | `Moderation/LockTests.cs`, `MessageLog/TrackerTests.cs`          | 14                      |
| **Total** |                                                                  | **~96 tests**           |
