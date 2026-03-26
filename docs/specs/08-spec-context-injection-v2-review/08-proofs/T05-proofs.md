# T05 Proof Summary: Deterministic date-filtered test for overflow cascade Step 4

## Task
Inject `TimeProvider` into `ContextAssembler` to replace `DateTime.UtcNow` calls, making
the overflow cascade Step 4 and Layer 1/2 training history cutoff deterministic and testable.

## Changes
1. **ContextAssembler.cs**: Added `TimeProvider` constructor parameter; replaced 2 `DateTime.UtcNow`
   calls with `_timeProvider.GetUtcNow().DateTime` (Step 4 at line 373, Layer 1/2 cutoff at line 579).
2. **ServiceCollectionExtensions.cs**: Registered `TimeProvider.System` as singleton in DI.
3. **ContextAssemblerTests.cs**: Added 3 deterministic tests using `FakeTimeProvider` from
   `Microsoft.Extensions.TimeProvider.Testing`, plus a shared `BuildOverflowStep4Input` helper.
4. **EvalTestBase.cs**: Updated constructor call for compatibility.

## Proof Artifacts

| File | Type | Status |
|------|------|--------|
| T05-01-test.txt | test | PASS |
| T05-02-file.txt | file | PASS |

## Test Coverage
- 3 new tests covering overflow cascade Step 4 with deterministic dates:
  - Date filtering retains only workouts within 14-day window
  - Boundary condition: exact cutoff date is included (>= comparison)
  - All-old-workouts scenario: middle sections empty after filtering
- All 361 non-eval tests pass; 9 pre-existing eval cache failures unrelated to changes
