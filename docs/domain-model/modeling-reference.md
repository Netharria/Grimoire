# Domain Modeling Reference

Date: 2026-04-18

## Reference checklist

- [x] Capture concrete example shapes for the highest-value redesign targets
- [x] Show how to model invalid-state prevention directly in the types
- [x] Keep examples domain-first and C#-idiomatic
- [x] Note where EF Core mapping would likely need to adapt

## Design principles used in these examples

### Prefer value objects over raw primitives

Use `readonly record struct` for small validated values such as:

- names
- reasons
- message content
- embed colors
- XP amounts
- level/rank wrappers where useful

### Prefer variants over boolean state bundles

Use:

- `abstract record`
- sealed derived records
- exhaustive `switch` expressions

This works especially well when the business meaning is currently hidden in:

- multiple booleans
- nullable companion fields
- generic storage-oriented identifiers

### Prefer safe construction

For simple immutable data:

- primary constructors keep syntax short

For values with invariants:

- `TryCreate`
- or `Create` returning a result type

### Prefer staged migration paths for live tables

For `Grimoire.Domain` types backed by tables that already contain data, prefer a model transition that can be rolled out safely:

- add new columns or tables without immediately dropping old ones
- backfill from the current representation
- tolerate old and new shapes in the application during the transition
- remove legacy state only after the new representation is fully populated and in use

This does not change the desired end state. It changes how aggressively the shape should move in one release.

### Preserve already-migrated replacement models, but use the pre-production window

Many `Grimoire.Settings/Domain` types are already the replacement models for obsolete settings-era entities still visible in `Grimoire.Domain` and `Grimoire/GrimoireDbContext.cs`.

That changes the default recommendation for those types:

- preserve the newer boundary first
- redesign the settings models that still leak persistence shape or hide important invariants
- otherwise prefer incremental hardening through better value objects, factories, and explicit temporal rules

Because `Grimoire.Settings` has not been migrated to production yet, safe improvements that still translate cleanly from the obsolete source data should be taken now instead of deferred. This is the lowest-risk point to tighten schemas, remove persistence-shaped public APIs, and make migration rules more explicit before a production boundary exists.

### Prefer read-only public collections

When a type owns a collection, expose:

- `IReadOnlyList<T>`
- `IReadOnlyCollection<T>`

and keep mutation behind methods or creation operations.

---

# Example 1: `CustomCommand`

Current file:

- `Grimoire.Domain/CustomCommand.cs`

## Problem summary

The current shape relies on:

- `HasMention`
- `HasMessage`
- `IsEmbedded`
- `RestrictedUse`
- optional `EmbedColor`
- a separate `Roles` collection

That combination allows invalid states and forces consumers to interpret meaning from flags.

It also hides an important behavior in the current implementation of `GetCustomCommand.IsUserAuthorized(...)`:

- no roles + `RestrictedUse = false` means everyone may execute the command
- no roles + `RestrictedUse = true` means no one may execute the command
- roles + `RestrictedUse = true` means the roles are an allow-list
- roles + `RestrictedUse = false` means the roles are a deny-list

## Recommended shape

```csharp
namespace Grimoire.Domain;

public sealed record CustomCommand(
    CustomCommandName Name,
    GuildId GuildId,
    DateTimeOffset CreatedAt,
    CommandPresentation Presentation,
    CommandInvocationBehavior InvocationBehavior,
    CommandRolePolicy RolePolicy,
    ModeratorId? CreatedBy);

public abstract record CommandPresentation;

public sealed record PlainTextCommand(MessageContent Content) : CommandPresentation;

public sealed record EmbeddedCommand(
    MessageContent Content,
    EmbedColorSetting Color) : CommandPresentation;

public enum CommandInvocationBehavior
{
    MentionOnly,
    MessageOnly,
    MentionAndMessage
}

public abstract record CommandRolePolicy
{
    public sealed record Everyone : CommandRolePolicy;

    public sealed record NoOne : CommandRolePolicy;

    public sealed record AllowListed(IReadOnlySet<RoleId> Roles) : CommandRolePolicy;

    public sealed record DenyListed(IReadOnlySet<RoleId> Roles) : CommandRolePolicy;
}

public abstract record EmbedColorSetting
{
    public sealed record None : EmbedColorSetting;

    public sealed record Specified(CustomCommandEmbedColor Value) : EmbedColorSetting;
}
```

## Value objects used by this shape

