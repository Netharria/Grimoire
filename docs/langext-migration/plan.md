# language-ext v5 migration plan

Status: **approved direction, 2026-11-14.** The conflicts in part 2 are settled. No section has a spec yet.

This plan moves Grimoire to the effect style from the `spike/language-ext-v5` spike, which is described in
`docs/language-ext-spike.md` on that branch. It also folds in every decision in `docs/review/design-decisions.md` and
every known issue still open in `docs/testing/known-issues.md`. It's split into sections that are small enough to
review one at a time.

## 1. Ground rules for every section

1. **The spec comes first.** Before any code, the section gets a design spec in `docs/langext-migration/specs/`, built
   from the template in part 6. The owner reviews it. Code starts only after the spec is approved.
2. **One section is one reviewable change.** It builds, all tests pass, and it's committed before the next section
   starts. Old and new code run side by side: the spike showed that the runner and the harness allow this.
3. **Tests split by what they test** (part 4). A section that ports a feature also moves that feature's logic tests to
   the unit project, and keeps only real integration tests in the integration project.
4. **Docs are updated in the same section.** That covers the area's manual test doc, known-issues, and the architecture
   doc. Each fixed KI is marked fixed in `known-issues.md`.
5. **Decisions keep their IDs.** Specs cite `Q`/`TD`/`KI` IDs, so you can trace what each section implements.
6. **DSharpPlus problems go in `docs/upstream/dsharpplus.md`** (owner, 2026-11-15), as `DSP-` entries, for the owner to
   review and then report or fix. A KI covers only Grimoire's side (a workaround, a containment, a skipped test) and
   links to its DSP entry.

## 2. Settled conflicts (owner, 2026-11-14)

These override `design-decisions.md` where they differ.

