# Spec: CustomCommand Role Policy

> **Status: Implemented** — `RolePrecedence` enum added; `IsUserAuthorized` rewritten with correct mixed-role semantics; `Revert` passes `RolePrecedence` through; factories accept optional `rolePrecedence` parameter.

## Problem

The current `CustomCommand` model uses `CustomCommandAllowRole` and `CustomCommandDenyRole`
subtypes to encode role access policy. This correctly represents three states (Everyone,
AllowListed, DenyListed) but has no mechanism for resolving conflicts when a user holds both an
allow-listed and a deny-listed role simultaneously.

The external UI does not currently expose mixed role lists, but the feature is planned. The domain
model should support it from the start to avoid a schema migration later.

---

## New type: `RolePrecedence`

Add `RolePrecedence` to `Grimoire.Domain/CustomCommand.cs` (or a dedicated file if preferred).

```csharp
/// <summary>
/// Determines which role type wins when a user holds both an allow-listed and a deny-listed role
/// on the same custom command.
/// </summary>
public enum RolePrecedence
{
    /// <summary>
    /// Deny roles cancel allow roles. A user with both an allow role and a deny role is denied.
    /// This is the default.
    /// </summary>
    DenyOverride,

    /// <summary>
    /// Allow roles cancel deny roles. A user with both an allow role and a deny role is allowed.
    /// </summary>
    AllowOverride
}
```

---

## Policy semantics

When both `CustomCommandAllowRole` and `CustomCommandDenyRole` entries exist on a command:

| Has AllowRole | Has DenyRole | DenyOverride | AllowOverride |
|:---:|:---:|:---:|:---:|
| ✅ | ✅ | ❌ denied | ✅ allowed |
| ✅ | ❌ | ✅ allowed | ✅ allowed |
| ❌ | ✅ | ❌ denied | ❌ denied |
| ❌ | ❌ | ❌ denied* | ❌ denied* |

*When allow roles exist on the command, a user with none of them is always denied regardless of
precedence — the allow list acts as a gate first.

When only deny roles exist (no allow roles), any user without a deny role is allowed. When only
allow roles exist (no deny roles), any user without an allow role is denied. These cases are
unaffected by `RolePrecedence`.

---

## Changes to `CustomCommand.cs`

Add `RolePrecedence` to the base `CustomCommand` record:

```csharp
[UsedImplicitly]
public abstract record CustomCommand
{
    public required CustomCommandName Name { get; init; }
    public required GuildId GuildId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required CustomCommandContent Content { get; init; }
    public ModeratorId? ModeratorId { get; init; }
    public RolePrecedence RolePrecedence { get; init; } = RolePrecedence.DenyOverride;
    public ICollection<CustomCommandRole> Roles { get; protected init; } = [];
}
```

Update both `Create` factories to accept an optional `RolePrecedence` parameter:

```csharp
public sealed record TextCustomCommand : CustomCommand
{
    public static Validation<CustomCommand> Create(
        CustomCommandName name,
        GuildId guildId,
        CustomCommandContent content,
        ICollection<CustomCommandRole> customCommandRoles,
        ModeratorId? moderatorId,
        RolePrecedence rolePrecedence = RolePrecedence.DenyOverride)
    {
        var now = DateTimeOffset.UtcNow;
        return Validation<CustomCommand>.Succeed(new TextCustomCommand
        {
            Name = name,
            GuildId = guildId,
            CreatedAt = now,
            Content = content,
            ModeratorId = moderatorId,
            RolePrecedence = rolePrecedence,
            Roles = customCommandRoles.Select<CustomCommandRole, CustomCommandRole>(r => r switch
            {
                CustomCommandAllowRole allow => allow with { CreatedAt = now },
                CustomCommandDenyRole deny   => deny  with { CreatedAt = now },
                _ => throw new UnreachableException()
            }).ToList()
        });
    }
}

public sealed record EmbedCustomCommand : CustomCommand
{
    public CustomCommandEmbedColor? EmbedColor { get; init; }

    public static Validation<CustomCommand> Create(
        CustomCommandName name,
        GuildId guildId,
        CustomCommandContent content,
        CustomCommandEmbedColor? embedColor,
        ICollection<CustomCommandRole> customCommandRoles,
        ModeratorId? moderatorId,
        RolePrecedence rolePrecedence = RolePrecedence.DenyOverride)
    {
        var now = DateTimeOffset.UtcNow;
        return Validation<CustomCommand>.Succeed(new EmbedCustomCommand
        {
            Name = name,
            GuildId = guildId,
            CreatedAt = now,
            Content = content,
            EmbedColor = embedColor,
            ModeratorId = moderatorId,
            RolePrecedence = rolePrecedence,
            Roles = customCommandRoles.Select<CustomCommandRole, CustomCommandRole>(r => r switch
            {
                CustomCommandAllowRole allow => allow with { CreatedAt = now },
                CustomCommandDenyRole deny   => deny  with { CreatedAt = now },
                _ => throw new UnreachableException()
            }).ToList()
        });
    }
}
```

---

## EF Core configuration

Add `RolePrecedence` to the `CustomCommand` entity configuration. A value conversion from
`RolePrecedence` to `string` (or `int`) is recommended for readability in the database.

```csharp
builder.Property(x => x.RolePrecedence)
    .HasConversion<string>()
    .HasDefaultValue(RolePrecedence.DenyOverride)
    .HasColumnName("RolePrecedence");
```

---

## Migration

A single EF Core migration is required to add the `RolePrecedence` column to the `CustomCommands`
table with a default of `DenyOverride` for all existing rows.

```sql
ALTER TABLE "CustomCommands"
    ADD COLUMN "RolePrecedence" text NOT NULL DEFAULT 'DenyOverride';
```

No data migration is required. All existing commands correctly default to `DenyOverride`.

---

## Call site changes

### Authorization logic

The command authorization logic (currently in `GetCustomCommand.IsUserAuthorized` or equivalent)
must be updated to implement the mixed-role resolution semantics described in the policy table
above. The current logic that handles the pure AllowListed and DenyListed cases remains correct;
only the mixed case needs to be added.

```csharp
bool IsUserAuthorized(CustomCommand command, IReadOnlySet<RoleId> userRoles)
{
    var allowRoles = command.Roles.OfType<CustomCommandAllowRole>().Select(r => r.RoleId).ToHashSet();
    var denyRoles  = command.Roles.OfType<CustomCommandDenyRole>().Select(r => r.RoleId).ToHashSet();

    var hasAllow = allowRoles.Count > 0 && userRoles.Overlaps(allowRoles);
    var hasDeny  = denyRoles.Count  > 0 && userRoles.Overlaps(denyRoles);

    // Pure modes (unchanged behaviour)
    if (allowRoles.Count == 0 && denyRoles.Count == 0) return true;   // Everyone
    if (allowRoles.Count == 0) return !hasDeny;                        // DenyListed only
    if (denyRoles.Count  == 0) return hasAllow;                        // AllowListed only

    // Mixed mode — apply precedence
    return command.RolePrecedence switch
    {
        RolePrecedence.DenyOverride  => hasAllow && !hasDeny,
        RolePrecedence.AllowOverride => hasAllow || !hasDeny,
        _ => throw new UnreachableException()
    };
}
```

### Slash command handlers

Any handler that creates or edits a custom command should accept an optional `RolePrecedence`
parameter and pass it through to the `Create` factory. Until the UI exposes the setting, callers
can omit it and the default (`DenyOverride`) will apply.
