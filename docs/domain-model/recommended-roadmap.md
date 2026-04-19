# Recommended Domain Model Roadmap

Date: 2026-04-18

## Roadmap checklist

- [x] Group reviewed models by recommended action
- [x] Reflect live-data migration constraints in `Grimoire.Domain`
- [x] Treat `Grimoire.Settings` as the active replacement boundary for migrated settings models
- [x] Use the pre-production `Grimoire.Settings` window to pull forward safe schema/model improvements
- [x] Preserve explicit `embed color = none` semantics for `CustomCommand`
- [x] Preserve the current allow-list / deny-list role semantics for `CustomCommand`
- [x] Keep the roadmap short enough to use as a practical prioritization tool

## How to read this roadmap

This roadmap is not a ranking of code quality alone. It is a combined view of:

- domain-model quality
- migration cost
- persistence risk
- whether a model is already the migrated replacement for an obsolete one
- whether the new settings-side model has reached production yet

The buckets mean:

- **Redesign** — the public model still hides important invariants or leaks persistence shape enough that a deeper redesign is justified.
- **Harden Incrementally** — the current model is already a reasonable replacement boundary; improve it with value objects, factories, invariant checks, and any safe pre-production schema cleanup instead of another broad rewrite.
- **Leave Alone for Now** — the current shape is good enough relative to its migration cost and should not be a near-term focus.

---

## Redesign

These are the types where a deeper model change still looks worth the migration and implementation effort.

### `Grimoire.Domain/CustomCommand.cs`

Why it belongs here:

- multiple booleans encode business state
- presentation and invocation semantics are spread across flags
- restrictions are only partially represented in the type system
- the current `RestrictedUse` + role-row combination actually encodes four behaviors, not two
- it is still an active `Grimoire.Domain` model, so the target shape matters long-term

What to redesign toward:

- explicit presentation variants
- explicit invocation behavior
- explicit role access policy
- explicit embed-color choice with support for `none`

Role-policy target:

- `Everyone`
- `NoOne`
- `AllowListed(roles)`
- `DenyListed(roles)`

Specific note:

If the command is an embed, the color should not be modeled as mandatory. The better target shape is an explicit choice such as:

- `EmbedColorSetting.None`
- `EmbedColorSetting.Specified(CustomCommandEmbedColor)`

Migration note:

Because the table contains live data, treat this as a staged migration:

1. keep the current columns readable
2. map the richer domain model over the current storage first
3. add any new persistence representation additively
4. backfill and transition gradually
5. remove legacy state later

### `Grimoire.Settings/Domain/XpIgnoredItem.cs`

Why it still belongs here:

- although it is already in `Grimoire.Settings`, the public model still exposes the persistence-shaped generic `Id`
- the semantic meaning of the ignored target depends on runtime subtype interpretation
- this is the clearest settings-side example where the replacement model still leaks storage concerns

What to redesign toward:

- `IgnoredChannel(ChannelId ... )`
- `IgnoredMember(UserId ... )`
- `IgnoredRole(RoleId ... )`

Migration note:

This is an especially good candidate to fix before first production rollout. It does not need the same caution level as live `Grimoire.Domain` tables, but the redesign should still be staged carefully because settings history and tests already rely on the current append-only behavior.

### `Grimoire.Domain/XpHistory.cs`

Why it belongs here:

- `Type` and `AwarderId` are coupled implicitly
- business meaning is spread into consumers
- numeric XP values are still primitive and unconstrained

What to redesign toward:

- variant records such as `EarnedXp`, `AwardedXp`, `ReclaimedXp`, `MigratedXp`, `CreatedXp`
- a constrained `XpAmount` value object if negative values are invalid

Migration note:

This is a live-table redesign candidate, so favor additive persistence changes and a compatibility window.

### `Grimoire.Domain/Sin.cs` and related audit types

Affected files:

- `Grimoire.Domain/Sin.cs`
- `Grimoire.Domain/SinReasonHistory.cs`
- `Grimoire.Domain/Pardon.cs`
- `Grimoire.Domain/PublishedMessage.cs`

Why they belong here:

- actor semantics are nullable or implicit
- reasons are still raw strings
- aggregate invariants are likely important but not modeled directly

What to redesign toward:

- explicit actor types
- validated reason value objects
- safer aggregate construction paths

Migration note:

This is another area where the end state should improve, but the rollout should preserve old rows throughout the transition.

