# Domain Model Review Summary

Date: 2026-04-18

## Review checklist

- [x] Review `Grimoire.Domain` entities and value objects
- [x] Review `Grimoire.Settings/Domain` entities and setting/event types
- [x] Review EF-facing configuration where it affects the public model
- [x] Identify invalid-state risks
- [x] Identify places where modern C# and more functional modeling would help
- [x] Record prioritized recommendations for follow-up refactors

## Executive summary

The codebase already has a strong foundation:

- strongly typed identifiers in `Grimoire.Domain/StronglyTypedIds.cs`
- broad use of records and init-only state
- a few good variant-style models already in place, especially `Grimoire.Domain/MessageHistory.cs` and several event types under `Grimoire.Settings/Domain`

The main weakness is that many domain types are still shaped as EF-friendly property bags instead of domain-first types with construction-time invariants.

In practice, this means:

- invalid combinations are still representable
- raw strings and primitive values carry too much business meaning
- boolean flags often stand in for richer state
- public models sometimes reflect persistence shortcuts more than domain intent

One important constraint changes how these recommendations should be applied: `Grimoire.Domain` tables already contain live data that must migrate successfully across schema changes. That does not change the end-state recommendation, but it does change the delivery strategy. For live tables, prefer staged schema evolution over one-shot rewrites.

In practice, that means:

- favor additive migrations first
- backfill new columns or tables from existing data
- keep read/write compatibility during a transition window when practical
- remove legacy columns only after the new representation is proven and fully populated

Another important constraint changes the `Grimoire.Settings` side of the review: many `Grimoire.Settings/Domain` types are already the migrated successors to obsolete models still visible through `Grimoire/GrimoireDbContext.cs`, but those settings models have not been migrated to production yet. That means the newer boundary should still be preserved conceptually, while safe schema and model improvements that translate cleanly from the obsolete data should be taken now rather than deferred.

In practice, that means:

- keep full redesign pressure on models that still leak persistence shape or hide important invariants
- prefer front-loading safe changes before first production cutover when the migration from obsolete rows is straightforward
- use incremental hardening for settings replacements that are already clearly better than their obsolete predecessors, but do that hardening now when it reduces future migration cost
- avoid reopening stable replacement models only when the remaining design debt is minor enough that changing it now would create more churn than safety

## What is already working well

### Strongly typed IDs

`Grimoire.Domain/StronglyTypedIds.cs` already prevents many accidental ID mixups:

- `UserId`
- `GuildId`
- `ChannelId`
- `RoleId`
- `MessageId`
- `ModeratorId`

This is worth preserving and extending.

### Variant-style history modeling

`Grimoire.Domain/MessageHistory.cs` is one of the cleanest areas of the model:

- `MessageHistoryEntry`
- `MessageHistoryContentEntry`
- `MessageCreatedEntry`
- `MessageEditedEntry`
- `MessageDeletedEntry`
- `MessageDeletedByModeratorEntry`

This already resembles a functional sum type and is preferable to a single broad record with many nullable fields.

### State/event modeling in settings

Several settings-domain types are already headed in the right direction:

- `Grimoire.Settings/Domain/GuildSetting.cs`
- `Grimoire.Settings/Domain/ChannelLock.cs`
- `Grimoire.Settings/Domain/ThreadLock.cs`

These types already separate states or events into variants rather than flattening everything into one record.

## Main issues

### 1. `required init` does not make invalid states impossible

Many types use `required` effectively for presence, but not for correctness.

Examples:

- `Grimoire.Domain/CustomCommand.cs`
- `Grimoire.Domain/Pardon.cs`
- `Grimoire.Domain/PublishedMessage.cs`
- `Grimoire.Settings/Domain/Reward.cs`
- `Grimoire.Settings/Domain/SpamFilterOverride.cs`
- `Grimoire.Settings/Domain/MessageLogChannelOverride.cs`

Typical problems:

- empty or whitespace-only string values are allowed
- temporal relationships are not enforced
- flags and nullable properties can be combined in nonsensical ways

### 2. Several domain concepts are still raw strings

These should likely be value objects instead of primitive strings:

- `Reason` in `Grimoire.Domain/Pardon.cs`
- `Reason` in `Grimoire.Domain/SinReasonHistory.cs`
- `Reason` in `Grimoire.Settings/Domain/ChannelLock.cs`
- `Reason` in `Grimoire.Settings/Domain/ThreadLock.cs`
- `RewardMessage` in `Grimoire.Settings/Domain/Reward.cs`
- `Value` in `Grimoire.Settings/Domain/GuildSetting.cs`
- `SystemId` and `MemberId` in `Grimoire.Domain/ProxiedMessageLink.cs`

### 3. Boolean flags are encoding state that should be types

