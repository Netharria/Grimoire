# Spec: Settings Layer Fixes

> **Status: Implemented** — All 9 fixes applied. Fix 1: `protected ulong Id { get; init; }` retained at the base (EF Core 10 rejects mapping typed sub-type properties to the same column as a root shadow property in TPH); the typed properties `ChannelId`/`UserId`/`RoleId` on each subtype are made `public required` while keeping the facade `get => new(Id); init => Id = value.Value` pattern — this enforces required typed initialization without EF schema changes. Fix 2: zero-ID guards added to `ChannelLocked.Create` and `ThreadLocked.Create`. Fix 3: second `ChannelLocked.Create` overload replaced with `WithPermissions` instance method; call site in `SettingsModule.Moderation.Locks.cs` updated. Fix 4: `guildId` guards added to `MuteAdded.Create`, `MuteRemoved.Create`, `RewardAdded.Create`, `RewardRemoved.Create`. Fix 5: `RewardEntry.RewardMessage` changed from `string?` to `RewardMessage?`; projection and call sites updated. Fix 6: `GuildModuleState` made `sealed`. Fix 7: `XpTimeoutPeriodExtensions.ToDatabaseString` migrated to C# 14 extension member syntax. Fix 8: `FromDatabaseOrDefault` renamed to `FromDatabase` returning `Validation<T>` on all four value types; `SettingsModule.Leveling.Levelsettings` uses `Validation.Combine`; invalid-setting errors propagate as `InvalidOperationException` caught by `GetLevelingSettings`. Fix 9: `ModerationReason.CreateIfNotNull` and `RewardMessage.CreateIfNotNull` simplified to null-conditional ternary.

A collection of smaller, self-contained fixes across `Grimoire.Settings.Domain` and
`Grimoire.Domain`. None require a schema migration unless noted.

---

## Fix 1 — `XpIgnoredItem`: remove persistence-shaped `protected ulong Id` from base

### Problem

`XpTrackedItem` holds a `protected ulong Id` that each subtype reinterprets as a typed identifier
via a get/init façade property. The raw `Id` is visible and writable within the assembly,
bypassing type intent.

### Change

Remove `Id` from `XpTrackedItem`. Each concrete subtype owns its typed identifier directly as a
`required` property.

```csharp
// Before
public abstract record XpTrackedItem
{
    protected ulong Id { get; init; }
    public required GuildId GuildId { get; init; }
    public required ModeratorId SetBy { get; init; }
    public required DateTimeOffset SetAt { get; init; }
}

public sealed record IgnoredChannel : XpIgnoredItem
{
    public ChannelId ChannelId { get => new(Id); init => Id = value.Value; }
}

// After
public abstract record XpTrackedItem
{
    public required GuildId GuildId { get; init; }
    public required ModeratorId SetBy { get; init; }
    public required DateTimeOffset SetAt { get; init; }
}

public sealed record IgnoredChannel : XpIgnoredItem
{
    public required ChannelId ChannelId { get; init; }
}
```

Apply the same pattern to `WatchedChannel`, `IgnoredMember`, `WatchedMember`, `IgnoredRole`, and
`WatchedRole`.

### EF Core configuration

No schema migration required — the column stays. Add an explicit `HasColumnName("Id")` to each
subtype's typed property so EF Core maps it to the existing column:

```csharp
modelBuilder.Entity<IgnoredChannel>()
    .Property(x => x.ChannelId)
    .HasConversion(v => v.Value, v => new ChannelId(v))
    .HasColumnName("Id");

modelBuilder.Entity<IgnoredMember>()
    .Property(x => x.UserId)
    .HasConversion(v => v.Value, v => new UserId(v))
    .HasColumnName("Id");

modelBuilder.Entity<IgnoredRole>()
    .Property(x => x.RoleId)
    .HasConversion(v => v.Value, v => new RoleId(v))
    .HasColumnName("Id");

// Same for WatchedChannel, WatchedMember, WatchedRole
```

### `Create` factory changes

Update each `Create` factory to use `required` init syntax instead of the old `{ ChannelId = ...,
Id = ... }` pattern. The `ChannelId =` initialiser now directly sets the typed property:

```csharp
return Validation<IgnoredChannel>.Succeed(
    new IgnoredChannel { ChannelId = channelId, GuildId = guildId, SetBy = setBy, SetAt = setAt });
```

---

## Fix 2 — `ChannelLocked` and `ThreadLocked`: add zero-ID guards to `Create`

### Problem

`ChannelUnlocked.Create` and `ThreadUnlocked.Create` validate that `moderatorId`, `channelId`, and
`guildId` are non-zero. The corresponding `ChannelLocked.Create` and `ThreadLocked.Create`
overloads only validate the temporal constraint (`endTime > setAt`), leaving zero IDs
unconditionally accepted.

