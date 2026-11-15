# Documentation index

Where to find the current answer for each topic. When two documents disagree, the one listed here as the source wins.
Older documents carry a banner at the top saying what replaced them.

## Current sources

| Topic | Source | Notes |
|---|---|---|
| Building, testing, code style, project layout | [`../CLAUDE.md`](../CLAUDE.md) | Also covers configuration keys and the Stryker setup. |
| Running the bot, configuration for self-hosting | [`../README.md`](../README.md) | User-facing. |
| The language-ext migration: sections, order, settled conflicts | [`langext-migration/plan.md`](langext-migration/plan.md) | Part 2 overrides the design decisions where they differ. |
| Design specs for each migration section | [`langext-migration/specs/`](langext-migration/specs/) | One per section, reviewed before its code. |
| Design decisions (FP, performance, schema, feature behaviour) | [`review/design-decisions.md`](review/design-decisions.md) | Read with the plan's part 2. |
| Branch-review decisions (defect fixes) | [`review/review-decisions.md`](review/review-decisions.md) | |
| Known issues | [`testing/known-issues.md`](testing/known-issues.md) | Each open KI is assigned to a migration section. |
| Manual test plan, by feature area | [`testing/README.md`](testing/README.md) | The area docs `01`–`09`. |
| Errors and results: the four error cases, `Fin`, `Validation`, `Write<T>` | [`architecture.md`](architecture.md), section 3 | The migration bridge (`FinTaskExtensions`) is in spec 1.1. |
| Architecture: layers, effects, error model, boundaries, tests | [`architecture.md`](architecture.md) | Updated as migration sections land. |

## Historical

Kept for their reasoning. They are not current, and they move to `docs/archive/` in migration Phase 5.

| Document | What it was |
|---|---|
| [`domain-model/`](domain-model/README.md) | The April 2026 domain-model review and its implemented specs. `error-result-validation-strategy.md` is superseded by the migration (C1, C4). |
| [`review/branch-review.md`](review/branch-review.md) | Defect review of `feature/reorganizing`; decided in `review-decisions.md`. |
| [`review/design-review.md`](review/design-review.md), [`review/design/`](review/design/) | The FP/performance/C# 15 review; decided in `design-decisions.md`. |
| [`testing/automation-backlog.md`](testing/automation-backlog.md) | The integration-test backlog A-00 to A-53, complete. |