---

## Harden Incrementally

These types are already the newer replacement boundary or otherwise close enough to a good model that they should usually be improved without a broad redesign. Because the settings-side rollout has not hit production yet, safe cleanup in this bucket should happen earlier than it otherwise would.

### `Grimoire.Settings/Domain/ChannelLock.cs`
### `Grimoire.Settings/Domain/ThreadLock.cs`

Why they belong here:

- they already model events or state transitions more clearly than the obsolete domain-era predecessors
- the lock workflows already behave like append-only histories
- the biggest remaining issues are invariant enforcement and stringly typed reasons, not foundational shape problems

Recommended improvements:

- validated reason value objects or explicit optional reason types
- factory methods for creation
- temporal invariant checks such as `EndTime > SetAt`
- optional renaming to event-focused names like `ChannelLocked` / `ChannelUnlocked`
- prefer doing the low-risk schema and migration cleanup before the first production settings rollout

### `Grimoire.Settings/Domain/MessageLogChannelOverride.cs`
### `Grimoire.Settings/Domain/SpamFilterOverride.cs`

Why they belong here:

- these are already small, focused replacement models
- the remaining debt is mostly around primitive state and construction rules, not a broken overall shape

Recommended improvements:

- use validated value objects where rules exist
- consider whether `Inherit` / `Always` / `Never` patterns should remain enums or become small variants
- use factory methods if business validation becomes more complex
- take any schema cleanup now if the obsolete settings-era rows still map cleanly into the revised shape

### `Grimoire.Settings/Domain/Mute.cs`
### `Grimoire.Settings/Domain/Reward.cs`

Why they belong here:

- they are active replacement models, but still somewhat thin
- the likely wins are better value objects and temporal/numeric validation, not necessarily a major structural rewrite first

Recommended improvements:

- constrain time and numeric values
- introduce validated message/reason wrappers where appropriate
- add safer construction paths
- take any field-level schema cleanup now if the obsolete-to-settings migration can still be updated in one pass

### `Grimoire.Settings/Domain/GuildSetting.cs`

Why it belongs here:

- the variant-style shape is already a positive step
- the main weakness is the stringly typed custom value rather than the overall state model

Recommended improvements:

- validated wrapper for custom values
- more typed per-setting adapters where the highest-value settings need stronger guarantees
- consider stronger typing before first production migration if it does not complicate the obsolete-data import path

---

## Leave Alone for Now

These models look good enough relative to their current risk and migration cost that they should not be near-term priorities.

### `Grimoire.Domain/MessageHistory.cs`

Why it belongs here:

- it already uses a good variant-style shape
- it is one of the clearest examples of the direction the rest of the domain should move toward
- remaining improvements are comparatively small

### `Grimoire.Domain/StronglyTypedIds.cs`

Why it belongs here:

- the strongly typed ID approach is already a net positive
- follow-up work should be evolutionary rather than disruptive
- any future refinement can be done as part of targeted model work elsewhere

### Most already-migrated obsolete replacements in `Grimoire.Settings`

Why they belong here by default:

- they already represent progress away from the older obsolete `Grimoire.Domain` settings-era entities
- reopening all of them at once would risk losing migration focus and architectural clarity
- the default action should be to preserve the replacement boundary and improve it surgically, while still taking safe pre-production fixes where the migration path is obvious

This “leave alone” guidance is not permanent. It means “do not make this a top-level redesign project right now.”

---

## Suggested sequence of work

1. Standardize reusable validated value-object patterns
2. Redesign `CustomCommand` with a staged migration plan
3. Redesign `XpIgnoredItem`
4. Redesign `XpHistory`
5. Improve `Sin` and related audit models
6. Apply safe pre-production cleanup across `Grimoire.Settings` replacement types
7. Incrementally harden the remaining `Grimoire.Settings` replacement types
8. Revisit lower-priority models only after the higher-risk redesigns are stable

## Final rule of thumb

If a model is already the migrated replacement for an obsolete predecessor, the default question should be:

> "Can this be hardened safely without reopening the whole design?"

If the answer is yes, prefer incremental hardening.

If the model has not reached production yet, add a second question:

> "Is there a safe cleanup I should do now before the first production migration makes this shape harder to change?"

If the answer is no because the public model still leaks storage concerns or hides critical invariants, promote it back into the redesign bucket.

