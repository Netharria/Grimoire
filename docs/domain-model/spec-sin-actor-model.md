# Spec: Sin Actor Model

> **Status: Implemented** — `AuditActor` renamed to `ModerationActor` during implementation.

## Problem

`Sin` and `SinReasonHistory` use `ModeratorId?` to represent who performed an action. A `null`
value conventionally means "the system did this", but this convention is invisible in the type.
Nothing prevents a `null` moderator on a `Sin` that should have had one, or vice versa.

---

## New type: `AuditActor`

Add `AuditActor.cs` to `Grimoire.Domain`. This discriminated union makes the acting party
explicit and exhaustively matchable.

```csharp
// Grimoire.Domain/AuditActor.cs

namespace Grimoire.Domain;

public abstract record AuditActor
{
    /// <summary>A known Discord moderator performed the action.</summary>
    public sealed record Moderator(ModeratorId Id) : AuditActor;

    /// <summary>The system performed the action (automated or no moderator context available).</summary>
    public sealed record System : AuditActor;
}
```

---

## Changes to `Sin.cs`

Replace `ModeratorId? ModeratorId` with `AuditActor Actor`. Add four named factory methods — one
per `SinType` — that return `Validation<Sin>` consistent with the rest of the domain.

```csharp
[UsedImplicitly]
public sealed record Sin
{
    // EF Core sets this via field access after INSERT for the PostgreSQL bigserial identity.
    public SinId Id { get; init; }

    public required AuditActor Actor { get; init; }
    public required DateTimeOffset SinOn { get; init; }
    public required SinType SinType { get; init; }
    public required UserId UserId { get; init; }
    public required GuildId GuildId { get; init; }

    public ICollection<Pardon> Pardons { get; init; } = [];
    public ICollection<PublishedMessage> PublishMessages { get; init; } = [];
    public ICollection<SinReasonHistory> ReasonHistory { get; init; } = [];

    public static Validation<Sin> ForWarn(
        AuditActor actor, UserId userId, GuildId guildId, DateTimeOffset occurredAt)
        => Create(SinType.Warn, actor, userId, guildId, occurredAt);

    public static Validation<Sin> ForMute(
        AuditActor actor, UserId userId, GuildId guildId, DateTimeOffset occurredAt)
        => Create(SinType.Mute, actor, userId, guildId, occurredAt);

    public static Validation<Sin> ForBan(
        AuditActor actor, UserId userId, GuildId guildId, DateTimeOffset occurredAt)
        => Create(SinType.Ban, actor, userId, guildId, occurredAt);

    public static Validation<Sin> ForKick(
        AuditActor actor, UserId userId, GuildId guildId, DateTimeOffset occurredAt)
        => Create(SinType.Kick, actor, userId, guildId, occurredAt);

    private static Validation<Sin> Create(
        SinType type, AuditActor actor, UserId userId, GuildId guildId, DateTimeOffset occurredAt)
    {
        if (userId.Value == 0)
            return Validation<Sin>.Fail(
                new Error("sin.user-id.invalid", "UserId must be specified."));
        if (guildId.Value == 0)
            return Validation<Sin>.Fail(
                new Error("sin.guild-id.invalid", "GuildId must be specified."));
        if (actor is AuditActor.Moderator m && m.Id.Value == 0)
            return Validation<Sin>.Fail(
                new Error("sin.moderator-id.invalid", "ModeratorId must be specified."));
        return Validation<Sin>.Succeed(new Sin
        {
            Actor = actor,
            SinType = type,
            UserId = userId,
            GuildId = guildId,
            SinOn = occurredAt
        });
    }
}
```

---

## Changes to `SinReasonHistory.cs`

Replace `ModeratorId? ModeratorId` with `AuditActor Actor`.

```csharp
[UsedImplicitly]
public sealed record SinReasonHistory
{
    public required SinId SinId { get; init; }
    public required ModerationReason Reason { get; init; }
    public required AuditActor Actor { get; init; }
    public required DateTimeOffset SetAt { get; init; }
    public Sin? Sin { get; init; }
}
```

---

## EF Core configuration

No schema migration is required. The existing nullable `ModeratorId` column serves as the
backing store for `AuditActor`:

- `AuditActor.System` ↔ `NULL`
- `AuditActor.Moderator(id)` ↔ `id.Value` (non-null `ulong`)

Configure a value converter on both `Sin.Actor` and `SinReasonHistory.Actor`:

```csharp
var actorConverter = new ValueConverter<AuditActor, ulong?>(
    actor => actor is AuditActor.Moderator m ? m.Id.Value : (ulong?)null,
    value => value.HasValue
        ? new AuditActor.Moderator(new ModeratorId(value.Value))
        : new AuditActor.System());

modelBuilder.Entity<Sin>()
    .Property(x => x.Actor)
    .HasConversion(actorConverter)
    .HasColumnName("ModeratorId");

modelBuilder.Entity<SinReasonHistory>()
    .Property(x => x.Actor)
    .HasConversion(actorConverter)
    .HasColumnName("ModeratorId");
```

---

## Call site changes

### Construction

Replace all object initialisers that set `ModeratorId` with calls to the appropriate named factory:

```csharp
// Before
var sin = new Sin
{
    ModeratorId = moderatorId,
    SinType = SinType.Ban,
    UserId = userId,
    GuildId = guildId,
    SinOn = DateTimeOffset.UtcNow
};

// After
var result = Sin.ForBan(new AuditActor.Moderator(moderatorId), userId, guildId, DateTimeOffset.UtcNow);
```

For system-initiated sins (no moderator context):

```csharp
var result = Sin.ForWarn(new AuditActor.System(), userId, guildId, DateTimeOffset.UtcNow);
```

### `SinReasonHistory` construction

```csharp
// Before
new SinReasonHistory { ModeratorId = moderatorId, ... }

// After — moderator action
new SinReasonHistory { Actor = new AuditActor.Moderator(moderatorId), ... }

// After — system action
new SinReasonHistory { Actor = new AuditActor.System(), ... }
```

### Pattern matching on actor

Any call site that previously null-checked `ModeratorId` can now pattern-match cleanly:

```csharp
// Before
var label = sin.ModeratorId is { } id
    ? $"Moderator <@{id.Value}>"
    : "System";

// After
var label = sin.Actor switch
{
    AuditActor.Moderator m => $"Moderator <@{m.Id.Value}>",
    AuditActor.System     => "System",
    _                     => throw new UnreachableException()
};
```
