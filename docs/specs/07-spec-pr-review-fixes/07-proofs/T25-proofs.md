# T25 Proof Summary

## Task
FIX test-4: Add test for ISO week year boundary in GroupByWeek

## Artifacts
| # | Type | File | Status |
|---|------|------|--------|
| 1 | test | T25-01-test.txt | PASS |
| 2 | cli  | T25-02-cli.txt  | PASS |

## Summary
Added a defensive test to `ContextAssemblerTests.cs` that exercises the `GroupByWeek` method
with workouts spanning the ISO week year boundary at Dec 28-29, 2025.

Dec 29, 2025 is a Monday -- the start of ISO week 2026-W01 -- so workouts on Dec 29, Dec 31,
Jan 1, and Jan 2 all belong to ISO week 2026-W01 despite spanning two calendar years.
Meanwhile Dec 28, 2025 (Sunday) belongs to ISO week 2025-W52.

The test creates 5 workouts across this boundary and verifies that the Layer 2 weekly summary
output contains exactly 2 week groups: one with 4 runs (ISO 2026-W01) and one with 1 run
(ISO 2025-W52). This confirms `ISOWeek.GetYear` and `ISOWeek.GetWeekOfYear` correctly handle
the case where the ISO year differs from the calendar year.

Note: The production code already handled this correctly by design (using `ISOWeek.GetYear`
rather than `DateTime.Year`). This test serves as a defensive regression guard documenting
the expected behavior at this edge case boundary.

All 326 tests pass. Build succeeds with 0 warnings.
