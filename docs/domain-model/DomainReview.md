# Domain Model Review — Grimoire.Domain

Status key: ✅ Resolved · ⚠️ Partially resolved · ❌ Still open

---

## I. Result & Validation Machinery

### #1 — ✅ Inconsistent error shape (array vs. single) across subtypes

**Original issue:** `Result<T>.Invalid` held `ImmutableArray<Error>` while `NotFound`, `NotModified`, `Conflict`, and `Forbidden` each held a single `Error`. The `Match` method then wrapped the single-error cases into an array on the fly.

**Current state:** All subtypes now hold a single `Error`. `Match` passes errors directly without wrapping. The shape is fully consistent.

---

### #2 — ✅ `Validation<T>` didn't accumulate errors — the name lied

**Original issue:** `Validation<T>` short-circuited on `Bind`, identical to `Result<T>`. It had no applicative combiner to collect all failures independently.

**Current state:** `ValidationCombinators.cs` provides `Validation.Combine` with 2–6 arity overloads that collect all failures into a single `ImmutableArray<Error>`. `Result.WhenAll` uses these combiners internally.

---

### #3 — ✅ `Validation<T>` missing `Tap` / `TapAsync`

**Original issue:** `Result<T>` had both methods but `Validation<T>` didn't.

**Current state:** Both `Tap(Action<T>)` and `TapAsync(Func<T, Task>)` are present on `Validation<T>`.

---

### #4 — ✅ `Result.WhenAll` silently dropped all but the first failure

**Original issue:** The fixed-arity `WhenAll` overloads short-circuited on the first failing task. The `Map<TOut>(_ => default!)` null-suppression was also a smell.

**Current state:** All fixed-arity `WhenAll` overloads delegate to `Validation.Combine`, collecting every failure. The `default!` suppression is gone.

---

### #5 — ✅ `MatchAsync` failure handlers were always synchronous

**Original issue:** `Result<T>.MatchAsync` accepted `Func<T, Task<TOut>>` for success but only `Func<Error, TOut>` (sync) for failure, forcing callers to `Task.FromResult` in failure arms.

**Current state:** Two `MatchAsync` overloads exist — one with sync failure handlers and one with fully-async `Func<Error, Task<TOut>>` failure handlers.

---

### #6 — ✅ Parameter name mismatch: `onNotChanged` vs. `onNotModified`

**Original issue:** `Result.Match` used `onNotModified`; the `ResultTaskExtensions` extension used `onNotChanged`.

**Current state:** Both use `onNotModified` consistently.

---

### #7 — ✅ No `IEnumerable`-arity `WhenAll`

**Original issue:** `Result.WhenAll` only had fixed-arity overloads (up to 6). The most practically useful form — accepting `IEnumerable<Task<Result<T>>>` — was missing.

**Current state:** `Result.WhenAll<T>(IEnumerable<Task<Result<T>>> tasks)` is implemented, collects all errors, and returns `Result<IReadOnlyList<T>>`.

---

### #8 — ✅ No `ToResult()` extension on `Task<Validation<T>>`

**Original issue:** `Validation<T>.ToResult()` existed as an instance method but there was no extension to call it in a pipeline without first `await`ing.

**Current state:** `ValidationTaskExtensions` provides `ToResult()` on `Task<Validation<T>>`.

---

## II. Strongly-Typed IDs

### #9 — ✅ `SinId` and `AttachmentId` co-located with entities, not with other IDs

**Original issue:** The six Discord IDs lived in `StronglyTypedIds.cs`; `SinId` lived in `Sin.cs` and `AttachmentId` in `Attachment.cs`, with no consistent convention.

**Current state:** Both `SinId` and `AttachmentId` are now in `StronglyTypedIds.cs` alongside all other IDs.

---

### #10 — ✅ `SinId` missing `TryParse`; `long` vs. `ulong` unexplained

**Original issue:** All Discord IDs have `TryParse`; `SinId` did not. `SinId` uses `long` (matching PostgreSQL `bigserial`) while all other IDs use `ulong`, with no comment explaining the deliberate difference.

