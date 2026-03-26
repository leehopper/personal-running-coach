# T02 Proof Summary: Fix VDOT pace table off-by-one shift (DEC-040)

## Task

Fix confirmed off-by-one row shift in Daniels pace table from VDOT 50 through 85,
where each row N contained the correct paces for VDOT N+1.

## Proof Artifacts

| # | Type | Description | Status |
|---|------|-------------|--------|
| 1 | test | Full test suite (305 tests, 0 failures) including corrected VDOT 50 values | PASS |
| 2 | cli | Build verification + cross-reference of corrected values against DEC-040/R-019 | PASS |

## Changes Made

1. **PaceCalculator.cs** (lines 50-85): Corrected pace table values
   - VDOT 50: New row computed from published book tables and equation verification
     - Marathon=271 (race prediction), Threshold=255 (book + equation), Interval=235 (book + equation), Repetition=218 (book R-400)
   - VDOT 51-85: Shifted back by one row to undo the off-by-one transcription error
   - Added XML doc comment explaining the correction and its provenance

2. **PaceCalculatorTests.cs**: Updated `CalculatePaces_Vdot50_MatchesDanielsTableValues`
   - Changed expected values from old (shifted) to corrected values
   - Added comments citing DEC-040 and verification sources

## Impact

- Fixes the pre-existing eval test failure (`Lee_Intermediate_GeneratesPacesWithinVdotZones`)
  that was documented as a known issue in the POC 1 productionization validation report
- All pace zones for VDOT 50-85 now return correct training paces
- No changes to VDOT 30-49 (confirmed correct by R-019 research)
- No architectural or API changes; data-only fix
