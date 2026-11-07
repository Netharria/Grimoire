# Design Review: functional style, performance and C# 15

A section-by-section review of the whole codebase against the design goals of this update. It is a different review from
`branch-review.md`. That one looked for defects. This one asks whether each part of the code has the shape the update
is aiming for. Findings only: nothing is changed until the owner picks what to do.

## Goals

Every finding serves one of these goals. Tag each finding with the goal's tag.

| Tag | Goal | What to look for |
|-----|------|------------------|
| `pipe` | Happy-path pipelines, one way of handling errors | Imperative `if (...) { await SendErrorResponseAsync(...); return; }` chains that could be one `Result` pipeline ending in a single error renderer. `.Match(..., _ => throw new UnreachableException())` used to unwrap a `Result`. `try/catch` around expected failures. Each feature reporting errors its own way. |
| `state` | Invalid state cannot be represented | Primitives standing in for domain concepts, nullable fields that are only valid together, `bool` flags that pick a mode, objects that can be built half-valid, `_ =>` arms that hide a missing case. |
| `perf` | No unnecessary work | Queries per message or event, the same row loaded twice in one flow, data loaded and then thrown away, results buffered with `ToListAsync` when streaming would do, missing `AsNoTracking`, or projections and compiled queries on hot paths. Frequently read rows that should be cached, and caches that are never invalidated. Sequential awaits that are independent. |
| `remove` | Remove what doesn't pay for itself | Types, layers, wrappers, tables, columns or indirection that add code without adding safety or speed. This includes parts of the database model overhaul that turned out not to pay off. |
| `cs15` | C# 15 adoption | `closed` hierarchies, `union` types, `[with(...)]` collection arguments, extension indexers and labeled `break`/`continue`, wherever they remove code or a runtime check. These are applied only after the move to `net11.0` (see "C# 15 readiness"). |

Check every finding against the ten coding principles in project memory (`feedback_coding_principles`).

## What counts as a finding

- **Smaller or safer.** A finding should remove code, remove a runtime check in favour of a compile-time one, or remove
  measurable work. If a change only restyles code and adds lines, leave it out or mark it `optional`.
- **No repeats.** Don't re-report the 96 findings in `review-decisions.md`, the known issues in
  `docs/testing/known-issues.md`, or the decisions in project memory. If a design finding would change how one of
  those decided fixes is done, say so and name its ID in "Interacts with".
- **Assume every correctness fix is in.** The owner will finish all fixes in `review-decisions.md` and the KI
  decisions before any of this review is implemented, and is making them while it runs. Review the code as it will be
  after those fixes. Where a decided fix changes the code you're looking at, base the finding on the fixed version and
  don't report a problem the fix already removes. Code may change while you read it. Cite the type and member name
  along with `path:line`, so findings still make sense when line numbers move.
- **Check the code.** Read the code before writing a finding down. For `perf`, name the event or command and how often
  it runs (every message, every command, a timer).
- **Respect deliberate choices.** `/Warn` is channel-level on purpose. `BanAddedEvent` is the only writer of ban sins.
  Full attachment URLs are stored on purpose. These are not findings.

### Finding format

```
#### <ID> <short title>  `<tag>` · **<impact>**
- **Where:** `path:line` (more than one if needed)
- **Now:** what the code does, in one or two sentences
- **Proposed:** the change, with a short code sketch if it helps
- **Size:** about how many lines are added and removed, and the files touched
- **Needs:** `net11.0` / a migration / another finding's ID / nothing
- **Interacts with:** IDs in review-decisions.md, or "none"
```

Impact levels: **high** (affects every message or event, or removes a class of bugs), **med** (one feature or a
frequent command), **low** (local cleanup), **optional** (style only).

IDs use the section prefix and a number, such as `P-3` or `MOD-12`.

## How to run a section

Each section can run in a fresh session or as a subagent.

1. Read this file and `docs/review/design/01-foundations.md` (once section 1 is done). Read `docs/domain-model` only
   if the section needs it.
2. Read only the section's paths. Use `review-decisions.md` to avoid repeats.
3. Don't edit source code. Write findings only to the section's own file, `docs/review/design/NN-<name>.md`. Never
   edit this file or another section's file. The roll-up updates the status table.
