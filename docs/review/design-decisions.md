# Design review decisions

The owner's answers to the questions in `docs/review/design/12-rollup.md` part 7. Each entry gives the question number,
the decision, and how it changes the findings. Where a decision differs from the review's recommendation, the entry
says so.

## A. Foundations

| Q | Decision | Effect on the findings |
|---|----------|------------------------|
| 1 | **Typed `Unavailable`, no exceptions in our own code.** Differs from F-1. Keep S-4's direction. Outages are a `Result` failure of their own kind, created only by one adapter at the IO edge (where `ExecuteSafelyAsync` is now), which catches the EF/DSharpPlus exceptions. Exceptions stay only for defects (invariant violations, bugs) and cancellation. | F-1 is rewritten: don't delete `ExecuteSafelyAsync`; narrow it to the IO edge, and make it produce `Unavailable`. Remove the `GetOrElse(() => false/null)` defaults and let callers `Bind` through. The boundaries (command `RunAsync`, the last step of an event handler, the background tick) render the generic "[ID …]" reply and raise the alert. F-9 is required. SET-10 (fail-safe) is kept, so settings reads serve the last good value during an outage. LVL-4 keeps its alert, routed through the boundary. |
| 2a | **Keep `Validation<T>` and `Result<T>` as separate types.** Differs from F-2. `Validation` is for cheap, pure input checks: it runs them all and collects every error, and never touches the database. `Result` short-circuits, so expensive work doesn't run after a failure. | F-2 changes from "merge" to "use `Validation` for what it's for". In T-1, a command's validate step is a `Validation` over all of its inputs, converted to `Result` once, before the defer and any loading. Validations never query the database (no id-existence checks). The boundary renders every collected message. |
| 2b | **Drop `Error.Code` and `Error.Target`.** An error is its case plus a message. A specific error gets its own type only where code branches on it. | F-4 as proposed. About 60 test assertions move from `Code` to the case and the message. |
| 2c | **Two colours.** `Invalid` red, `NotFound` yellow. `Unavailable` always shows the generic error with an ID. | T-2's `ColorOf`, exhaustive over the cases under C# 15. |
| 3 | **Every refusal is ephemeral.** Successful actions stay public. | F-5's boundary applies the rule. MOD-1, SIN-1, LVL-3 and CC-2 as proposed. "Sin not found" gets one rendering. |
| 3b | **Warn: DM first, then the reply.** | T-1 as sketched. The reply can say whether the DM was delivered. No after-reply effect. |
| 4 | **Validate in the command.** The owner confirms that DSharpPlus hides a converter's error message. | F-6's converter base is dropped, and no spike is needed. Each command calls `Create` for its validated inputs in its `Validation` step (Q2a), so the user sees the real message, collected with the other input errors. MOD-4 and CC-4 follow this. LVL-3's `PositiveXpAmount.Create` in the input already fits. |

## B. Schema before the first deploy

| Q | Decision | Effect on the findings |
|---|----------|------------------------|
| 5 | **Keep all Settings history.** Differs from D-10. Every Settings table stays append-only, as `migration-strategy.md` says. Locks and mutes were already decided: `ClosedAt` on the xAdded rows, with the xRemoved events kept as the audit trail. | D-10 and D-11 are declined. S-5, S-13 and KI-103 stay as decided. The tombstone types, `Inherit` and `GuildSettingDefault` stay. Latest-row reads stay, served from the cache (SET-1/SET-3). D-14's positional-record cleanup is unaffected. |
| 6 | **Edit the undeployed migrations in place**, as decision 30 already does for `ModelRedesign`. No squash. A dev database has run them; it is rebuilt (dropped and recreated from `main`'s backup) after the edits, not patched. | P-12 (where it still applies, see Q9) and P-13 are done as edits. Remove the five create-then-drop index pairs, both the creates in the earlier migrations and the drops in `DropRedundantIndexes`. |
| 7 | **`bigint` ids with the first deploy.** Run the production size query first, to estimate how long the rewrite takes. | P-9 goes ahead in both contexts. `leaderboard_view` is dropped and recreated around it, and KI-31's `decimal?` workaround goes. P-11's parameters are `bigint`. |
| 8 | **Re-pardons stay, to correct a pardon's wording.** The pardon date never changes. Model it like `SinReasonHistory`: `Pardon` becomes 1:1 with `Sin` (key `SinId`, who pardoned and when), plus a new `PardonReasonHistory` (reason, actor, time). A second `/Pardon` on a pardoned sin appends a reason row. | Replaces D-12's options (a)/(b). The migration goes in `ModelRedesign`: each sin's earliest pardon row becomes the `Pardon`, and every row becomes reason history. SIN-2's single projection reads the `Pardon` plus the latest reason. Defect 7 (mixed rows) is fixed by construction. |
| 8b | **Show the original pardoner** and the original date, with the latest reason wording. | `/SinLog` and `/Publish Unban`. |
| 9 | **Readable string discriminators.** Differs from P-12. Keep `ModelRedesign`'s strings for `XpHistory.Type` and `MessageHistory`, and accept the rewrite and the UPDATE at deploy. | P-12 is declined. Combine the deploy rewrite with P-9's rewrite where the migration allows, so each table is rewritten once. |
| 10 | **Take the obsolete types out of the EF model now.** The tables stay until P-7's release drops them with raw SQL. | D-20 as proposed. |
