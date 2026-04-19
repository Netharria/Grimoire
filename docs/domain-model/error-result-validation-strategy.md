# Native C# Error, Result, and Validation Strategy

Date: 2026-04-18

## Strategy checklist

- [x] Define the proposed native C# building blocks
- [x] Clarify what should be unified and what should stay specialized
- [x] Capture a phased implementation plan for later work
- [x] Identify good first adopters and risky follow-up areas
- [x] Record compatibility guidance for existing consumers and tests

## Purpose

This document describes a native C# approach for introducing shared error, validation, and result primitives across `Grimoire.Domain` and `Grimoire.Settings`.

It is intentionally implementation-oriented and should be read alongside:

- [`review-summary.md`](./review-summary.md)
- [`modeling-reference.md`](./modeling-reference.md)
- [`recommended-roadmap.md`](./recommended-roadmap.md)
- [`migration-strategy.md`](./migration-strategy.md)

The goal is to improve functional-style modeling and construction-time safety **without** introducing a third-party functional library.

## Goals

The proposed design should support the existing repo goals:

- modern C# / .NET syntax
- invalid states not representable
- more functional-style modeling where it helps
- lower ceremony than ad hoc `bool` + `out` patterns
- compatibility with EF Core, DSharpPlus, and existing application/service boundaries

## Proposed building blocks

## 1. Shared `Error`

Use one shared error payload across domain creation, validation, and settings write failures.

Conceptual shape:

```csharp
public sealed record Error(
    string Code,
    string Message,
    string? Target = null);
```

### Why

This gives one common format for:

- machine-readable error codes
- human-readable messages
- optional member/field targeting

Examples:

- `custom-command.name.empty`
- `moderation-reason.too-long`
- `xp.amount.negative`
- `settings.lock.end-time.invalid`

### Suggested location

Keep this in `Grimoire.Domain` so that:

- `Grimoire.Domain` can use it directly
- `Grimoire.Settings` can use it through the existing `global using Grimoire.Domain;` in `Grimoire.Settings/GlobalUsings.cs`

## 2. `Validation<T>`

Use `Validation<T>` for value-object creation and input normalization.

Conceptual shape:

```csharp
public abstract record Validation<T>
{
    public sealed record Valid(T Value) : Validation<T>;

    public sealed record Invalid(ImmutableArray<Error> Errors) : Validation<T>;

    public static Validation<T> Succeed(T value) => new Valid(value);

    public static Validation<T> Fail(Error error) => new Invalid([error]);

    public static Validation<T> Fail(IEnumerable<Error> errors)
        => new Invalid(errors.ToImmutableArray());
}
```

### Intended use

Use `Validation<T>` for:

- `CustomCommandName.Create(...)`
- `ModerationReason.Create(...)`
- `MessageContent.Create(...)`
- `XpAmount.Create(...)`
- `RewardMessage.Create(...)`
- future domain-specific parsers and normalizers

### Why this is separate from `Result<T>`

`Validation<T>` answers:

> “Can a valid domain value be constructed from this input?”

That is narrower and more specific than a generic operation result.

## 3. Richer `Result<T>`

Use `Result<T>` for domain and application operations that can succeed or fail for reasons broader than simple input invalidity.

Conceptual shape:

```csharp
public abstract record Result<T>
{
    public sealed record Success(T Value) : Result<T>;

    public sealed record Invalid(ImmutableArray<Error> Errors) : Result<T>;

    public sealed record NotFound(Error Error) : Result<T>;

    public sealed record Conflict(Error Error) : Result<T>;

    public sealed record Forbidden(Error Error) : Result<T>;

    public static Result<T> Ok(T value) => new Success(value);

    public static Result<T> Fail(Error error) => new Invalid([error]);

    public static Result<T> Fail(IEnumerable<Error> errors)
        => new Invalid(errors.ToImmutableArray());
}
```

### Intended use

Use `Result<T>` for:

- domain/application operations
- aggregate behaviors
- authorization/policy evaluation
- service-layer workflows where failure is meaningful and not just input validation

Examples:

