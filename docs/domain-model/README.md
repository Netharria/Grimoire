# Domain Model Notes

This folder captures the April 2026 review of the domain model across `Grimoire.Domain` and `Grimoire.Settings`.

## Files

- [`review-summary.md`](./review-summary.md) — decision-oriented summary of the review, prioritized recommendations, and affected files.
- [`modeling-reference.md`](./modeling-reference.md) — concrete modeling sketches showing how the highest-impact types could be redesigned.
- [`recommended-roadmap.md`](./recommended-roadmap.md) — practical next-step grouping of models into redesign, incremental hardening, and leave-alone-for-now buckets.
- [`migration-strategy.md`](./migration-strategy.md) — staged rollout guidance for the highest-priority redesigns, with separate rules for live `Grimoire.Domain` tables and pre-production `Grimoire.Settings` tables.
- [`error-result-validation-strategy.md`](./error-result-validation-strategy.md) — native C# plan for shared `Error`, `Validation<T>`, richer `Result<T>`, and how those should relate to `SettingsResult` without collapsing all result semantics into one type.

## Scope

Projects reviewed:

- `Grimoire.Domain`
- `Grimoire.Settings`

Primary goals:

- use modern C# / .NET syntax
- make invalid states unrepresentable
- use functional style where practical
- reduce boilerplate and verbose state modeling

## Highest-priority redesign targets

1. `Grimoire.Domain/CustomCommand.cs`
2. `Grimoire.Settings/Domain/XpIgnoredItem.cs`
3. `Grimoire.Domain/XpHistory.cs`
4. `Grimoire.Domain/Sin.cs`
5. safe pre-production `Grimoire.Settings` schema tightening, starting with `GuildSetting`, lock reason/invariant cleanup, and other models that still translate cleanly from obsolete predecessors

## Notes

These documents are intentionally domain-first. They describe the recommended shape of the model, even when the current EF Core mapping may need to be adjusted to support it.

For `Grimoire.Domain` in particular, these notes should now be read as migration-aware guidance rather than big-bang rewrite guidance. Existing rows must survive schema evolution, so changes to live tables should favor staged, additive migrations with backfills and compatibility periods over immediate destructive redesigns.

For `Grimoire.Settings`, many of the types are already the migrated replacements for obsolete models that still exist in `Grimoire.Domain`. Because those settings models have not been migrated to production yet, this is the best time to take safe schema and model improvements that still translate cleanly from the obsolete source data. The goal is to preserve the migration gains while being more aggressive about fixing persistence leaks and weak invariants before the first production cutover.

For `CustomCommand`, the role policy should be read as a four-way interpretation of the current `RestrictedUse` + role-row state rather than a simple restricted/unrestricted toggle:

- no roles + `RestrictedUse = false` → everyone may use the command
- no roles + `RestrictedUse = true` → no one may use the command
- roles + `RestrictedUse = true` → allow-list
- roles + `RestrictedUse = false` → deny-list