4. Begin the section file with a three-to-five line summary: the biggest wins, then the findings.
5. End with "Questions for the owner" for anything that needs a decision rather than a fix.

## Order

1. **Section 1 runs first and alone.** It sets the target shapes: the command pipeline, how errors are rendered,
   whether `Result<T>` becomes a `closed` record or a `union`, and how background services report failures. The
   feature sections measure code against these shapes, so they all find the same kind of problem the same way.
2. **Sections 2 to 11 run in parallel.** They only read code and each writes its own file.
3. **Section 12 is the roll-up.** It runs last.

## Sections

| # | Status | Section | Prefix | Paths | Lines |
|---|--------|---------|--------|-------|-------|
| 1 | `[ ]` | Foundations: Result, errors, command boundary | `F` | `Grimoire.Domain/{Result,ResultCombinators,ResultTaskExtensions,Validation,ValidationCombinators,ValidationTaskExtensions,Error,Unit}.cs`, `Grimoire/Extensions/CommandContextExtension.cs` and the error-response helpers, `Grimoire/Utilities/GenericBackgroundService.cs`, plus three sample commands (`Moderation/Warn`, `Leveling/Rewards`, `CustomCommands`) to test the shapes on | ~900 + samples |
| 2 | `[ ]` | Domain model | `D` | `Grimoire.Domain` (except section 1 files), `Grimoire.Settings/Domain` | ~3,000 |
| 3 | `[ ]` | Persistence and queries | `P` | `Grimoire.Data`, `Grimoire/DatabaseQueryHelpers`, the last three migrations and the model snapshot. Look at query *shapes* across the code (`grep` for `CreateDbContextAsync`), not feature logic | ~1,400 + queries |
| 4 | `[ ]` | Settings and caching | `SET` | `Grimoire.Settings` (except `Domain`), plus every FusionCache use (`SpamTrackerModule`, `DiscordAuditLogParserService`, `GrimoireServiceRegistration`) | ~2,500 |
| 5 | `[ ]` | Message hot path (cross-feature) | `HOT` | The five `MessageCreatedEventArgs` handlers: `GainUserXp`, `AddMessageEvent`, `LockedThreadEvents`, `TextCustomCommandProcessor`, `SpamEvents`. Also `UpdateMessageEvent`, `DeleteMessageEvent` and `ReactionEvents`. Look only at the work done per event, and at work duplicated *between* handlers (settings lookups, module checks, member fetches, PluralKit calls). The feature sections cover the rest of these files | ~1,000 |
| 6 | `[ ]` | Shared infrastructure | `SH` | `Grimoire/Features/Shared` (except Commands), `Grimoire/Extensions`, `Grimoire/Utilities`, `Program.cs`, `GrimoireServiceRegistration.cs` | ~3,500 |
| 7 | `[ ]` | Moderation: actions | `MOD` | `Moderation/{Ban,Kick,Mute,Warn,Lock}` and the background tasks | ~1,850 |
| 8 | `[ ]` | Moderation: sins and diagnostics | `SIN` | `Moderation/{SinAdmin,PublishSins,Diagnostics,SpamFilter}`, plus `Shared/Commands` (general settings, modules, purge, user info) | ~2,600 |
| 9 | `[ ]` | Logging | `LOG` | `Grimoire/Features/Logging`, `Grimoire/Features/LogCleanup` | ~1,750 |
| 10 | `[ ]` | Leveling | `LVL` | `Grimoire/Features/Leveling` | ~1,300 |
| 11 | `[ ]` | Custom commands | `CC` | `Grimoire/Features/CustomCommands` | ~1,200 |
| 12 | `[ ]` | Roll-up | n/a | All section files: remove duplicates, reconcile with `review-decisions.md`, put the changes in order (what to do before and after `net11.0` and what needs migrations), and estimate the net line change | n/a |

Tests (`Grimoire.Tests`, `*.Test.Unit`) aren't reviewed. Note a test only when it blocks a proposed change, for example
a test that pins the current error text.

## Section-specific questions

**1. Foundations.** Decide the target shapes and write them as short code sketches, not just as findings:
- The command shape. Can a command be `validate → load → act → reply` returning `Result<Reply>`, with one boundary that
  sends the reply, renders any error (handling the deferred and ephemeral rules from KI-21) and logs? Where does the
  DSharpPlus context end and pure code begin?
