# T01 Proof Summary

## Task
FIX: GroupByWeek sets WeekStart to first workout date, not Monday

## Bug
`ContextAssembler.GroupByWeek` at line 174 set `weekStart = weekWorkouts[0].Date`, using the first workout's date instead of the ISO Monday. This caused "Week of" labels sent to the LLM to show incorrect dates when the first workout in a week was not a Monday.

## Fix
Replaced `weekWorkouts[0].Date` with `DateOnly.FromDateTime(ISOWeek.ToDateTime(isoYear, isoWeek, DayOfWeek.Monday))`, extracting the ISO year and week number from the group key to compute the actual Monday.

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | test | T01-01-test.txt | PASS |
| 2 | file | T01-02-file.txt | PASS |

## Test Added
`AssembleAsync_WeeklySummaryWeekStart_UsesIsoMondayNotFirstWorkoutDate` — verifies ISO 2025-W52 shows Monday 2025-12-22, not Sunday 2025-12-28 (workout date).

## Files Modified
- `backend/src/RunCoach.Api/Modules/Coaching/ContextAssembler.cs` (bug fix)
- `backend/tests/RunCoach.Api.Tests/Modules/Coaching/ContextAssemblerTests.cs` (new test)
