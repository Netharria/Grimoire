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

## Implementation

```csharp
public static bool IsUserAuthorized(DiscordMember? member, CustomCommand command)
{
    if (member is null) return false;
    var memberRoles = member.Roles.Select(static r => r.GetRoleId()).ToHashSet();

    var allowRoles = command.Roles.OfType<CustomCommandAllowRole>().Select(r => r.RoleId).ToHashSet();
    var denyRoles  = command.Roles.OfType<CustomCommandDenyRole>().Select(r => r.RoleId).ToHashSet();

    var hasAllow = allowRoles.Count > 0 && memberRoles.Overlaps(allowRoles);
    var hasDeny  = denyRoles.Count  > 0 && memberRoles.Overlaps(denyRoles);

    if (allowRoles.Count == 0 && denyRoles.Count == 0) return true;
    if (allowRoles.Count == 0) return !hasDeny;
    if (denyRoles.Count  == 0) return hasAllow;
    return hasAllow && !hasDeny;
}
```

No schema column is required. The policy is encoded entirely in the presence or absence of
`CustomCommandAllowRole` / `CustomCommandDenyRole` rows.