- The `Error` model. `Error(string Code, string Message, string? Target)` is stringly typed. Should the error cases
  (`NotFound`, `Forbidden`, ...) be what the renderer switches on, with `Code` dropped?
- `Result<T>`: a `closed` record (keeps `Map`/`Bind` as members and removes `_ => throw new UnreachableException()`),
  a `union` (`union Result<T>(Ok<T>, Invalid, NotFound, ...)`, case types with no shared base), or both. Do six failure
  cases pay for themselves, or would one `Failure(ErrorKind, ...)` do? How does `Validation<T>` relate to it?
- Is there a safe way to unwrap a `Result` from a smart constructor whose input is already known to be valid (there
  are 45 `UnreachableException` uses), or should those constructors take a pre-validated type?
- The background-service failure shape (`GenericBackgroundService`, alerts).

**2. Domain model.** For each of the ~20 `abstract record` hierarchies, decide whether it should become `closed`,
`union` or stay as it is, and which `_ =>` arms that change would remove. Ask whether each part of the database model
redesign earns its place: TPH hierarchies, strongly-typed IDs that are never mixed up, history tables that nobody
reads, `Obsolete/` types. Where can an entity still be built in an invalid state?

**3. Persistence.** Per-event query counts and round trips. Indexes that match the real query shapes. Places where
compiled queries, projections, `ExecuteUpdate`/`ExecuteDelete`, `AsAsyncEnumerable` or split queries would help. EF value
converters that cost a lot on hot paths. Whether EF Core and Npgsql handle `closed` hierarchies (the compiler adds
`[CompilerFeatureRequired]` to constructors). Flag this as a spike to run once the .NET 11 SDK is installed, and don't
guess the answer.

**4. Settings and caching.** Is every setting read on the hot path served from the cache? Is invalidation right after
every write, including in multi-row writes? Which rows from `GrimoireDbContext` are read often and change rarely, and
should be cached too (custom command definitions, ignore lists, reward tables, member/user rows)? Is the
`SettingsModule` partial-class layout (12 files, ~38 async APIs) the right size, or is it boilerplate per setting?

**5. Hot path.** Produce a table: handler → database calls, cache calls, Discord REST calls and PluralKit calls per
message, in the common case (a regular user in a regular channel, every module on). Then say what can be shared,
skipped early, run in parallel or batched.

## C# 15 readiness

- **Status now.** Every project targets `net10.0`. The only SDK installed is `10.0.114`. `global.json` pins `10.0.112`
  with `rollForward: latestMajor` and `allowPrerelease: false`, so the .NET 11 SDK is picked up automatically once it
  is installed and released. C# 15 needs `net11.0` as the TFM. Don't set `LangVersion` to `latest`.
- **During the review.** `cs15` findings describe the target code and what it replaces. Nothing is applied.
- **After .NET 11 ships.** A separate step: move the TFM and packages (EF Core 11, Npgsql 11, `Microsoft.Extensions.*`
  11), check DSharpPlus nightly on `net11.0`, run the EF `closed`-hierarchy spike, watch for the compiler's breaking
  changes (`with(`, `union`, `closed` as identifiers, `nameof(this.P)` in attributes), then apply the `cs15` findings
  in the order from the roll-up.
- **Leave out** the memory-safety preview (`unsafe(...)`, `safe`). It needs preview settings and this code has no
  unsafe code.

## Starting numbers

Measured on 2026-11-07, excluding migrations, `bin` and `obj`. Use these to see how the code changes, not as goals.

| Measure | Count |
|---------|-------|
| `throw new` | 79 |
| `catch (` | 62 |
| `UnreachableException` | 45 |
| `_ =>` switch arms | 138 |
| `abstract record` hierarchies | 20 |
| Early `return;` in `Features` | 148 |
| `SendErrorResponseAsync` calls | 27 |
| `CreateDbContext` calls | 84 |
| `SaveChangesAsync` | 42 |
| `ToListAsync` / `ToArrayAsync` / `ToDictionaryAsync` | 34 |
| `IAsyncEnumerable` / `AsAsyncEnumerable` | 8 / 5 |
| Compiled queries | 1 (`GainUserXp`) |
| Files using FusionCache | 4 |
| `MessageCreated` handlers | 5 |