```csharp
namespace Grimoire.Domain;

public readonly record struct CustomCommandName
{
    public string Value { get; }

    private CustomCommandName(string value) => this.Value = value;

    public static bool TryCreate(string? input, out CustomCommandName name)
    {
        var trimmed = input?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length is < 1 or > 100)
        {
            name = default;
            return false;
        }

        name = new(trimmed);
        return true;
    }

    public override string ToString() => this.Value;
}

public readonly record struct CustomCommandEmbedColor
{
    public string Value { get; }

    private CustomCommandEmbedColor(string value) => this.Value = value;

    public static bool TryCreate(string? input, out CustomCommandEmbedColor color)
    {
        var trimmed = input?.Trim();

        if (trimmed is null || !System.Text.RegularExpressions.Regex.IsMatch(trimmed, "^#?[0-9A-Fa-f]{6}$"))
        {
            color = default;
            return false;
        }

        color = new(trimmed.StartsWith('#') ? trimmed : $"#{trimmed}");
        return true;
    }

    public override string ToString() => this.Value;
}
```

`EmbedColorSetting.None` represents the existing valid case where a command is an embed but has no specified color.

## Why this is better

- embed commands can explicitly distinguish between “no color” and “custom color”
- plain-text commands cannot accidentally carry an embed-only property
- role access semantics are explicit, including the current empty-role edge cases
- consumer logic becomes simple pattern matching

## Persistence note

If this shape is adopted, EF Core mapping for `CustomCommand` will likely need either:

- an owned/value conversion strategy for nested value objects and discriminators, or
- a persistence DTO / EF entity separate from the richer domain model

Because `Grimoire.Domain/CustomCommand.cs` already maps to a live table, the migration should be staged. The existing columns in `Grimoire/Configuration/CustomCommandConfiguration.cs` already expose a useful compatibility surface:

- `Content`
- nullable `EmbedColor`
- `Name`
- `GuildId`
- `ModeratorId`
- `RestrictedUse`

The current boolean flags and role rows can be migrated safely by treating the new shape as a projection over the existing columns first, then introducing any new persistence structure additively if the final design still needs it.

The role-policy mapping from the current table shape is straightforward:

- no role rows + `RestrictedUse = false` → `CommandRolePolicy.Everyone`
- no role rows + `RestrictedUse = true` → `CommandRolePolicy.NoOne`
- role rows + `RestrictedUse = true` → `CommandRolePolicy.AllowListed(roles)`
- role rows + `RestrictedUse = false` → `CommandRolePolicy.DenyListed(roles)`

A practical migration path would be:

1. keep existing columns readable
2. introduce the new domain representation in code behind adapters or converters
3. if needed, add new columns/discriminators in a nullable form
4. backfill from `IsEmbedded`, `EmbedColor`, `HasMention`, `HasMessage`, `RestrictedUse`, and related role rows
5. switch the application to read the new representation first
6. remove legacy state only in a later cleanup migration

---

# Example 2: `XpHistory`

Current file:

- `Grimoire.Domain/XpHistory.cs`

## Problem summary

The current type stores:

- `Type`
- raw numeric `Xp`
- optional `AwarderId`

That means validity depends on rules outside the type.

## Recommended shape

```csharp
namespace Grimoire.Domain;

public abstract record XpHistoryEntry(
    XpAmount Xp,
    DateTimeOffset At,
    UserId UserId,
    GuildId GuildId);

public sealed record EarnedXp(
    XpAmount Xp,
    DateTimeOffset At,
    UserId UserId,
    GuildId GuildId)
    : XpHistoryEntry(Xp, At, UserId, GuildId);

public sealed record AwardedXp(
    XpAmount Xp,
    DateTimeOffset At,
    ModeratorId AwarderId,
    UserId UserId,
    GuildId GuildId)
    : XpHistoryEntry(Xp, At, UserId, GuildId);

public sealed record ReclaimedXp(
    XpAmount Xp,
    DateTimeOffset At,
    UserId UserId,
    GuildId GuildId)
    : XpHistoryEntry(Xp, At, UserId, GuildId);

public sealed record MigratedXp(
    XpAmount Xp,
    DateTimeOffset At,
    UserId UserId,
    GuildId GuildId)
    : XpHistoryEntry(Xp, At, UserId, GuildId);

public sealed record CreatedXp(
    XpAmount Xp,
    DateTimeOffset At,
    UserId UserId,
    GuildId GuildId)
    : XpHistoryEntry(Xp, At, UserId, GuildId);
```

## Supporting value object

```csharp
namespace Grimoire.Domain;

public readonly record struct XpAmount
{
    public long Value { get; }

    private XpAmount(long value) => this.Value = value;

    public static bool TryCreate(long value, out XpAmount xp)
    {
        if (value < 0)
        {
            xp = default;
            return false;
        }

        xp = new(value);
        return true;
    }

    public static implicit operator long(XpAmount value) => value.Value;
    public override string ToString() => this.Value.ToString();
}
```