### Change

Add the same three zero-ID guards to the primary `Create` overload on both `ChannelLocked` and
`ThreadLocked`:

```csharp
// ChannelLocked.Create (primary overload)
public static Validation<ChannelLocked> Create(...)
{
    if (moderatorId.Value == 0)
        return Validation<ChannelLocked>.Fail(
            new Error("channel-lock.moderator-id.invalid", "ModeratorId must be specified."));
    if (channelId.Value == 0)
        return Validation<ChannelLocked>.Fail(
            new Error("channel-lock.channel-id.invalid", "ChannelId must be specified."));
    if (guildId.Value == 0)
        return Validation<ChannelLocked>.Fail(
            new Error("channel-lock.guild-id.invalid", "GuildId must be specified."));
    if (endTime <= setAt)
        return Validation<ChannelLocked>.Fail(
            new Error("channel-lock.end-time.invalid", "End time must be after the lock's set time."));
    // ... succeed
}
```

Apply the same guards to `ThreadLocked.Create`.

---

## Fix 3 — `ChannelLocked`: replace always-succeeding static `Create` overload with `WithPermissions`

### Problem

The second `ChannelLocked.Create` overload takes an existing `ChannelLocked` plus permissions and
always returns `Validation<ChannelLocked>.Succeed(...)`. Returning `Validation<T>` from something
that cannot fail is a misleading API.

### Change

Replace the static overload with an instance method returning `ChannelLocked` directly:

```csharp
// Before
public static Validation<ChannelLocked> Create(
    ChannelLocked lockAction,
    PreviouslyAllowedPermissions previouslyAllowed,
    PreviouslyDeniedPermissions previouslyDenied)
{
    return Validation<ChannelLocked>.Succeed(new ChannelLocked(...));
}

// After
public ChannelLocked WithPermissions(
    PreviouslyAllowedPermissions previouslyAllowed,
    PreviouslyDeniedPermissions previouslyDenied)
    => new(this.ModeratorId, this.ChannelId, this.GuildId, this.SetAt,
           this.Reason, previouslyAllowed, previouslyDenied, this.EndTime);
```

Update all call sites from `ChannelLocked.Create(lock, allowed, denied).SomeValidationOp(...)` to
`lock.WithPermissions(allowed, denied)`.

---

## Fix 4 — `Mute` and `Reward`: add `guildId` validation to all `Create` factories

### Problem

`MuteAdded.Create`, `MuteRemoved.Create`, `RewardAdded.Create`, and `RewardRemoved.Create` all
accept a `GuildId` parameter but do not check that it is non-zero, inconsistent with every other
`Create` factory in `Grimoire.Settings`.

### Change

Add `guildId.Value == 0` guards to all four factories:

```csharp
if (guildId.Value == 0)
    return Validation<MuteAdded>.Fail(
        new Error("mute.guild-id.invalid", "GuildId must be specified."));
```

Use appropriate error code prefixes for each type (`mute`, `mute-removed`, `reward`,
`reward-removed`).

---

## Fix 5 — `RewardEntry`: use `RewardMessage?` instead of `string?`

### Problem

`RewardEntry` is a read projection used to surface reward information. It stores the reward message
as a raw `string?`, while the domain entity `RewardAdded` correctly uses the `RewardMessage?`
value object.

### Change

```csharp
// Before
public sealed record RewardEntry(RoleId RoleId, int RewardLevel, string? RewardMessage);

// After
public sealed record RewardEntry(RoleId RoleId, int RewardLevel, RewardMessage? RewardMessage);
```

Add a value converter to the EF projection configuration so the `string?` column materialises as
`RewardMessage?`:

```csharp
// In the EF query or owned-type configuration for RewardEntry projections
.Property(x => x.RewardMessage)
.HasConversion(
    v => v.HasValue ? v.Value.Value : null,
    v => v is not null ? RewardMessage.FromDatabase(v) : (RewardMessage?)null);
```

---

## Fix 6 — `GuildModuleState`: add `sealed`

### Problem

`GuildModuleState` is a concrete record that is not `sealed`, inconsistent with every other
concrete record in both domain layers.

### Change

```csharp
// Before
public record GuildModuleState(...);

// After
public sealed record GuildModuleState(...);
```

---

## Fix 7 — `XpTimeoutPeriod`: update extension to C# 14 extension member syntax

### Problem

`XpTimeoutPeriodExtensions.ToDatabaseString` uses the old `this` extension method style while the
three sibling extensions (`XpGainAmountExtensions`, `LevelingSettingsExtensions`,
`LevelScalingModifierExtensions`) all use the new C# 14 `extension(...)` member syntax.

