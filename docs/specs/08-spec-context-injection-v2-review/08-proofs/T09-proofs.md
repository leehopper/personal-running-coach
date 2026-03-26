# T09 Proof Summary: Add construction validation to UserProfile record

## Task
Add construction validation to UserProfile record -- Age, RunningExperienceYears,
CurrentWeeklyDistanceKm, and other fields now reject invalid values at construction time.

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | test | T09-01-test.txt | PASS |
| 2 | file | T09-02-file.txt | PASS |

## Changes Made

### Implementation
- **UserProfile.cs**: Converted from positional record to explicit constructor with
  `{ get; init; }` properties. Added 12 validation guards covering all meaningful fields.

### Tests
- **UserProfileTests.cs**: 22 new tests covering valid construction, invalid values for
  each guarded parameter, boundary values for age, and record equality.

### Call Site Updates
- **TestProfiles.cs**: Updated all 5 profile constructions from PascalCase to camelCase
  named arguments to match new constructor parameter names.

## Verification
- `dotnet build`: PASS (0 errors, 0 warnings)
- `dotnet test`: 410/419 pass (9 pre-existing Eval failures, unchanged from baseline)
- No regressions in TestProfilesTests (all 30 tests pass)