| # | Topic | Decision |
|---|-------|----------|
| C1 | Error model | **Extend language-ext's errors** with Grimoire types that match the Discord error mapping (TD-12): `NotFound`, `Invalid`, `Forbidden`, `Unavailable`, all deriving from `Expected`. The error is its case plus a message; codes aren't used (Q2b). The boundary matches these cases. Any other `Error` (`Exceptional`) is a defect. |
| C2 | Outermost edge | **The runner renders the error replies** and raises the alerts itself. Nothing is rethrown to DSharpPlus. Its handlers stay only for failures that happen before our code runs (checks, converters). |
| C3 | Data access | **Dapper for all runtime code, EF Core only for migrations** and the model snapshot. Q28 becomes a non-retrying streaming query path. TD-6's documents are `jsonb` with System.Text.Json type handlers. Q16's upsert is a `Sql` value. |
| C4 | Domain types | **Replace** `Grimoire.Domain.Result/Validation/Error/Unit` with `Fin`, `Validation<Error, _>` and `Unit`. `Grimoire.Domain` references `LanguageExt.Core`. |
| C5 | Discord types in the core | **Builder types such as `DiscordEmbedBuilder` are allowed in the core.** Discord types that tests can't construct (`DiscordMember`, `DiscordGuild`, `DiscordChannel`, `DiscordMessage`, …) stay in the shell, which copies what the core needs into Grimoire records. |
| C6 | In-memory state | **`Atom` and `AtomHashMap`** for the spam tracker (replacing Q20's `ConcurrentDictionary`), the XP cooldown (Q18), the last-known member cache (Q17d) and similar state. |
| C7 | Integration tests | Integration covers SQL, migrations, DSharpPlus wiring, event dispatch, PluralKit/HTTP, and **one end-to-end happy-path smoke test per command and handler**. Everything else is a unit test. |
| C8 | Unmerged KI branches | **Discard** `ki/leveling`, `ki/message-logging`, `ki/guild-sync-invites`, `ki/alerts-background` and `ki/pluralkit-crosscutting`. Their KIs stay open in `known-issues.md` and are fixed in the Phase 3 sections that own them (checked: every one is assigned below). |
| C9 | First production deploy | **Wait for the whole migration.** Phase 2 edits the undeployed migrations in place (Q6). |
| C10 | `/Warn` reason | **Warn requires a reason**, as an exception to Q30. |
| C11 | Text-command replies (owner, 2026-11-15, spec 1.2 OQ-1) | **Text commands reply publicly**, refusals and the generic error included, which amends Q3. The exceptions: a private command run as text reacts 🔒 and doesn't run (the user-command-channel commands `/Level` and `/Leaderboard`); a non-moderator's text `/SinLog` gets its log by DM, which reverses Q36's silent reply; text custom commands react ❓ for both "not found" and "not allowed", so Q44 holds (3.17, spec 1.2 OQ-7). |
| C12 | One migration chain (owner, 2026-11-18, spec 2.1 OQ-6) | **Both schemas are migrated by one migrations-only context, as one ordered chain**, so each data copy comes right before the drop or rewrite that depends on it, and the order no longer rests on startup code (P-7's trap). This partly amends Q6: the post-`main` migrations of both contexts are re-expressed in the merged chain, while `main`'s deployed migrations stay as they are. Section 2.0. |
| C13 | Module states in one document (owner, 2026-11-19, spec 2.2 OQ-1) | **All six module on/off flags are one `Modules` settings document**, and each module's document holds only its settings. This amends TD-6 ("Disabled is `Enabled = false`"); a null channel still turns a log off. The gate reads one small document, and a disabled module's document is never loaded or cached. SET-1's single cache entry per guild becomes one entry per document (3.1 decides the final layout). |
| C14 | Settings collections as documents (owner, 2026-11-19, domain review DD-2) | **Rewards, XP exclusions, the two channel overrides and the diagnostics ignores become versioned documents per guild**, like TD-6's settings, instead of append-only logs where the newest row decides. This amends Q5 for these tables: history is kept as document versions with who/when, not as event rows. `Inherit`, the `Watched…` types and `RewardRemoved` go: a missing entry means "no override", "not excluded" or "no reward". Section 2.5. |

These spike deviations will be corrected to follow the decisions:

- **Q3b:** the DM is sent before the reply. The spike replies first.
- **TD-11:** the boundary defers, and a privacy attribute replaces `Command.Defer(...)` in the pipeline.
- **TD-12:** Discord lookups return `Fin` with the C1 cases, instead of `Option` plus a `DiscordError` code.
- **Spike records holding Discord types:** they become Grimoire records (C5).
- **Run summary:** `GenericBackgroundService`'s run summary gets ported.
- **Version pin:** language-ext stays pinned to an exact version. The spike used `5.0.0-beta-78`; section 1.1 checks for a newer release.

### Domain decisions (owner, 2026-11-19)

From a review of both domains against the principles (no invalid state, no primitive obsession, no implicit state
machines, functional core). Each is done by the section named, and its spec cites the ID. Only DD-2 changes the schema.

| # | Decision | Section |
|---|----------|---------|
| DD-1 | **A sin's standing is one union, computed by one pure function**: `Active`, `Lapsed(at)` (out of the auto-pardon window) or `Pardoned(by, at, reason)`, from the sin, its pardon, the setting and the time. Never stored, since a setting change moves it. Pardoned wins over lapsed. `/SinLog`, `/UserInfo` and the spam escalation use it. | 3.6 |
| DD-2 | **Settings collections as versioned documents** (C14). | 2.5 |
| DD-3 | **Hidden state machines become unions over today's columns:** `Reaction` (`Present`/`Removed(at)` for `RemovedAt`), log cleanup (`Pending(attempts)`/`GaveUp(at)` for `TimesTried`), alerts (`Unreported`/`Reported(at)` for `ReportedAt`), and a message's current state as one pure fold over its history (`Live`/`Deleted`). The history rows stay. | 3.12, 3.13, 3.20, 3.10 |
| DD-4 | **`NewSin` and `Sin` split**, as `NewMute`/`Mute` are: a sin exists only with its id, from the database (`RETURNING "Id"`). | 3.2 |
| DD-5 | **Each XP entry owns its typed amount** (`Earned(PositiveXpAmount)`, `Reclaimed(NegativeXpAmount, ModerationActor)`, …). `RawXp` stays at the row edge. `TimeOut` becomes `At`. A legacy reclaim with no moderator is `System`. | 3.14 |
| DD-6 | **No magic values or legacy nulls:** an invite's limit is `Unlimited`/`UpTo(n)` (not `MaxUses = 0`), and a custom command version's author is a `ModerationActor` (not `ModeratorId?`). | 3.19, 3.17 |
| DD-7 | **`Level` and `XpTotal` value types and a `LevelCurve`** (`LevelAt`, `Threshold`, `ToNext`), replacing `int`/`long` levels and `GetXpNeededForLevel(level, levelModifier)`. A reward's level is a `Level` (at least 1, Q41b). | 3.14, 3.15 |
| DD-8 | **Targets as a union:** `XpTarget` (`Channel`/`Member`/`Role`) and a diagnostics equivalent, instead of six subtypes over a raw `ulong`. Callers get the current set (`XpExclusions.Excludes(...)`), never "lifted" rows. Takes in SIN-9. | 3.16, 3.8 |
| DD-9 | **Strings with meaning get types:** `AlertKey`, `AlertType` (a closed set), `ExceptionTypeName`, `OccurrenceCount`, and `EmojiName` for custom emoji. | 3.20, 3.12 |
| DD-10 | **No user wording in the domain:** `Write<T>.Unchanged` carries no message; each command's renderer words it. | 3.1 |
| DD-11 | **Ported read models drop EF navigations** (`ICollection<T>`, `Sin?`, `Message?`) for immutable sequences of what the query returned. Phase 5 deletes the rest with the EF entities. | each Phase 3 port, Phase 5 |

## 3. Sections

The phases are in order. Sections inside a phase can run in parallel unless they list a dependency. KIs are listed in
the section that fixes them.

### Phase 0: groundwork (no language-ext yet)

| § | Section | Contents |
|---|---------|----------|
| 0.1 | Save the spike **(done 2026-11-14, 4a3f8ac)** | Commit the spike to `spike/language-ext-v5`. It's uncommitted in `../Grimoire-langext-spike`. |
| 0.2 | KI branches and known-issues **(done 2026-11-14)** | Delete the five unmerged `ki/*` branches and all eight worktrees in `.claude/worktrees/` (C8). Check that `known-issues.md` still lists those branches' KIs as open. Confirm that the custom-command KIs committed in fb1b29e (83–88, 113, 114) are fixed, test each one, and mark them fixed, since the doc still lists them as open. |
| 0.3 | Configuration **(done 2026-11-14)** | Q23, Q23b and SH-1: delete the extra `ConfigurationBuilder` and put a placeholder in place of the tracked password. Finding (tested): the block only beat environment variables for **non-empty** file values, because a chained source treats an empty value as missing. So `main` works, and the risk was this branch's non-empty dev connection string. Rollup defect 1 overstated it. |
| 0.4 | Split the test projects **(done 2026-11-14)** | Create `Grimoire.UnitTests` (no Docker, no network) and `Grimoire.IntegrationTests` (Testcontainers and the fake Discord harness). Move existing tests by what they touch: `Grimoire.Domain.Test.Unit` and the pure Settings tests go to Unit; the DB-backed `Features/*` tests, the Settings DB tests and `Integration/*` go to Integration. Shared fixtures go in a small `Grimoire.Tests.Common` only if both projects need them. Fix Stryker: `mtp` runner, unit project only, solution mode (spec: `specs/0.4-test-project-split.md`). Update CLAUDE.md and the `dotnet test` commands. Tests are moved, not rewritten. |
| 0.5 | Docs, first pass **(done 2026-11-14)** | Part 5, step 1: fix CLAUDE.md's wrong facts, mark the superseded docs, and add `docs/README.md` as an index. |

### Phase 1: foundations

| § | Section | Depends on | Contents |
|---|---------|------------|----------|
| 1.1 | Error and result types **(done 2026-11-15, spec `specs/1.1-error-and-result-types.md`)** | C1, C4 | Add `LanguageExt.Core`, pinned. Add the `Expected` subtypes `NotFound`, `Invalid`, `Forbidden` and `Unavailable`. In one sweep, Domain and Settings domain move to `Fin`/`Validation<Error, _>`/`Unit`. Apply TD-5 (`Create` returns `Validation` only when it can fail) and F-4 (assertions move from `Code` to the case). Retire `GetOrElse` (TD-13). |
| 1.2 | Runtime and boundary **(done 2026-11-15, 63647bb, spec `specs/1.2-runtime-and-boundary.md`)** | 1.1, C2 | Productionise `Grimoire/Functional`: the capabilities, the runtimes and `Has<>`. Command boundary: validate, then defer (TD-11, with a privacy attribute), then run, then render (Q2c colours, Q3 ephemeral refusals, text commands per C11 (public replies, 🔒 for private commands, DM for `/SinLog`), the generic "[ID …]" reply plus an alert for `Unavailable` and defects). Event boundary and worker boundary, with the run summary ported and F-9. A fake runtime for unit tests. |
| 1.3 | Data access **(done 2026-11-15, spec `specs/1.3-data-access.md`)** | 1.2, C3 | Dapper type handlers (strongly typed ids, `jsonb`), `Sql` values, transactions, a retry schedule only for idempotent reads, and a non-retrying streaming path (Q28 → `IAsyncEnumerable` through `IO`). Postgres errors map to `Unavailable` (Q1) at this one edge. Pattern for SQL tests in the integration project. |
| 1.4 | Discord edge **(done 2026-11-16, ed32d54, spec `specs/1.4-discord-edge.md`)** | 1.2 | TD-12 lookups (NotFound, Invalid, Forbidden, Unavailable). SH-15 retry split (Q25). SH-10 one log message record with the event's time (Q26). LOG-2 log gate on `GuildLog` (Q26b). KI-54. |
| 1.5 | PluralKit **(done 2026-11-16, 20fffa8, spec `specs/1.5-pluralkit.md`)** | 1.4 | SH-2 token bucket with a small queue (Q24), the deleted-member branch (Q37), LOG-3 and LOG-4. KI-101, KI-111. |
| 1.6 | Telemetry **(done 2026-11-16, 2a1c681, spec `specs/1.6-telemetry.md`)** | 1.2, 1.3, 1.4 | OpenTelemetry traces and metrics, exported over OTLP. **Traces:** the command, event and worker boundaries each open a root span (command or event name, guild id, outcome case). The database, Discord, PluralKit, settings and cache capabilities open child spans, so a failed command's trace shows which step failed and why. Npgsql's and `HttpClient`'s own instrumentation adds the SQL and REST detail. **Metrics:** duration histograms for commands, events, worker ticks and each capability call; counters by outcome case (`Invalid`, `NotFound`, `Forbidden`, `Unavailable`, defect); gauges for the outbox backlog, the cache hit rate, the PluralKit queue and the guild log queue. **Logs:** Serilog logs carry the trace id. The "[ID …]" in the generic error reply becomes the trace id, so a user's report leads straight to its trace. No message content or names in spans or tags (defect 9). **Owner, 2026-11-14:** the backend is `grafana/otel-lgtm` on the owner's home server, which has plenty of storage, so retention can be long. Grafana's UI goes behind the existing Nginx Proxy Manager (TLS plus an access list or Grafana login). **The bot runs on DigitalOcean App Platform and the backend at home** (owner, 2026-11-16). App Platform has no sidecars and wipes its disk on every deploy, so the bot exports straight to home over OTLP/HTTP (TLS and basic auth through NPM), from background threads with bounded queues that never block it. A home outage longer than a few minutes loses traces and logs, while metrics recover their totals and App Platform's console logs keep every line with its trace id (spec 1.6, OQ-3). Tail sampling runs in `otel-lgtm`'s own collector and drops only successful worker ticks that did nothing (OQ-2). The error reply's ID is the trace id. Unit tests check spans and metrics with in-memory exporters. |

### Phase 2: schema before the first deploy (EF migrations, edited in place per Q6, in one chain per C12)

2.0 and 2.1 can run in parallel. 2.2, 2.3 and 2.5 start after 2.0, so they edit migrations in the merged chain. 2.4,
the rehearsal, runs last, after 2.5.

| § | Section | Contents |
|---|---------|----------|
| 2.0 | One migration chain **(done 2026-11-19, 94d2809, spec `specs/2.0-one-migration-chain.md`)** | C12. One migrations-only context that maps both the `public` and `Settings` schemas. (Both contexts already share `public.__EFMigrationsHistory`, spec 2.0; what they don't share is one chain.) The post-`main` migrations of `GrimoireDbContext` and `SettingsDbContext` (`ModelRedesign` onwards, and Settings `Initial` onwards) become one ordered chain, so Settings `Initial`'s copy from the legacy tables runs before anything that changes or drops them. `main`'s deployed migrations stay byte for byte, so production's history still matches. Startup migrates once, with P-3's timeout. The two runtime contexts stay until their features are ported (Phase 5), with no migrations of their own. The two test fixtures stay, both migrating through the chain (OQ-3). EF 10 runs each migration in its own transaction (checked), an input to the squash decision after 2.3 (OQ-2). Dev and test databases are rebuilt. **The spec decides:** where the context lives (Grimoire.Data referencing Grimoire.Settings, or a new migrations project), whether the post-`main` migrations are squashed or kept as steps, and how a check proves the merged chain produces the same schema as today's two chains. |
| 2.1 | Production measurements **(done 2026-11-19, fd3c9c3, spec `specs/2.1-production-measurements.md`, results `measurements/2.1-results.md`)** | Q7: run the size query and the `EXPLAIN` plans from rollup part 6. This sets the time budget for 2.3. **Result:** the first deploy's migrations are estimated at about 6 minutes, the longest statement at about 2 minutes (`XpHistory`), against 30 minutes per statement and two hours in total. Both guards are 0. P-16's compaction is needed (3.14). |
| 2.2 | Settings schema **(done 2026-11-19, a6ebe77, spec `specs/2.2-settings-schema.md`; the production equivalence check is still to run)** | TD-6 settings as JSON per module, with module states in their own `Modules` document (C13) (versioned, with who/when). TD-1 and TD-2 state rows for mutes and locks (they replace `ClosedAt` from 876e460). P-9 bigint. Q6 redundant index pairs. Migration of the legacy data into the new shapes. Decide what the 3 overdue mutes on production become (2.1). |
| 2.3 | Grimoire schema **(done 2026-11-19, 8349ee0, spec `specs/2.3-grimoire-schema.md`; the production copy run is still to run)** | Q8 `Pardon` 1:1 plus `PardonReasonHistory`. Q11/D-19 column drops. Q10/D-20 obsolete types leave the model. TD-9 rename to `ServerDisplayNameHistory`. Q6 index pairs. P-9 bigint with the view recreated: tie-break (defect 23), names from the database (TD-9), 0 XP excluded (Q12b). Q9 string discriminators, rewritten once together with P-9. **Budget (2.1):** no statement over 15 minutes. Fold the `XpHistory."Type"` rewrite into P-9's rewrite of that table (one `ALTER TABLE`), and drop the `IX_XpHistory_GuildId_Xp` pair. Decide what a pardon with no reason becomes (24 of 38). |
| 2.5 | Settings collections as documents | C14, DD-2. Rewards, XP exclusions, the message log and spam filter channel overrides, and the diagnostics ignores become versioned `jsonb` documents per guild in Settings `GuildSettings` (or a sibling table; the spec decides), written with 2.2's version race. Settings `Initial`'s legacy copy builds them from `main`'s tables, keeping only what is in force. The append-only tables, `Inherit`, the `Watched…` types and `RewardRemoved` go. `SettingsModule` keeps its public API, as in 2.2. **The spec decides:** one document per collection or fewer, how a reward's level is held before DD-7's `Level` exists, and what the cache entries become (feeds 3.1). |
| 2.4 | Rebuild and rehearse | Rebuild the dev database from `main`'s backup. Run both contexts' migrations against a copy of production. From 2.1: check that autovacuum is on (production's statistics were reset). The runbook ends with `ANALYZE`, plus `VACUUM` on `Messages` and `MessageHistory`. The fallback of migrating from the owner's machine sets `Command Timeout=1800` (spec 2.1 OQ-4). |

### Phase 3: feature ports

Each section ports its slice to `Eff`, implements its decisions and KIs, adds its telemetry (1.6), moves its logic tests to unit tests, and
proposes UX changes in its spec.

| § | Section | Decisions | KIs |
|---|---------|-----------|-----|
| 3.1 | Settings service | SET-1/2/3 typed cache (one entry per settings document since C13, spec 2.2, and the C14 documents), Q19 1-hour expiry, SET-10 fail-safe, KI-103, DD-10 | 103, 105 |
| 3.2 | `/Warn` (the template command) | Q3b, C10 (reason required), T-1 shape, DD-4 | — |
| 3.3 | Ban, Unban, Kick | MOD-1/2/3/4, Q30, Q32, KI-03 handoff, MOD-11 leak (defect 8), Q33 publish | — |
| 3.4 | Mute processor | TD-1/3/4 (adds the outbox's retry columns by editing Settings `Initial`, spec 2.2), Q31, KI-55 order, MOD-6, `UserJoinedWhileMuted` replaced | 53 |
| 3.5 | Lock processor (channels, threads, voice) | TD-1/2/3 (the same retry columns; an expiry records why it ended, which 2.2 dropped), permission capture, Q15 locked-thread handoff | 102, 112 |
| 3.6 | Sins | SIN-1..6, Q8/8b, Q33, Q34/34b, Q36 as amended by C11 (text `/SinLog` DMs a non-moderator), DD-1 | — |
| 3.7 | Spam filter | Q20, Q14b, Q35/SIN-12 through the mute processor | 63, 107 |
| 3.8 | Diagnostics | SIN-14/15, the parts of D-23 that are already decided, DD-8 | — |
| 3.9 | `MessageCreated` pipeline | Q14, HOT-3/4, Q15 | — |
| 3.10 | Message logging (edit, delete, bulk delete) | LOG-5 (Q39), defect 9, defects 12/13, DD-3 (message state) | 64, 69, 70, 104 |
| 3.11 | User logging and last-known cache | LOG-1 `Diff`, Q17/17b/17d, TD-7, TD-8, TD-9 | 67, 68, 97 |
| 3.12 | Reactions | Q16 single upsert, DD-3 (reaction state), DD-9 (`EmojiName`) | 72 (re-check) |
| 3.13 | Log cleanup worker | Q38/LOG-6, streaming, DD-3 (retry state) | — |
| 3.14 | XP gain and rewards | Q18 cooldown, Q41/41b, LVL-5 `PlanRewards`, P-16 ledger compaction (2.1: three members over 50,000 rows), DD-5, DD-7 | 74, 75, 76, 109 |
| 3.15 | `/Level`, Leaderboard, Award, Reclaim | TD-10, Q12, Q40, LVL-3/7, C11 (🔒 on private text use), DD-7 | 77, 79, 80, 81 |
| 3.16 | Leveling settings and ignores | LVL-12, DD-8 | 73, 78 |
| 3.17 | Custom commands | CC-1..9, Q21 (and dropping `fuzzystrmatch`), Q42–45, C11 (❓ reaction for not found and not allowed), DD-6 | 87 (decide: case-insensitive names). Keep the fixes for 83–86, 88, 113 and 114 |
| 3.18 | Log and moderation settings commands, Modules | — | 65, 66, 71 |
| 3.19 | Guild sync and invites | DD-6 | 89, 90, 98, 100, 108, 116 |
| 3.20 | Alerts, guild issues, gateway health | SH-11 (defect 10), DD-3 (alert state), DD-9 | 48, 91–96, 99, 106, 110 |

### Phase 4: .NET 11 and C# 15 (once the Arch SDK ships; can be slotted in anywhere after 1.2)

- Move the TFM and packages to 11.
- Run the EF `closed` spike.
- Make the domain sum types `closed` (the C1 error cases need a Grimoire base under `Expected` for this; decide in the Phase 4 spec), and remove the `_ =>` arms (T-3, D-23).

### Phase 5: cleanup

- Remove the EF runtime query helpers, and the EF navigations left on domain records (DD-11).
- **All EF code moves into `Grimoire.Migrations`** (owner, 2026-11-19). Once nothing queries them, delete
  `GrimoireDbContext` and `SettingsDbContext`. Move the entity configurations (`Grimoire.Data/Configuration`,
  `Grimoire.Settings/Configurations`), the EF value converters and the id conventions into `Grimoire.Migrations`, so
  `GrimoireMigrationsDbContext` declares its own model. Delete 2.0's sharing code: `GrimoireModel`, `SettingsModel`,
  `EntitySetConventions` and `MigrationModelTests`, keeping explicit names only where a Settings table still shares a
  name with a `public` one (none once P-7 drops the legacy tables). The domain's `InternalsVisibleTo("Grimoire.Data")`
  for the `FromDatabase` factories points at `Grimoire.Migrations` instead. `Grimoire.Data` keeps only the Dapper type
  handlers, and may merge into `Grimoire`. The runtime projects drop their EF packages. The spec checks that the moved
  model produces no pending changes against the snapshot.
- Remove `GenericBackgroundService`, the bridges and the old command helpers (`TryDeferAsync`). (`IPluralkitService`,
  the 1.5 bridge, goes earlier, in 3.10.)
- Remove the old Discord lookups (`Get…OrDefaultAsync`, `GetUserAvatar`), `DiscordRetryPolicy`, and the old guild
  log classes (`GuildLogMessageBase`, `GuildLogTextMessage`, `GuildLogMessageCustomEmbed`,
  `GuildLogMessageCustomMessage`) with `GuildLog`'s bridge overloads (spec 1.4).
- Delete the integration tests that duplicate unit tests.
- Write the backlog items into `docs/backlog.md`: Q29, the DM-based `/SinLog` (Q36), the UX pass from Q40, and the DSharpPlus upstream report: every open entry in `docs/upstream/dsharpplus.md` (Q17 is DSP-1), reviewed by the owner first.
- Final docs pass (part 5, step 4).

## 4. Test strategy

| Project | Holds | May touch |
|---------|-------|-----------|
| `Grimoire.UnitTests` | Smart constructors, pure decisions, embed builders, state machines (mute/lock transitions), `Diff`, scoring, and whole `Eff` pipelines run against the fake runtime: outages, `Forbidden`, absent members, retries and ordering. | Nothing outside the process. |
| `Grimoire.IntegrationTests` | Every `Sql` value against Postgres (Testcontainers + Respawn). Migrations. Live capabilities against the fake Discord REST/gateway. Command registration, options and checks, slash/text parity, ephemeral flags. PluralKit/HTTP. One end-to-end smoke test per command and handler (C7). | Docker, the fake Discord harness. |

**Mutation testing:** Stryker runs against `Grimoire.UnitTests` only. Each Phase 3 spec records the area's score before and after (`dotnet stryker --since:main`), and surviving mutants in the pure core are treated as missing tests.

A port counts as done when every behaviour pinned by the old integration tests is covered by a unit test or a kept
integration test. Each spec lists that mapping.

## 5. Documentation

Conflicts found so far:

- ~~**CLAUDE.md is wrong in four places.**~~ Fixed in 0.5, together with the `SettingsDbContext` name and the feature folders. Line endings are LF everywhere (owner, 2026-11-14): `.editorconfig` set to `lf`, `.gitattributes` added, and the 33 CRLF/mixed files converted.
  - It names `Grimoire.Test.Unit`, which doesn't exist.
  - It says commands use `ApplicationCommandModule`, but the code uses DSharpPlus.Commands.
  - It says the `this.` qualifier is always required, which no longer holds for primary-constructor parameters (Q27).
  - The five-project list is out of date.
- *(Banner added in 0.5.)* **`docs/domain-model/error-result-validation-strategy.md`** describes the native `Result`/`Validation` that C4 removes.
- *(Banner added in 0.5.)* **`docs/domain-model/migration-strategy.md`** says Settings is append-only. TD-1 and TD-6 changed that.
- *(Banners added in 0.5.)* **`docs/domain-model/*` from April** is a review whose findings are now resolved or superseded.
- **`design-decisions.md`** has crossed-out entries (Q13, Q17c) and amendments in place. *(0.5 added a banner listing the plan's overrides; the clean `docs/decisions.md` is step 3.)*
- **The root `README.md` dates from 2024.** *(0.5 fixed its facts: .NET 10 and the configuration keys; 0.3 made its env-var claim true. The full refresh is in Phase 5.)*
- ~~`known-issues.md` listed the KIs fixed in fb1b29e as open.~~ Fixed in 0.2.

Steps:

1. **Phase 0.5:** fix CLAUDE.md. Add a "Superseded by …" banner to the conflicting docs. Add `docs/README.md` as an index that names the current source of truth for each topic.
2. **Phase 1.2 (done 2026-11-15):** add `docs/architecture.md` as the single current description: layers, effects and runtimes, the error model, the boundary rules, and the test strategy. It replaces the domain-model strategy docs.
3. **As sections land:** keep a clean `docs/decisions.md` that holds only the current decisions, merged from review-decisions and design-decisions. Superseded wording moves to an appendix.
4. **Phase 5:** move the finished reviews, the implemented specs and the automation backlog to `docs/archive/`. Refresh the README.

## 6. Spec template (`docs/langext-migration/specs/NN-name.md`)

1. **Scope:** the files, and the decision and KI IDs it covers.
2. **Flow:** the steps from input to the reply or log, as a list or diagram. Mark which steps are pure and which are effects.
3. **Types:** new records and unions, their smart constructors, and the error cases.
4. **Edge cases:** a table of each case, what happens, and the error case and colour it gets.
5. **UX suggestions:** wording, ephemerality, embed layout and option changes, each marked "suggested", for the owner to accept or reject.
6. **SQL:** each statement and its test.
7. **Telemetry:** the spans, tags and metrics the section adds, and what a failure looks like in a trace.
8. **Tests:** the unit tests, the integration tests that are kept, and the old tests mapped to their replacements.
9. **Docs to update.**
10. **Open questions.**