### Change

```csharp
// Before
public static class XpTimeoutPeriodExtensions
{
    internal static Validation<string> ToDatabaseString(this Validation<XpTimeoutPeriod> v)
        => v.Map(x => x.Value.ToString("c", CultureInfo.InvariantCulture));
}

// After
public static class XpTimeoutPeriodExtensions
{
    extension(Validation<XpTimeoutPeriod> v)
    {
        internal Validation<string> ToDatabaseString()
            => v.Map(x => x.Value.ToString("c", CultureInfo.InvariantCulture));
    }
}
```

---

## Fix 8 — `FromDatabaseOrDefault`: propagate validation errors to callers

### Problem

`XpGainAmount.FromDatabaseOrDefault`, `LevelScalingBase.FromDatabaseOrDefault`,
`LevelScalingModifier.FromDatabaseOrDefault`, and `XpTimeoutPeriod.FromDatabaseOrDefault` call
`int.Parse` / `TimeSpan.Parse` without bounds-checking. An out-of-range value stored in the
database bypasses the domain constraint and silently constructs an invalid value object. Guild
admins who set a custom value would have no indication it was ignored.

### Change

Rename each method to `FromDatabase` and return `Validation<T>`. A `null` input (no custom setting
saved for this guild) still succeeds with the domain default — that is correct behaviour. A
non-null but invalid value becomes a `Validation.Fail` that propagates up to the command handler,
where the guild admin can be notified to reset the setting.

```csharp
// XpGainAmount.cs
internal static Validation<XpGainAmount> FromDatabase(string? input)
    => input is null
        ? Validation<XpGainAmount>.Succeed(Default)
        : Create(input);

// LevelScalingBase.cs
internal static Validation<LevelScalingBase> FromDatabase(string? input)
    => input is null
        ? Validation<LevelScalingBase>.Succeed(Default)
        : Create(input);

// LevelScalingModifier.cs
internal static Validation<LevelScalingModifier> FromDatabase(string? input)
    => input is null
        ? Validation<LevelScalingModifier>.Succeed(Default)
        : Create(input);

// XpTimeoutPeriod.cs
internal static Validation<XpTimeoutPeriod> FromDatabase(string? input)
    => input is null
        ? Validation<XpTimeoutPeriod>.Succeed(Default)
        : Create(input);
```

### Settings service changes

Wherever `LevelingSettingEntry` is constructed from raw database values, replace the imperative
per-call pattern with `Validation.Combine` so all four failures are collected at once:

```csharp
// Before
var entry = new LevelingSettingEntry(
    XpTimeoutPeriod.FromDatabaseOrDefault(rawTimeout),
    LevelScalingModifier.FromDatabaseOrDefault(rawModifier),
    LevelScalingBase.FromDatabaseOrDefault(rawBase),
    XpGainAmount.FromDatabaseOrDefault(rawAmount));

// After
var entryValidation = Validation.Combine(
    XpTimeoutPeriod.FromDatabase(rawTimeout),
    LevelScalingModifier.FromDatabase(rawModifier),
    LevelScalingBase.FromDatabase(rawBase),
    XpGainAmount.FromDatabase(rawAmount))
    .Map((timeout, modifier, @base, amount) =>
        new LevelingSettingEntry(timeout, modifier, @base, amount));
```

### Command handler changes

Propagate `entryValidation` up through the result chain. On failure, reply to the guild admin
identifying which settings need to be reset:

```csharp
return entryValidation.Match(
    onValid:   entry  => /* proceed with leveling logic */,
    onInvalid: errors => /* reply: "One or more leveling settings are invalid: {errors}. 
                                    Please reset them using the /leveling settings commands." */);
```

---

## Fix 9 — `ModerationReason` and `RewardMessage`: simplify `CreateIfNotNull`

### Problem

Both `ModerationReason.CreateIfNotNull` and `RewardMessage.CreateIfNotNull` use a `switch`
expression where a null-conditional ternary is simpler and equally readable.

### Change

```csharp
// ModerationReason.cs — before
public static Validation<ModerationReason?> CreateIfNotNull(string? input)
    => input switch
    {
        not null => Create(input).Map(reason => (ModerationReason?)reason),
        _        => Validation<ModerationReason?>.Succeed(null)
    };

// After
public static Validation<ModerationReason?> CreateIfNotNull(string? input)
    => input is null
        ? Validation<ModerationReason?>.Succeed(null)
        : Create(input).Map(r => (ModerationReason?)r);
```

Apply the same change to `RewardMessage.CreateIfNotNull`.