## Why this is better

- awarded XP always has an awarder
- business meaning moves into the type itself
- consumer code can switch on concrete types instead of interpreting an enum manually

---

# Example 3: `XpIgnoredItem`

Current file:

- `Grimoire.Settings/Domain/XpIgnoredItem.cs`

## Problem summary

The current model stores a generic `Id` in the base type and each subtype remaps it to its semantic meaning.

That is efficient for discriminator-based storage, but it makes the public model more opaque than it needs to be.

This remains a good redesign candidate even after accounting for the old-to-new migration boundary, because the replacement model still exposes the persistence shortcut directly in its public API.

The tests in `Grimoire.Settings.Tests/Leveling/XpIgnoresTests.cs` show the actual intent clearly:

- ignore a channel
- ignore a member
- ignore a role
- disable an ignore
- re-enable an ignore later

## Recommended shape

```csharp
namespace Grimoire.Settings.Domain;

public abstract record XpIgnoredItem(
    GuildId GuildId,
    ModeratorId SetBy,
    DateTimeOffset SetAt,
    bool Enabled);

public sealed record IgnoredChannel(
    ChannelId ChannelId,
    GuildId GuildId,
    ModeratorId SetBy,
    DateTimeOffset SetAt,
    bool Enabled)
    : XpIgnoredItem(GuildId, SetBy, SetAt, Enabled);

public sealed record IgnoredMember(
    UserId UserId,
    GuildId GuildId,
    ModeratorId SetBy,
    DateTimeOffset SetAt,
    bool Enabled)
    : XpIgnoredItem(GuildId, SetBy, SetAt, Enabled);

public sealed record IgnoredRole(
    RoleId RoleId,
    GuildId GuildId,
    ModeratorId SetBy,
    DateTimeOffset SetAt,
    bool Enabled)
    : XpIgnoredItem(GuildId, SetBy, SetAt, Enabled);
```

## Why this is better

- each subtype directly owns the domain identifier it represents
- intent is visible from the public API
- tests and domain model line up more naturally

## Persistence note

`Grimoire.Settings/Configurations/XpIgnoredItemsConfiguration.cs` would need to stop relying on a shared generic `Id` property in the public model.

That is a worthwhile trade if domain clarity is more important than using one generalized storage field in the public type.

---

# Example 4: `Sin`, `Pardon`, and reason history

Current files:

- `Grimoire.Domain/Sin.cs`
- `Grimoire.Domain/SinReasonHistory.cs`
- `Grimoire.Domain/Pardon.cs`
- `Grimoire.Domain/PublishedMessage.cs`

## Problem summary

Current issues include:

- nullable moderator/actor state
- raw string reasons
- read-write collection exposure
- likely aggregate invariants that are not enforced by construction

## Recommended shape

```csharp
namespace Grimoire.Domain;

public sealed record Sin(
    SinId Id,
    SinType Type,
    AuditActor Actor,
    DateTimeOffset OccurredAt,
    UserId UserId,
    GuildId GuildId,
    IReadOnlyList<SinReasonHistoryEntry> ReasonHistory,
    IReadOnlyList<Pardon> Pardons,
    IReadOnlyList<PublishedMessage> PublishedMessages);

public abstract record AuditActor
{
    public sealed record Moderator(ModeratorId Id) : AuditActor;
    public sealed record System : AuditActor;
}

public sealed record SinReasonHistoryEntry(
    SinId SinId,
    ModerationReason Reason,
    AuditActor Actor,
    DateTimeOffset SetAt);

public sealed record Pardon(
    SinId SinId,
    GuildId GuildId,
    ModeratorId ModeratorId,
    ModerationReason Reason,
    DateTimeOffset SetAt);
```

## Supporting value object

```csharp
namespace Grimoire.Domain;

public readonly record struct ModerationReason
{
    public string Value { get; }

    private ModerationReason(string value) => this.Value = value;

    public static bool TryCreate(string? input, out ModerationReason reason)
    {
        var trimmed = input?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 4096)
        {
            reason = default;
            return false;
        }

        reason = new(trimmed);
        return true;
    }

    public override string ToString() => this.Value;
}
```

## Why this is better

- actor identity is explicit rather than nullable
- empty reasons are prevented at creation time
- collections are intentionally read-only
- richer aggregate rules can be enforced in factories later

---

# Example 5: lock and unlock events

Current files:

- `Grimoire.Settings/Domain/ChannelLock.cs`
- `Grimoire.Settings/Domain/ThreadLock.cs`
- `Grimoire.Settings/Services/SettingsModule.Moderation.Locks.cs`
- legacy reference: `Grimoire.Domain/Obsolete/Lock.cs`

