# Domain Model Migration Strategy

Date: 2026-04-18

## Migration checklist

- [x] Capture staged rollout guidance for the highest-priority redesign targets
- [x] Separate live `Grimoire.Domain` migration rules from pre-production `Grimoire.Settings` changes
- [x] Preserve the current `CustomCommand` role semantics and embed-color behavior
- [x] Keep the document focused on migration safety rather than ideal end-state modeling alone

## Purpose

This document is the migration-aware companion to:

- [`review-summary.md`](./review-summary.md)
- [`modeling-reference.md`](./modeling-reference.md)
- [`recommended-roadmap.md`](./recommended-roadmap.md)

Those documents describe the preferred model shape.
This one describes how to move from the current model to that shape without breaking existing data or making the upcoming settings migration harder than it needs to be.

## Shared migration rules

### Live `Grimoire.Domain` tables

For active `Grimoire.Domain` tables with existing rows, prefer this default sequence:

1. keep the current schema readable and writable
2. introduce the richer domain representation in code first, usually as an adapter, projection, or compatibility layer
3. add new columns, tables, or discriminators additively and in nullable or backward-compatible form
4. backfill from the current columns
5. update reads to tolerate both old and new shapes during rollout
6. switch writes to the new representation only after the read path is stable
7. remove legacy columns or rules only in a later cleanup migration

### Pre-production `Grimoire.Settings` tables

For `Grimoire.Settings`, the migration pressure is different:

- these tables are the replacement boundary for several obsolete settings-era entities in `Grimoire.Domain`
- they have not been migrated to production yet
- the obsolete-to-settings migration logic can still be changed in one coordinated pass

That means the default rule is:

> If a settings-side cleanup is safe and still translates clearly from the obsolete source data, prefer to do it now rather than after first production rollout.

This does **not** mean “rewrite everything.”
It means taking low-risk wins before the production boundary hardens.

## Cross-model safety rules

### Preserve current behavior before improving storage

For every redesign target, the first question is:

- can the new model be projected from the old storage without changing behavior?

If yes, do that first.
Only then decide whether storage also needs to change.

### Normalize ambiguous legacy values explicitly

When old rows use weak encodings such as:

- `null`
- `string.Empty`
- missing related rows
- overloaded booleans

prefer mapping them to an explicit compatibility state first instead of forcing an immediate destructive cleanup.

### Keep rollout decisions visible

Where a redesign still has an unresolved semantic choice, record it before the migration is started.
Examples:

- whether `XpHistory` should remain a signed ledger or become only positive typed entries
- whether blank moderation reasons stay blank, become `None`, or become invalid for new writes only

---

# 1. `CustomCommand`

Primary files:

- `Grimoire.Domain/CustomCommand.cs`
- `Grimoire/Configuration/CustomCommandConfiguration.cs`
- `Grimoire/Features/CustomCommands/GetCustomCommand.cs`

## Current storage and runtime behavior

The current model spreads behavior across:

- `HasMention`
- `HasMessage`
- `IsEmbedded`
- `RestrictedUse`
- nullable `EmbedColor`
- related role rows

The existing authorization behavior in `GetCustomCommand.IsUserAuthorized(...)` is:

- no roles + `RestrictedUse = false` → everyone may use the command
- no roles + `RestrictedUse = true` → no one may use the command
- roles + `RestrictedUse = true` → allow-list
- roles + `RestrictedUse = false` → deny-list

The existing embed behavior is:

- `IsEmbedded = true` with no `EmbedColor` is valid
- nullable `EmbedColor` already represents “no color chosen” well enough for a compatibility phase

## Recommended target shape

Target direction from the modeling docs:

- `CommandPresentation`
- `CommandInvocationBehavior`
- `CommandRolePolicy`
- `EmbedColorSetting.None` or `EmbedColorSetting.Specified(...)`

The important migration note is that the role policy is not just restricted vs unrestricted. It must preserve the current four-way meaning.

## Staged migration strategy

### Stage 1 — introduce a compatibility projection in code

Do **not** change the table first.
Instead, add code that maps the current row shape into a richer domain representation:

- `IsEmbedded = false` → plain text presentation
- `IsEmbedded = true` and `EmbedColor = null` → embedded presentation with `EmbedColorSetting.None`
- `IsEmbedded = true` and `EmbedColor != null` → embedded presentation with `EmbedColorSetting.Specified(...)`
- current role rows + `RestrictedUse` → one of:
  - `Everyone`
  - `NoOne`
  - `AllowListed(roles)`
  - `DenyListed(roles)`

This gives immediate domain clarity without schema churn.

### Stage 2 — stabilize reads on the richer model

Once the projection exists:

- update command execution and admin/read paths to reason about the richer model first
- keep persistence writing to the current columns
- add tests around the four role-policy cases and the embed-color-none case

This step proves the semantics before storage changes.

### Stage 3 — decide whether storage needs to change at all

After the compatibility model is stable, decide whether the table shape is still a problem.

A conservative option is to keep the current storage longer if:

- reads are clean
- writes can be translated back safely
- the booleans are no longer leaking past the persistence boundary