The clearest example is `Grimoire.Domain/CustomCommand.cs`:

- `HasMention`
- `HasMessage`
- `IsEmbedded`
- `RestrictedUse`

Those booleans currently encode business meaning that would be better expressed by a variant or nested value object.

### 4. Enum + nullable dependent field patterns are hiding invariants

Examples:

- `Grimoire.Domain/XpHistory.cs`
- `Grimoire.Domain/Sin.cs`
- `Grimoire.Domain/SinReasonHistory.cs`

These shapes usually force consumers to remember rules like “this property must be set only when that enum equals X”. A variant model is safer and easier to reason about.

### 5. Some types are persistence-shaped rather than domain-shaped

The clearest example is `Grimoire.Settings/Domain/XpIgnoredItem.cs`.

The base type stores a generic `ulong Id`, and derived types reinterpret it as:

- `ChannelId`
- `UserId`
- `RoleId`

That is efficient for storage and discriminator-based mapping, but weak as a public domain model.

### 6. Mutable collection exposure leaks implementation concerns

Examples:

- `Message.Attachments`
- `Message.MessageHistory`
- `Sin.Pardons`
- `Sin.PublishMessages`
- `Sin.ReasonHistory`
- `CustomCommand.Roles`

These are all exposed as mutable collections. The domain model would be clearer with read-only exposure and explicit mutation through methods or event creation.

## Prioritized recommendations

## P1 — redesign `CustomCommand`

Affected file:

- `Grimoire.Domain/CustomCommand.cs`

Why it is priority 1:

- multiple booleans encode state
- invalid combinations are possible
- content and embed semantics are weakly modeled
- restriction semantics are partially split between flags and collection state

Recommendation:

- model presentation as variants, e.g. plain text vs embed
- model invocation behavior as a meaningful enum or closed set of variants
- model role access as an explicit policy rather than a simple restricted/unrestricted flag
- capture the four current behaviors directly: `Everyone`, `NoOne`, `AllowListed(roles)`, and `DenyListed(roles)`
- validate `CustomCommandName`
- model embed color as an explicit choice such as `None` or `Specified(CustomCommandEmbedColor)` rather than assuming every embed has a color
- for the live `CustomCommand` table, migrate in stages rather than replacing the shape in a single schema change

This recommendation matches the current runtime behavior in `Grimoire/Features/CustomCommands/GetCustomCommand.cs`:

- no roles + `RestrictedUse = false` means everyone is authorized
- no roles + `RestrictedUse = true` means no one is authorized
- roles + `RestrictedUse = true` means the roles are an allow-list
- roles + `RestrictedUse = false` means the roles are a deny-list

## P2 — redesign `XpIgnoredItem`

Affected files:

- `Grimoire.Settings/Domain/XpIgnoredItem.cs`
- `Grimoire.Settings/Configurations/XpIgnoredItemsConfiguration.cs`
- `Grimoire.Settings.Tests/Leveling/XpIgnoresTests.cs`

Why it is priority 2:

- the current base `Id` is a persistence shortcut
- the semantic meaning of an ignored target depends on runtime type
- the public model is less expressive than the tests and business rules imply

Recommendation:

- move the semantic identifier to each subtype directly
- keep append-only event history if desired, but let each subtype own its domain meaning
- make enabled/disabled transitions explicit in names or events if that improves clarity

`XpIgnoredItem` remains a redesign target even with the newer settings boundary in place, because it still exposes a persistence-shaped generic `Id` in the public model.

## P3 — redesign `XpHistory`

Affected file:

- `Grimoire.Domain/XpHistory.cs`

Why it is priority 3:

- `Type` and `AwarderId` are coupled implicitly
- `Xp` is just a raw number
- business meaning is spread into consumers

Recommendation:

- split into specific record variants such as earned, awarded, reclaimed, migrated, created
- introduce a constrained `XpAmount` value object if negative values are invalid

## P4 — improve `Sin` and related audit types

Affected files:

- `Grimoire.Domain/Sin.cs`
- `Grimoire.Domain/SinReasonHistory.cs`
- `Grimoire.Domain/Pardon.cs`
- `Grimoire.Domain/PublishedMessage.cs`

Why it is priority 4:

- actor semantics are nullable today
- reasons are raw strings
- several likely aggregate invariants are not represented in the type system

Recommendation:

- model actor explicitly, e.g. moderator actor vs system actor
- move reason text into a validated value object
- consider named factories for different `SinType` creation paths

## P5 — take safe pre-production improvements across migrated settings replacements

Affected files:

- `Grimoire.Settings/Domain/ChannelLock.cs`
- `Grimoire.Settings/Domain/ThreadLock.cs`
- `Grimoire.Settings/Domain/MessageLogChannelOverride.cs`
- `Grimoire.Settings/Domain/SpamFilterOverride.cs`
- `Grimoire.Settings/Domain/Mute.cs`
- `Grimoire.Settings/Domain/Reward.cs`
- `Grimoire.Settings/Services/SettingsModule.Moderation.Locks.cs`

Why it matters:

- these types are already the migrated replacements for obsolete settings-era models in `Grimoire.Domain`
- they have not been migrated to production yet, so this is the cleanest point to tighten schema and model rules that still migrate safely from the obsolete source data
- the current lock implementations already behave like append-only event histories
- “active lock” is derived by selecting the latest event per channel/guild pair
- unlock events currently use `Reason = string.Empty`
- temporal rules like `EndTime > SetAt` are not enforced in the model

Recommendation:

- preserve the current replacement boundary unless a model still leaks important persistence details
- prefer making safe structural fixes now, before first production rollout, when obsolete-to-settings migration logic can still be adjusted in one coordinated pass
- improve these types incrementally with validated value objects, factory methods, and temporal invariants
- keep the append-only event pattern for lock histories where that remains part of the feature design
- rename and model the events more explicitly if desired (`ChannelLocked` / `ChannelUnlocked`)
- replace raw string reasons with a value object or explicit optional reason type

## Concrete modeling guidance

### Prefer validated value objects

Good candidates include:

- `ModerationReason`
- `MessageContent`
- `Username`
- `Nickname`
- `CustomCommandName`
- `CustomCommandEmbedColor`
- `RewardMessage`
- `InviteCode`
- `InviteUrl`
- `XpAmount`
- `Level`
- `Rank`

### Prefer closed variants over boolean combinations

Use:

- `abstract record` + sealed variants
- exhaustive pattern matching in consumers

Avoid:

- multiple booleans encoding mutually-dependent state
- enum values that require separate nullable companion properties

### Prefer safe construction over wide object initializers

For simple immutable types:

- use primary constructors

For types with invariants:

- use static factories like `TryCreate` or `Create`
- validate string normalization, numeric ranges, and temporal ordering in one place

### Prefer read-only collection exposure

When EF Core requires mutable storage, prefer:

- a private backing collection
- public `IReadOnlyList<T>` or `IReadOnlyCollection<T>` exposure

## Persistence notes

### `GrimoireDbContext`

`Grimoire/GrimoireDbContext.cs` still includes several obsolete settings-related tables and types. The review assumes the long-term direction is to keep the old shapes isolated and continue moving active settings behavior into `Grimoire.Settings`.

That includes the obsolete `Lock` model in `Grimoire.Domain/Obsolete/Lock.cs`, which is a good example of the older, more weakly-typed style being replaced by the newer settings module event model.

The same replacement pattern applies to several other obsolete sets still referenced by `Grimoire/GrimoireDbContext.cs`, including the old settings models for ignored items, mutes, rewards, spam filter overrides, and message log overrides. In those areas, `Grimoire.Settings` should generally be treated as the active model boundary.

For non-obsolete `Grimoire.Domain` tables with existing data, the recommendation is to treat schema evolution as an explicit part of the design:

1. add the new representation in a backward-compatible form
2. backfill from the current columns
3. update reads to tolerate both shapes during rollout
4. switch writes to the new representation
5. drop legacy columns only in a later migration

### `SettingsDbContext`

`Grimoire.Settings/SettingsDbContext.cs` already acts like a home for append-only configuration/event histories:

- `GuildSettings`
- `XpIgnoredItems`
- `ChannelLocks`
- `ThreadLocks`
- and other settings tables

That is compatible with a more domain-first design. Because these settings tables have not yet been migrated to production, the right default here is to take safe schema improvements now when the obsolete-to-settings migration can still be changed without live production churn. The main exceptions remain the same conceptually: some types only need incremental hardening, while others still expose obvious persistence shortcuts, such as `XpIgnoredItem`, or stringly-typed state, such as parts of `GuildSetting`.

## Suggested refactor order

1. Standardize validated value-object patterns
2. Redesign `CustomCommand`
3. Redesign `XpIgnoredItem`
4. Redesign `XpHistory`
5. Improve `Sin` actor/reason modeling
6. Apply safe pre-production `Grimoire.Settings` improvements with factories, temporal invariants, validated value objects, and any low-risk schema cleanups that still migrate cleanly from obsolete rows
7. Replace public mutable collection exposure with read-only views where it improves correctness without fighting EF unnecessarily

## Cross-cutting rule of thumb

When deciding between two shapes, prefer the one that makes misuse impossible at construction time, even if the EF Core configuration becomes slightly more explicit.

For live tables, add a second rule: prefer the migration path that keeps old data valid and observable throughout the transition, even if the final cleanup takes more than one release.



