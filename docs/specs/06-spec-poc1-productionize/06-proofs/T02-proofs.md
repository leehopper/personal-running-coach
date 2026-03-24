# T02 Proof Summary: Relocate TestProfiles from src to tests

| # | Type | Artifact | Status |
|---|------|----------|--------|
| 1 | File | `TestProfiles.cs` namespace → `RunCoach.Api.Tests.Modules.Training.Profiles` | PASS |
| 2 | CLI | `grep TestProfiles backend/src/` → no matches | PASS |
| 3 | CLI | `dotnet test` → 289 pass, 1 pre-existing failure (not caused by this change) | PASS |

Pre-existing failure: `Lee_Intermediate_GeneratesPacesWithinVdotZones` — cached LLM response has a marginal pace value (400s/km vs 387s/km easy max). Same failure confirmed on commit 9713afa before namespace changes. Tracked as DEC-040 (Daniels pace table fix).

All 3 proof artifacts passed. No regressions introduced.