A more aggressive option is to add new persistence fields that align with the richer model, for example:

- a presentation discriminator
- an invocation mode
- a role-policy discriminator

If you do this, make them additive first.

### Stage 4 — backfill any new representation

If new fields are introduced, backfill from the old shape using deterministic rules:

- `IsEmbedded` + nullable `EmbedColor`
- `HasMention` + `HasMessage`
- `RestrictedUse` + related role rows

The `RestrictedUse` + roles mapping should be documented in the migration itself so it is unambiguous.

### Stage 5 — dual-read, then dual-write if needed

Preferred order:

1. read new representation when present
2. otherwise fall back to the old representation
3. once reads are stable, write both shapes if required
4. finally stop depending on the old shape

### Stage 6 — cleanup

Only after the richer representation has been proven:

- remove legacy columns or stop treating them as the source of truth
- simplify the EF configuration
- drop compatibility-only translation code

## Risks to avoid

- collapsing `NoOne` into `Everyone` when there are no roles
- treating nullable `EmbedColor` as invalid for embedded commands
- assuming role rows always mean allow-list behavior
- changing authorization semantics before compatibility tests exist

---

# 2. `XpIgnoredItem`

Primary files:

- `Grimoire.Settings/Domain/XpIgnoredItem.cs`
- `Grimoire.Settings/Configurations/XpIgnoredItemsConfiguration.cs`
- `Grimoire.Settings.Tests/Leveling/XpIgnoresTests.cs`

## Current storage and behavior

The current model is append-only and event-like:

- rows are keyed by `GuildId`, `Id`, and `SetAt`
- latest row wins for a given ignored target
- `Enabled = true` and `Enabled = false` together represent ignore / unignore history

The public model still leaks storage shape:

- base `Id` is generic
- the meaning of that ID depends on subtype
- EF ignores the semantic properties and persists only the generic `Id` plus discriminator

## Recommended target shape

- `IgnoredChannel(ChannelId ...)`
- `IgnoredMember(UserId ...)`
- `IgnoredRole(RoleId ...)`

Keep the append-only history behavior.
The redesign goal is about the public model, not about removing the event-history pattern.

## Staged migration strategy

### Stage 1 — change the public model first

Because this data is not yet in production, this is a strong candidate for fixing now.

Preferred change:

- give each subtype its own semantic identifier property directly
- stop making consumers reason about a generic `Id`

This may require adjusting configuration and tests, but it is still cheaper now than after production rollout.

### Stage 2 — preserve the append-only event semantics

Do **not** lose the current behavior that the tests already describe:

- ignore a member / channel / role
- disable the ignore later
- re-enable the ignore later
- compute the active set from the latest event per target

The new model should still preserve these semantics even if the stored columns become clearer.

### Stage 3 — decide on storage strategy

Reasonable options include:

- keep one table with a discriminator, but persist each semantic ID more explicitly
- keep one table with a compatibility `Id` column internally while exposing only semantic properties publicly
- split storage later only if there is a compelling operational reason

Because the data is not yet production-live, favor the option that gives the clearest public model with the simplest migration from obsolete rows.

### Stage 4 — update obsolete-to-settings migration logic

If the old obsolete entities are still the source for the first real migration into `Grimoire.Settings`, update that migration path now so it writes directly into the improved settings shape.

That avoids a second migration later just to fix a persistence shortcut that was already known up front.

## Risks to avoid

- accidentally removing the append-only enabled/disabled history
- coupling the redesign to a needlessly complex storage split
- leaving the generic `Id` in the public API just because it was convenient for EF

---

# 3. `XpHistory`

Primary files:

- `Grimoire.Domain/XpHistory.cs`
- `Grimoire/Configuration/XpHistoryConfiguration.cs`
- call sites in leveling features that write `Earned`, `Awarded`, and `Reclaimed` rows

## Current storage and behavior

The current entity stores:

- `Xp`
- `TimeOut`
- `Type`
- optional `AwarderId`
- `UserId`
- `GuildId`

Current issues:

- `Type` and `AwarderId` are coupled only by convention
- `Xp` is a raw `long`
- the timestamp property name `TimeOut` is persistence-shaped and unclear
- it is not yet explicit whether the ledger is intended to stay signed forever or whether each history variant should carry a non-negative amount with semantics in the type itself

## Recommended target shape

A richer variant model such as:

- `EarnedXp`
- `AwardedXp`
- `ReclaimedXp`
- `MigratedXp`
- `CreatedXp`

plus a clearer value object such as `XpAmount` if the chosen semantics support it.

## Decision to make before schema work

Pick one of these two strategies explicitly:

### Option A — keep a signed ledger internally

Use typed variants in the domain, but let storage keep a signed `Xp` amount if that remains operationally convenient.

Pros:

- less disruptive to current aggregation logic
- easier migration from existing rows

Cons:

- weaker invalid-state guarantees

### Option B — use positive typed amounts and put meaning in the variant

Use positive `XpAmount` values and let the history variant express whether XP was earned, reclaimed, migrated, or created.

Pros:

- stronger domain model
- better invalid-state prevention

Cons:

