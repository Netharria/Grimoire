# Spec: XpHistory Redesign

> **Status: Implemented** — `RawXp` backing column on `XpHistoryEntry` base (internal) used for aggregate queries; typed `Xp` properties on subtypes are computed from `RawXp`. `MinMaxValue(0)` corrected to `MinMaxValue(1)` on `AwardUserXp`. `.LongCountAsync()` bug in `GainUserXp` fixed to `.SumAsync(x => x.RawXp)`.

## Problem

`XpHistory` is a flat record with an `XpHistoryType` enum and an optional `ModeratorId? AwarderId`.
The `AwarderId` field is only semantically valid when `Type == Awarded`, but nothing in the type
system enforces this. Any other type can be constructed with a non-null `AwarderId`, and an
`Awarded` entry can be constructed without one.

`Created` entries (zero XP, recorded on user join) no longer provide value and will be dropped.
`Migrated` entries exist as historical data only; no new ones will be created.

---

## New value types

Add two new `readonly record struct` types to `Grimoire.Domain`. These can live in a new file
`XpHistory.cs` alongside the variant records, or in a dedicated `XpAmount.cs`.

```csharp
/// <summary>
/// A non-zero positive XP amount. Used for Earned, Awarded, and Migrated entries.
/// </summary>
public readonly record struct PositiveXpAmount
{
    private PositiveXpAmount(long value) => Value = value;

    public long Value { get; }

    /// <summary>Bypasses validation for EF Core materialisation. DB data is trusted post-migration.</summary>
    internal static PositiveXpAmount FromDatabase(long value) => new(value);

    public static Validation<PositiveXpAmount> Create(long value)
        => value > 0
            ? Validation<PositiveXpAmount>.Succeed(new(value))
            : Validation<PositiveXpAmount>.Fail(
                new Error("xp-amount.must-be-positive", "XP amount must be greater than zero."));

    public override string ToString() => Value.ToString();
}

/// <summary>
/// A non-zero negative XP amount. Used for Reclaimed entries.
/// </summary>
public readonly record struct NegativeXpAmount
{
    private NegativeXpAmount(long value) => Value = value;

    public long Value { get; }

    /// <summary>Bypasses validation for EF Core materialisation. DB data is trusted post-migration.</summary>
    internal static NegativeXpAmount FromDatabase(long value) => new(value);

    public static Validation<NegativeXpAmount> Create(long value)
        => value < 0
            ? Validation<NegativeXpAmount>.Succeed(new(value))
            : Validation<NegativeXpAmount>.Fail(
                new Error("xp-amount.must-be-negative", "XP amount must be less than zero."));

    public override string ToString() => Value.ToString();
}
```

---

## New variant records

Replace the existing `XpHistory` sealed record and `XpHistoryType` enum with an abstract base and
four concrete variants. The `Created` variant is retired entirely.

```csharp
[UsedImplicitly]
public abstract record XpHistoryEntry
{
    public required DateTimeOffset TimeOut { get; init; }
    public required UserId UserId { get; init; }
    public required GuildId GuildId { get; init; }
}

/// <summary>XP earned organically by the user through activity.</summary>
[UsedImplicitly]
public sealed record EarnedXp : XpHistoryEntry
{
    public required PositiveXpAmount Xp { get; init; }

    public static Validation<EarnedXp> Create(
        PositiveXpAmount xp, UserId userId, GuildId guildId, DateTimeOffset timeOut)
        => Validation<EarnedXp>.Succeed(new EarnedXp
        {
            Xp = xp, UserId = userId, GuildId = guildId, TimeOut = timeOut
        });
}

/// <summary>XP awarded to the user by a moderator.</summary>
[UsedImplicitly]
public sealed record AwardedXp : XpHistoryEntry
{
    public required PositiveXpAmount Xp { get; init; }
    public required ModeratorId AwarderId { get; init; }

    public static Validation<AwardedXp> Create(
        PositiveXpAmount xp, ModeratorId awarderId, UserId userId, GuildId guildId, DateTimeOffset timeOut)
    {
        if (awarderId.Value == 0)
            return Validation<AwardedXp>.Fail(
                new Error("awarded-xp.awarder-id.invalid", "AwarderId must be specified."));
        return Validation<AwardedXp>.Succeed(new AwardedXp
        {
            Xp = xp, AwarderId = awarderId, UserId = userId, GuildId = guildId, TimeOut = timeOut
        });
    }
}

/// <summary>XP reclaimed from the user by a moderator. Xp value is negative.</summary>
[UsedImplicitly]
public sealed record ReclaimedXp : XpHistoryEntry
{
    public required NegativeXpAmount Xp { get; init; }

    public static Validation<ReclaimedXp> Create(
        NegativeXpAmount xp, UserId userId, GuildId guildId, DateTimeOffset timeOut)
        => Validation<ReclaimedXp>.Succeed(new ReclaimedXp
        {
            Xp = xp, UserId = userId, GuildId = guildId, TimeOut = timeOut
        });
}

/// <summary>
/// XP carried over from a legacy database migration. Read-only historical record;
/// no factory is provided because no new Migrated entries will be created.
/// EF Core materialises these directly from the database.
/// </summary>
[UsedImplicitly]
public sealed record MigratedXp : XpHistoryEntry
{
    public required PositiveXpAmount Xp { get; init; }
}
```