## Problem summary

The current settings implementation already behaves like an append-only event stream:

- a lock event is appended
- an unlock event is appended later
- the active state is computed by selecting the latest event for the channel/guild pair

That pattern is good.

This example should now be read as an incremental hardening direction, not as evidence that `ChannelLock` and `ThreadLock` need another broad redesign. Compared to the obsolete `Grimoire.Domain/Obsolete/Lock.cs` shape, the settings models are already a meaningful improvement.

However:

- unlock events currently use an empty string as the reason
- `EndTime > SetAt` is not enforced in the model
- the public type names could better reflect event semantics

## Recommended shape

```csharp
namespace Grimoire.Settings.Domain;

public abstract record ChannelLockEventBase(
    ModeratorId ModeratorId,
    ModerationReason? Reason,
    ChannelId ChannelId,
    GuildId GuildId,
    DateTimeOffset SetAt);

public sealed record ChannelLocked(
    ModeratorId ModeratorId,
    ModerationReason Reason,
    ChannelId ChannelId,
    GuildId GuildId,
    DateTimeOffset SetAt,
    PreviouslyAllowedPermissions PreviouslyAllowed,
    PreviouslyDeniedPermissions PreviouslyDenied,
    DateTimeOffset EndTime)
    : ChannelLockEventBase(ModeratorId, Reason, ChannelId, GuildId, SetAt)
{
    public static bool TryCreate(
        ModeratorId moderatorId,
        ModerationReason reason,
        ChannelId channelId,
        GuildId guildId,
        DateTimeOffset setAt,
        PreviouslyAllowedPermissions previouslyAllowed,
        PreviouslyDeniedPermissions previouslyDenied,
        DateTimeOffset endTime,
        out ChannelLocked? value)
    {
        if (endTime <= setAt)
        {
            value = null;
            return false;
        }

        value = new(
            moderatorId,
            reason,
            channelId,
            guildId,
            setAt,
            previouslyAllowed,
            previouslyDenied,
            endTime);

        return true;
    }
}

public sealed record ChannelUnlocked(
    ModeratorId ModeratorId,
    ChannelId ChannelId,
    GuildId GuildId,
    DateTimeOffset SetAt,
    ModerationReason? Reason = null)
    : ChannelLockEventBase(ModeratorId, Reason, ChannelId, GuildId, SetAt);
```

A similar approach fits thread locks:

```csharp
namespace Grimoire.Settings.Domain;

public abstract record ThreadLockEventBase(
    ModeratorId ModeratorId,
    ModerationReason? Reason,
    ChannelId ChannelId,
    GuildId GuildId,
    DateTimeOffset SetAt);

public sealed record ThreadLocked(
    ModeratorId ModeratorId,
    ModerationReason Reason,
    ChannelId ChannelId,
    GuildId GuildId,
    DateTimeOffset SetAt,
    DateTimeOffset EndTime)
    : ThreadLockEventBase(ModeratorId, Reason, ChannelId, GuildId, SetAt);

public sealed record ThreadUnlocked(
    ModeratorId ModeratorId,
    ChannelId ChannelId,
    GuildId GuildId,
    DateTimeOffset SetAt,
    ModerationReason? Reason = null)
    : ThreadLockEventBase(ModeratorId, Reason, ChannelId, GuildId, SetAt);
```

## Why this is better

- the append-only event model stays intact
- names reflect what the records actually represent
- “reason absent” is modeled intentionally instead of using `string.Empty`
- time-based invariants can be checked at creation time

## Legacy note

The obsolete `Grimoire.Domain/Obsolete/Lock.cs` shows the older shape that mixed raw primitive IDs, mutable properties, and unvalidated strings. The settings-domain event model is already a major improvement over that legacy shape.

Because the settings-side replacements are not yet in production, this is also a good time to apply any safe compatibility-preserving improvements to the new settings event models before the first production migration path is fixed.

---

# Suggested implementation sequence

1. Introduce shared validated value objects (`ModerationReason`, `XpAmount`, refined name/content wrappers)
2. Redesign `CustomCommand`
3. Redesign `XpIgnoredItem`
4. Redesign `XpHistory`
5. Improve `Sin` actor/reason modeling
6. Apply safe pre-production `Grimoire.Settings` improvements that still translate cleanly from obsolete rows
7. Incrementally harden migrated `Grimoire.Settings` replacements with factory methods, optional-reason modeling, and temporal invariants where step 6 did not already cover the needed work

# Rule of thumb

If a type currently needs a comment to explain which property combinations are legal, it is usually a sign that the model wants either:

- a value object, or
- a new variant type



