# T05 Proof Summary: Layer 1/Layer 2 Training History Content Tests

## Task
Add tests verifying Layer 1 (per-workout detail) and Layer 2 (weekly summary) training history content format in ContextAssembler.

## What Was Done
Added 6 new test methods to `ContextAssemblerTests.cs` that verify the actual content format of the `training_history` section, not just its existence. Also added a `BuildLayeredHistoryInput()` helper that creates 4 weeks of training history relative to real `DateTime.UtcNow`, ensuring a deterministic Layer 1/Layer 2 split.

## Tests Added
1. **RecentWeeksUsePerWorkoutFormat** - Verifies Layer 1 pipe-separated format (date | type | km | min | pace/km) with at least 5 fields
2. **OlderWeeksUseWeeklySummaryFormat** - Verifies Layer 2 "Week of" prefix with "km total" and "runs" content
3. **Layer1ContainsWorkoutTypesAndPaces** - Verifies Easy and LongRun types present, M:SS/km pace regex
4. **Layer2IncludesLongRunDistance** - Verifies "Long run:" appears in weekly summaries for weeks with long runs
5. **Layer1BeforeLayer2InOutput** - Verifies output ordering: per-workout lines before weekly summary lines
6. **WorkoutNotesIncludedInLayer1** - Verifies workout notes (e.g., "warm-up") appear in per-workout detail

## Proof Artifacts
| File | Type | Status |
|------|------|--------|
| T05-01-test.txt | test | PASS |
| T05-02-cli.txt | cli | PASS |

## Result
All 6 new tests pass. Full suite: 312 tests, 0 failures, 0 regressions.