**Current state:** `TryParse` added (using `long.TryParse`). A `<summary>` doc comment on `SinId` explains that the backing column is a PostgreSQL `bigserial` (signed 64-bit) and that switching to `ulong` would silently truncate values above `long.MaxValue` on EF Core round-trip.

---

### #11 — ✅ No conversion between `ModeratorId` ↔ `UserId`

**Original issue:** A moderator IS a Discord user, but converting between the two IDs required `new ModeratorId(userId.Value)`, defeating the type-system protection.

**Current state:** Explicit conversion operators are present:

```csharp
public static explicit operator ModeratorId(UserId id) => new(id.Value);
public static explicit operator UserId(ModeratorId id) => new(id.Value);
```

---

## III. Value Types — "Invalid State Unrepresentable" Coverage

### #12 — ✅ Half the value types allowed invalid/null state (no factory)

**Original issue:** `Nickname`, `Username`, `AvatarFileName`, `InviteCode`, and `InviteUrl` all had public constructors with no validation.

**Current state:** All five types now have private constructors and `Create()` factory methods returning `Validation<T>`.

---

### #13 — ✅ Static `Equals(a, b)` pattern is non-standard and confusing

**Original issue:** `AvatarFileName`, `Nickname`, `Username`, and `MessageContent` defined `public static bool Equals(T? a, T? b)` methods that shadow `object.Equals` and are unexpected by C# consumers.

**Current state:** All four types now use instance methods (`public bool Equals(T other, StringComparison comparison)`). The single caller that needed nullable handling was updated to use null-propagation (`message.Content?.Equals(...) ?? false`), which is more readable than the static form.

---

### #14 — ✅ `Attachment.FileName` was a raw string

**Original issue:** Every other filename-like field was wrapped in a value type; `Attachment.FileName` was a plain `string`.

**Current state:** `Attachment.FileName` is typed as `AttachmentFileName`, which has a private constructor, a `Create()` factory, and an internal `FromDatabase` method.

---

### #15 — ✅ PluralKit `SystemId` / `MemberId` were raw strings

**Original issue:** Nothing prevented passing a `SystemId` string where a `MemberId` was expected.

**Current state:** `PluralKitSystemId` and `PluralKitMemberId` are typed value objects with private constructors and `Create()` factories, living in `ProxiedMessageLink.cs`.

---

### #16 — ✅ `CustomCommand.Content` was a raw string

**Original issue:** `TextCustomCommand` and `EmbedCustomCommand` stored content as `required string Content` with no bounds or empty check.

**Current state:** `CustomCommandContent` is a smart-constructor value type (max 2000 chars, non-empty) used everywhere command content appears.

---

## IV. Entity Model Issues

### #17 — ✅ `{ get; } = DateTimeOffset.UtcNow` timestamps may not round-trip through EF

**Original issue:** Several entities used `public DateTimeOffset Foo { get; } = DateTimeOffset.UtcNow`. Without a setter, EF Core must set the backing field via reflection at materialization time. If the mapping is not configured precisely, an entity loaded from the database will have the object-construction time, not the stored value.

**Current state:** All seven remaining entities are now `required DateTimeOffset ... { get; init; }`. All construction sites in production and test code explicitly provide the timestamp. `Message.CreatedTimestamp` and `MessageCreatedEntry.Timestamp` use `args.Message.CreationTimestamp` (the actual Discord message timestamp) rather than `UtcNow`.

---

### #18 — ✅ `Sin.Id` uses `private set`, breaking record immutability

**Original issue:** `public SinId Id { get; private set; }` was the only property in the model using a mutable setter. All others use `init`.

**Current state:** Changed to `public SinId Id { get; init; }`. EF Core is configured with `.UsePropertyAccessMode(PropertyAccessMode.Field)` on the `Id` property so it writes directly to the backing field after INSERT for the `bigserial` identity column, bypassing the C# `init` restriction. A comment on the property explains the EF-only write-back.

---

### #19 — ✅ `required ModeratorId? ModeratorId` — required nullable is semantically confusing