---

## EF Core configuration

The existing table stays unchanged (TPH, same columns). Update the EF configuration to:

1. Replace the single `XpHistory` entity mapping with the abstract `XpHistoryEntry` base and four
   concrete type mappings.
2. Use the existing `Type` column as the discriminator with the same string values it already holds.
3. Add value converters for `PositiveXpAmount` and `NegativeXpAmount` ↔ `long`.
4. Map `AwarderId` as nullable on the base or per-type; it is only populated for `AwardedXp`.

```csharp
modelBuilder.Entity<XpHistoryEntry>()
    .HasDiscriminator<string>("Type")
    .HasValue<EarnedXp>("Earned")
    .HasValue<AwardedXp>("Awarded")
    .HasValue<ReclaimedXp>("Reclaimed")
    .HasValue<MigratedXp>("Migrated");

modelBuilder.Entity<EarnedXp>()
    .Property(x => x.Xp)
    .HasConversion(v => v.Value, v => PositiveXpAmount.FromDatabase(v))
    .HasColumnName("Xp");

modelBuilder.Entity<AwardedXp>()
    .Property(x => x.Xp)
    .HasConversion(v => v.Value, v => PositiveXpAmount.FromDatabase(v))
    .HasColumnName("Xp");

modelBuilder.Entity<ReclaimedXp>()
    .Property(x => x.Xp)
    .HasConversion(v => v.Value, v => NegativeXpAmount.FromDatabase(v))
    .HasColumnName("Xp");

modelBuilder.Entity<MigratedXp>()
    .Property(x => x.Xp)
    .HasConversion(v => v.Value, v => PositiveXpAmount.FromDatabase(v))
    .HasColumnName("Xp");

// AwarderId is only populated for AwardedXp; all other types store NULL
modelBuilder.Entity<AwardedXp>()
    .Property(x => x.AwarderId)
    .HasConversion(v => v.Value, v => new ModeratorId(v))
    .HasColumnName("AwarderId");
```

---

## Data migration

Write a single EF Core migration with the following raw SQL steps, executed in order.

### Step 1 — Fail fast on Awarded rows missing an AwarderId

If any `Awarded` row is missing an `AwarderId`, the data is already inconsistent and must be fixed
manually before the migration can proceed. Raise an exception rather than silently continuing.

```sql
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM "XpHistory"
        WHERE "Type" = 'Awarded' AND "AwarderId" IS NULL
    ) THEN
        RAISE EXCEPTION
            'Data integrity error: Awarded XpHistory rows found with NULL AwarderId. '
            'Resolve these rows manually before running this migration.';
    END IF;
END $$;
```

### Step 2 — Null out stale AwarderId on non-Awarded rows

```sql
UPDATE "XpHistory"
SET "AwarderId" = NULL
WHERE "Type" != 'Awarded' AND "AwarderId" IS NOT NULL;
```

### Step 3 — Delete Created rows

```sql
DELETE FROM "XpHistory" WHERE "Type" = 'Created';
```

No structural schema changes (no columns added or removed).

---

## Call site changes

All code that constructs `XpHistory { ... }` must be updated to call the appropriate typed factory.
All code that pattern-matches on `XpHistoryType` must be updated to switch on the concrete type:

```csharp
// Before
var entry = new XpHistory { Type = XpHistoryType.Awarded, AwarderId = moderatorId, Xp = 100, ... };

// After
var result = PositiveXpAmount.Create(100)
    .Bind(xp => AwardedXp.Create(xp, moderatorId, userId, guildId, timeOut));

// Before
switch (entry.Type)
{
    case XpHistoryType.Earned: ...
    case XpHistoryType.Awarded: ...
}

// After
switch (entry)
{
    case EarnedXp e: ...
    case AwardedXp a: // a.AwarderId is always non-null here
    case ReclaimedXp r: ...
    case MigratedXp m: ...
}
```

Queries that previously filtered by `XpHistoryType` enum value should filter by discriminator
string (`"Earned"`, `"Awarded"`, etc.) or use EF's `OfType<T>()`.