- may require more careful migration and query updates

Document this decision before changing the schema.

## Staged migration strategy

### Stage 1 — introduce a compatibility projection in code

Map existing rows into variant-like behavior without changing the table first:

- `Type = Awarded` + `AwarderId != null` → awarded entry
- `Type = Awarded` + `AwarderId = null` → invalid legacy case to detect and triage
- other types map directly to their variants

This is also the right place to decide whether `TimeOut` becomes a domain-level `OccurredAt` or similar while keeping the current column in storage.

### Stage 2 — add validation and anomaly reporting

Before schema changes, add checks for legacy rows such as:

- `Awarded` rows with missing `AwarderId`
- impossible type combinations if any exist
- suspicious XP signs relative to the chosen ledger strategy

That gives a real picture of the existing data before tightening constraints.

### Stage 3 — add new representation additively if needed

If the compatibility model is not enough, add new storage fields or a new table shape additively.

Examples:

- a clearer timestamp name while keeping the old column during transition
- a variant discriminator with more explicit semantics
- a normalized awarder requirement for award rows

### Stage 4 — backfill and dual-read

Backfill from existing rows, then:

- read the new representation when available
- otherwise read from the old columns

Only change writes after the dual-read path has been stable.

### Stage 5 — cleanup

After the new representation is stable:

- remove compatibility fallbacks
- tighten constraints on awarder presence where appropriate
- rename or retire old persistence-only names such as `TimeOut`

## Risks to avoid

- introducing a stronger `XpAmount` type before deciding whether signed values are still part of the ledger
- treating missing `AwarderId` as impossible without checking existing rows first
- renaming `TimeOut` in code and storage at the same time without a compatibility layer

---

# 4. `Sin` and related audit types

Primary files:

- `Grimoire.Domain/Sin.cs`
- `Grimoire.Domain/SinReasonHistory.cs`
- `Grimoire.Domain/Pardon.cs`
- `Grimoire.Domain/PublishedMessage.cs`

## Current storage and behavior

Current issues include:

- `Sin.ModeratorId` is nullable and implicitly doubles as “system actor”
- `SinReasonHistory.ModeratorId` is also nullable
- reasons are raw strings
- collections are mutable and aggregate rules are implicit
- `SinOn` is assigned at construction time, which is convenient but not ideal for replay-style or compatibility-heavy migrations

## Recommended target shape

Move toward:

- explicit actor modeling such as `ModeratorActor` vs `SystemActor`
- validated moderation reason value objects
- safer aggregate creation paths
- read-only exposure for related history collections

## Staged migration strategy

### Stage 1 — add compatibility mapping for actor identity

Before changing schema, define how current nullable IDs map into the richer model:

- `ModeratorId != null` → moderator actor
- `ModeratorId = null` → system actor or equivalent explicit compatibility actor

Do the same for `SinReasonHistory`.

This keeps legacy rows valid while making the meaning explicit in code.

### Stage 2 — decide how to treat blank or weak reasons

Before tightening reason rules, choose a policy for existing values such as:

- empty string
- whitespace-only string
- null if it appears in older data or manual inserts

Reasonable policies include:

- keep old blank rows readable, but reject blank reasons for new writes
- map blank legacy values to an explicit compatibility value such as `None`
- fully normalize blanks during backfill if the business rules support it

Record the rule before adding constraints.

### Stage 3 — introduce richer construction paths in code

Add factories or constructors that build:

- `Sin`
- `SinReasonHistory`
- `Pardon`

from the current persisted shape first.

This lets the domain surface improve before the table shape changes.

### Stage 4 — decide whether storage changes are needed

Some improvements may be possible without immediate schema changes.
Examples:

- actor adapters in code without changing nullable columns yet
- reason wrappers without immediately changing column types
- read-only public collection exposure while keeping EF-backed mutable storage internally

Only add new storage when the code-first compatibility layer is proven insufficient.

### Stage 5 — add and backfill any new actor or reason representation

If explicit actor or reason storage is introduced, do it additively:

- add new columns or a new related representation
- backfill from nullable `ModeratorId` and raw `Reason`
- dual-read during the transition
- only then switch writes fully to the new representation

### Stage 6 — cleanup

After the richer actor and reason model is stable:

- tighten nullability rules where appropriate
- remove compatibility-only mappings
- simplify aggregate construction and EF configuration

## Risks to avoid

- breaking old rows by making reasons strict before deciding what to do with blank legacy values
- assuming `ModeratorId = null` always means the same business concept without checking callers
- trying to redesign the whole aggregate and schema in one release

---

## Recommended sequence across all four migrations

1. `CustomCommand` compatibility projection and role-policy tests
2. `XpIgnoredItem` cleanup before first production settings rollout
3. `XpHistory` semantic decision on signed vs typed amounts, then compatibility projection
4. `Sin` actor/reason compatibility mapping
5. only after the compatibility layers are stable, decide which storage changes are still worth making

## Final rule of thumb

If a redesign can be delivered in two steps, prefer:

1. make the domain meaning explicit in code first
2. make storage match later only when the code-first change proves valuable enough to justify the schema work