**Original issue:** `required` + nullable reads as "you must provide this, but null is a valid value." The better model is a discriminated union (e.g., `ModeratorActor` vs. `SystemActor`).

**Current state:** `required` removed from both `Sin.ModeratorId` and `SinReasonHistory.ModeratorId`. Both are now plain optional `ModeratorId?` properties — nullable when the acting party is unknown or system-generated, consistent across the sin cluster.

---

### #20 — ✅ `Invite.Inviter` is a `Username`, not a `UserId` (review premise incorrect)

**Original issue:** The inviter's display name can change; storing a `Username` makes the invite record stale over time. The stable identity is the Discord user ID.

**Actual semantics:** `Invite` is a pure in-memory snapshot used to identify which invite a new member used when joining. It is never persisted. Staleness is not a concern — the snapshot is refreshed from the Discord API on every relevant event. The only use of `Inviter` is string interpolation for a join-log message, where `Username` is exactly the right type. No change needed.

---

### #21 — ✅ `Invite` has no `GuildId` (review premise incorrect)

**Original issue:** An invite is scoped to a guild, but the entity has no `GuildId`, making it impossible to query all invites for a guild without an external join.

**Actual semantics:** `Invite` is in-memory only, stored inside a `GuildInviteDto` whose `ConcurrentDictionary<InviteCode, Invite>` is already scoped per guild. The guild context lives in the container; adding `GuildId` to each `Invite` entry would be redundant. No change needed.

---

### #22 — ✅ `LeaderboardView` is not sealed

**Original issue:** Every other concrete entity is `sealed record`; `LeaderboardView` was just `record`.

**Current state:** Changed to `public sealed record LeaderboardView`.

---

### #23 — ✅ `XpHistory.TimeOut` is a misleading name (review was incorrect)

**Original issue:** The review assumed `TimeOut` stored the recording timestamp and suggested renaming to `RecordedAt` or `Timestamp`.

**Actual semantics:** `TimeOut` stores the XP cooldown expiry — the point in time *after* which the user can earn XP again. The `DateTimeOffset` type is correct. The name is intentional and domain-accurate; the review misread the field's purpose. No rename needed.

---

### #24 — ✅ `ICollection<T>` navigation properties expose mutable state (not applicable)

**Original issue:** Collections typed as `ICollection<T>` allow external callers to call `.Add()` / `.Remove()` directly, bypassing any invariants the aggregate might enforce.

**Decision:** No mutation of navigation collections occurs in application code — all writes go through the DbContext's DbSet directly. The theoretical exposure is real but has no practical impact on this codebase. Enforcing aggregate invariants via read-only collections would add significant boilerplate for no concrete benefit (see #28). Closed as not applicable.

---

### #25 — ✅ `MessageHistoryEntry.TimeStamp` casing inconsistency

**Original issue:** Every other timestamp property uses `Timestamp` (lowercase `s`); `MessageHistoryEntry` used `TimeStamp` (uppercase `S`).

**Current state:** Renamed to `Timestamp` (lowercase `s`) as part of the #17 fix. The EF column mapping preserves the existing `"TimeStamp"` column name via `HasColumnName("TimeStamp")`, so no migration is required for this rename.

---

### #26 — ✅ Dual `ProxiedMessageLink` properties on `Message` are confusing

**Original issue:** `Message` has both `ProxiedMessageLink? ProxiedMessageLink` and `ProxiedMessageLink? OriginalMessageLink`, both of the same type. The reason for two separate references isn't explained.

**Current state:** XML doc comments added to both properties explaining the PluralKit flow:
- `ProxiedMessageLink` is set when *this* message is the PluralKit webhook (proxied) message.
- `OriginalMessageLink` is set when *this* message is the original message sent by the user's main Discord account, which PluralKit deleted and replaced.

The names are correct; only the explanation was missing.

---

## V. Domain Layer Boundary

### #27 — ✅ EF Core navigation properties bleed into domain entities (accepted trade-off)

**Original issue:** Properties like `Pardon.Sin`, `Attachment.Message`, and `ProxiedMessageLink.ProxyMessage` are EF navigation properties, not domain concepts.

