# Spec: CustomCommand Role Policy

> **Status: Implemented** — `IsUserAuthorized` encodes all four cases; no column or toggle required.

## Policy semantics

Custom command access is determined by the combination of `CustomCommandAllowRole` and
`CustomCommandDenyRole` entries attached to the command. Deny roles always act as a veto.

| Allow roles configured | Deny roles configured | User has allow role | User has deny role | Result |
|:---:|:---:|:---:|:---:|:---:|
| ❌ | ❌ | — | — | ✅ Everyone |
| ✅ | ❌ | ✅ | — | ✅ Allowed |
| ✅ | ❌ | ❌ | — | ❌ Denied |
| ❌ | ✅ | — | ❌ | ✅ Allowed |
| ❌ | ✅ | — | ✅ | ❌ Denied |
| ✅ | ✅ | ✅ | ❌ | ✅ Allowed |
| ✅ | ✅ | ✅ | ✅ | ❌ Denied — deny always vetoes |
| ✅ | ✅ | ❌ | ❌ | ❌ Denied — allow list is a gate |
| ✅ | ✅ | ❌ | ✅ | ❌ Denied |

Key rules:
- **No roles configured** → unrestricted; everyone can use the command.
- **Allow list only** → acts as an allowlist; anyone without an allow role is denied.
- **Deny list only** → acts as a blocklist; anyone without a deny role is allowed.
- **Both configured** → user must hold an allow role *and* must not hold any deny role.
  Deny roles are an unconditional veto; there is no override.

## How roles are set

`/Commands Learn` makes one list from its `permission_role_*` options (review C-2):

- `restricted_use: true` → the roles are allow roles, and at least one is required.
- `restricted_use: false` (the default) → the roles are deny roles. With none, everyone may use the command.

A command can't get both lists from `Learn`; the policy above still covers both.

Legacy rows follow `main`'s rule (review P-1): `ModelRedesign` makes a restricted command's roles allow roles and
an unrestricted command's roles deny roles. A restricted legacy command with no roles was usable by nobody on `main`
and is usable by everyone after the migration; size it with the query in `branch-review.md` section 2.

## Implementation

```csharp
public static bool IsUserAuthorized(DiscordMember? member, CustomCommand command)
{
    if (member is null) return false;
    var memberRoles = member.Roles.Select(static r => r.GetRoleId()).ToHashSet();

    var allowRoles = command.Roles.OfType<CustomCommandAllowRole>().Select(r => r.RoleId).ToHashSet();
    var denyRoles  = command.Roles.OfType<CustomCommandDenyRole>().Select(r => r.RoleId).ToHashSet();

    // No allow roles means everyone is allowed; a deny role always wins.
    return (allowRoles.Count == 0 || memberRoles.Overlaps(allowRoles)) && !memberRoles.Overlaps(denyRoles);
}
```

No schema column is required. The policy is encoded entirely in the presence or absence of
`CustomCommandAllowRole` / `CustomCommandDenyRole` rows.