- a command policy check returning allowed/forbidden
- a lookup returning found/not found
- a workflow returning conflict when state already exists

## 4. Keep `SettingsResult` as a specialized write outcome

Do **not** collapse `Grimoire.Settings/Helpers/SettingsResult.cs` into the same shape as `Result<T>`.

Current `SettingsResult` expresses a different question:

- was a write performed?
- was nothing changed?
- was the request invalid?

That is a write-status model, not a generic domain operation result.

### Recommended evolution

Keep:

- `SettingsWritten`
- `SettingsUnchanged`
- `SettingsInvalid`

But migrate `SettingsInvalid` to use the shared `Error` payload instead of only a raw string reason.

---

## What should be unified

## Unify these

### Shared error payload

Unify on one shared `Error` type across:

- `Grimoire.Domain/Result.cs`
- future `Validation<T>`
- `Grimoire.Settings/Helpers/SettingsResult.cs`

### Factory naming and conventions

Standardize the conventions used to create results, for example:

- success factories
- invalid factories
- optional not-found / forbidden / conflict factories

### Matching and composition helpers

Provide a small native helper layer for:

- `Match`
- `Map`
- `Bind`

This gives functional ergonomics without introducing a third-party library.

## Do not unify these into one type

### Domain validation vs generic operation results

Do not force `Validation<T>` and `Result<T>` into a single catch-all type.

Why:

- validation answers “can this value be created?”
- result answers “did this operation succeed?”

### Settings write outcomes vs generic results

Do not force `SettingsWritten` / `SettingsUnchanged` / `SettingsInvalid` into plain `Result<T>`.

Why:

- `Written` vs `Unchanged` is a domain-specific write status
- folding it into generic success/failure would make the API less clear

---

## Relationship to existing code

## `Grimoire.Domain/Result.cs`

Current shape:

```csharp
public abstract record Result<T>
{
    public sealed record Ok(T Value) : Result<T>;

    public sealed record Err(string Reason) : Result<T>;
}
```

### Recommendation

Evolve this file into the richer shared `Result<T>` design.

### Compatibility note

`Ok` and `Err` are already simple enough that a staged migration is possible:

1. add richer result cases or factory methods first
2. migrate call sites gradually
3. remove old naming only after call sites and tests are updated

## `Grimoire.Settings/Helpers/SettingsResult.cs`

Current shape:

- `SettingsWritten`
- `SettingsUnchanged`
- `SettingsInvalid`
- generic forms of the same

### Recommendation

Keep the shape conceptually, but change invalid payloads from `string Reason` to one of:

- `Error`
- `ImmutableArray<Error>`

This preserves the important write-state semantics while unifying the error vocabulary.

---

## Proposed rollout phases

## Phase 1 — introduce shared primitives without changing behavior

### Add

- shared `Error`
- shared `Validation<T>`
- richer `Result<T>` factories or cases

### Keep existing behavior

- do not change command handlers yet
- do not change settings write semantics yet
- do not force existing value objects to adopt `Validation<T>` immediately

### Goal

Create the primitives first so later migrations are incremental instead of disruptive.

## Phase 2 — adapt `Grimoire.Domain/Result.cs`

### Work

- expand `Grimoire.Domain/Result.cs`
- preserve compatibility where practical with temporary helpers or aliases
- update only a small number of consumers first

### Goal

Make the domain result type useful enough to be the default for future work.

## Phase 3 — adapt `SettingsResult` to shared errors

### Work

- update `Grimoire.Settings/Helpers/SettingsResult.cs`
- change `SettingsInvalid` to carry structured errors
- add convenience factories so existing settings services remain easy to read

### Goal

Unify the error language without losing the write-status semantics.

## Phase 4 — adopt `Validation<T>` in new or redesigned value objects

Start with types where validation clearly matters and is local.

Good early candidates:

- `CustomCommandName`
- `ModerationReason`
- `MessageContent`
- `XpAmount`
- `RewardMessage`

Avoid forcing this onto every existing type at once.

## Phase 5 — add native helper methods

Introduce small extension/helper methods for:

- `Map`
- `Bind`
- `Match`

Only add helpers once there are a few real call sites to justify them.

## Phase 6 — migrate higher-level consumers gradually

Once the primitives are stable, move outward into:

- service-layer operations
- policy checks
- richer aggregate creation flows
- redesigned domain models such as `CustomCommand`, `Sin`, or `XpHistory`

---

## Recommended first adopters

## Best first adopters

These give good signal with relatively low architectural risk:

### Value objects

- future `CustomCommandName.Create(...)`
- future `ModerationReason.Create(...)`
- future `XpAmount.Create(...)`
- future `RewardMessage.Create(...)`

### Small settings validations

- `ChannelLock` / `ThreadLock` temporal checks
- `Reward` numeric and message checks

### Existing domain result consumers

Any existing code already using `Grimoire.Domain/Result.cs` should be early migration targets once the richer shape is ready.

## Areas to avoid first

These should not be the first adopters:

### EF entities and persistence boundaries

Do not start by forcing result/validation types directly into EF entity shapes.

### Broad settings-service rewrites

Do not redesign all settings service APIs just to fit the new primitives.
Use adapters and incremental changes.

### DSharpPlus command handlers

Do not start at the UI/interaction edge.
That layer should consume the new results later, not define them first.

---

## Candidate files to revisit later

## Core primitives

- `Grimoire.Domain/Result.cs`
- new shared error/validation files in `Grimoire.Domain`
- `Grimoire.Settings/Helpers/SettingsResult.cs`

## Good first-domain adopters

- `Grimoire.Domain/CustomCommand.cs`
- `Grimoire.Domain/XpHistory.cs`
- `Grimoire.Domain/Sin.cs`
- `Grimoire.Domain/Pardon.cs`
- `Grimoire.Domain/SinReasonHistory.cs`

## Good first-settings adopters

- `Grimoire.Settings/Domain/ChannelLock.cs`
- `Grimoire.Settings/Domain/ThreadLock.cs`
- `Grimoire.Settings/Domain/Reward.cs`

## Service-layer candidates

- `Grimoire.Settings/Services/SettingsModule.Moderation.Locks.cs`
- `Grimoire.Settings/Services/SettingsModule.Leveling.Rewards.cs`
- `Grimoire.Settings/Services/SettingsModule.Leveling.Levelsettings.cs`
- `Grimoire.Settings/Services/SettingsModule.ModuleEnabled.cs`

## Consumer-side candidates

- moderation command handlers in `Grimoire/Features/...`
- any code that currently unpacks `Result<T>` or `SettingsResult<T>` manually

---

## Compatibility guidelines

## Preserve behavior first

For the first pass, prefer compatibility wrappers over immediate breaking changes.

Examples:

- old `Err(string)` call sites can temporarily route to `Invalid(Error)`
- old `SettingsInvalid(string)` can temporarily be supported through overloads that wrap into a structured `Error`

## Update tests alongside semantics

When a result payload becomes structured, update tests from:

- checking just raw reason strings

to:

- checking error code and/or message
- checking the result case explicitly

## Keep the public API intention clear

If a method’s meaning is:

- write performed / unchanged / invalid

keep `SettingsResult`.

If its meaning is:

- success / not found / conflict / forbidden / invalid

use `Result<T>`.

If its meaning is:

- create a valid value or explain validation failure

use `Validation<T>`.

---

## Suggested implementation order

1. add shared `Error`
2. add `Validation<T>`
3. enrich `Grimoire.Domain/Result.cs`
4. adapt `SettingsResult` to shared structured errors
5. adopt `Validation<T>` in new value objects and redesign work
6. add small helper methods only after real call sites appear
7. migrate outward into services and consumers gradually

## Final rule of thumb

Use one **family** of native C# result types, not one universal mega-type.

Recommended family:

- `Error` for shared error payloads
- `Validation<T>` for creation and normalization
- `Result<T>` for domain/application operations
- `SettingsResult<T>` for settings write outcomes

That gives consistency where it helps, without erasing the semantic differences between validation, operations, and write-status reporting.