**Decision:** This is an accepted trade-off of the EF Core + single-project domain model approach. Navigation properties are used exclusively in EF query projections (`.Include()`, LINQ selects), never in domain logic. Separating persistence and domain models would require a full mapping layer with no meaningful benefit for this application. Closed as accepted trade-off.

---

### #28 — ✅ No aggregate roots or encapsulated invariants (not applicable)

**Original issue:** The `Sin` cluster (Sin + Pardons + ReasonHistory + PublishedMessages) should form an aggregate, but any code can add to any collection directly. The model doesn't enforce, for example, that a Sin cannot have two Pardons.

**Decision:** The invariants that exist in this domain are simple enough that enforcing them at the application layer (in command handlers) is sufficient and correct. Introducing aggregate roots would add significant architectural ceremony — loading full aggregates before every mutation, routing all writes through root methods — for no concrete correctness improvement. Closed as not applicable.

---

## Summary

| # | Status | Severity | Category | Issue |
|---|--------|----------|----------|-------|
| 1 | ✅ | High | Result | Inconsistent error shape across subtypes |
| 2 | ✅ | High | Validation | `Validation<T>` had no applicative combining |
| 4 | ✅ | High | Combinators | `WhenAll` silently dropped all but the first failure |
| 17 | ✅ | High | Entities | `{ get; } = UtcNow` timestamps don't round-trip through EF |
| 12 | ✅ | Medium | Value Types | Half the value types allowed invalid state (no smart constructor) |
| 13 | ✅ | Medium | Value Types | Static `Equals` pattern non-standard |
| 19 | ✅ | Medium | Entities | `required ModeratorId?` — required nullable |
| 20 | ✅ | Medium | Entities | `Invite.Inviter` is `Username` not `UserId` (in-memory only, N/A) |
| 24 | ✅ | Medium | Entities | `ICollection<T>` nav props expose mutable state (not applicable) |
| 11 | ✅ | Medium | IDs | No conversion between `ModeratorId` ↔ `UserId` |
| 3 | ✅ | Low | Validation | `Validation<T>` missing `Tap`/`TapAsync` |
| 5 | ✅ | Low | Result | `MatchAsync` failure handlers were always sync |
| 6 | ✅ | Low | Extensions | `onNotChanged` vs. `onNotModified` name mismatch |
| 7 | ✅ | Low | Combinators | No `IEnumerable`-arity `WhenAll` |
| 8 | ✅ | Low | Extensions | No `ToResult()` on `Task<Validation<T>>` |
| 9 | ✅ | Low | IDs | `SinId`/`AttachmentId` placement inconsistency |
| 10 | ✅ | Low | IDs | `SinId` missing `TryParse`; `long` choice undocumented |
| 14 | ✅ | Low | Value Types | `Attachment.FileName` was a raw string |
| 15 | ✅ | Low | Value Types | PluralKit `SystemId`/`MemberId` were raw strings |
| 16 | ✅ | Low | Value Types | `CustomCommand.Content` was a raw string |
| 18 | ✅ | Low | Entities | `Sin.Id` uses `private set`, breaks record immutability |
| 21 | ✅ | Low | Entities | `Invite` has no `GuildId` (in-memory only, N/A) |
| 22 | ✅ | Low | Entities | `LeaderboardView` is not sealed |
| 23 | ✅ | Low | Entities | `XpHistory.TimeOut` name is misleading (review misread semantics) |
| 25 | ✅ | Low | Entities | `MessageHistoryEntry.TimeStamp` casing inconsistency |
| 26 | ✅ | Low | Entities | Dual `ProxiedMessageLink` properties on `Message` unexplained |
| 27 | ✅ | Low | Architecture | EF navigation properties in domain entities (accepted trade-off) |
| 28 | ✅ | Low | Architecture | No aggregate roots or encapsulated invariants (not applicable) |

**28 resolved · 0 still open**

The remaining medium-priority work is #24 (mutable collections) and #28 (aggregate roots) — fixing the collections is the prerequisite for enforcing any aggregate invariants.
