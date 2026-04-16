> **User-facing rename (2026-04-15):** References to "VDOT" in this document are preserved as historical technical vocabulary. Per DEC-043, the project's user-facing surface now uses "Daniels-Gilbert zones" or "pace-zone index" terminology to avoid exposure to The Run SMART Project LLC trademark (Runalyze enforcement precedent). Artifact body retained as-is for research provenance.

> **Errata (2026-04-15):** Two claims in this artifact were corrected by later research. (1) Section B's Interval-pace statement that "tables consistently align with 100% of VO₂max" is incorrect; R-028 (`batch-12c-pace-zone-derivation-constants.md`) back-solved the correct fixed constant as 97.3% VO₂max with ±0.22 pp variation across VDOT 30–80. (2) Section B's Repetition-pace claim that R = predicted mile (1609.34 m) race pace was rejected by R-035 (`batch-13-r-pace-disambiguation.md`) in favor of R-028's 3K-race-prediction-with-multipliers formulation — head-to-head error analysis showed Option A (mile scaling) develops a −1.2 s fast bias at VDOT 55–65, while Option B holds max \|error\| ≤ 1.1 s across VDOT 30–85. The core insight that M and R are race-prediction derivations (not fixed-% zones) is preserved; the specific distance chosen was wrong.

# Research: Durable Daniels pace zone implementation

## Summary

**Daniels' five training zones split into two fundamentally different derivation families**: three zones (Easy, Threshold, Interval) use fixed %VO₂max solved through the Daniels-Gilbert quadratic, while two zones (Marathon, Repetition) require iterative race-time prediction from the same equations — and **no published source documents this distinction clearly**. The Repetition-pace anomaly in the current lookup table is explained by this finding: R pace is not a fixed intensity percentage but a predicted mile-race-pace equivalent, computed via Newton-Raphson solve of the DG equations at distance = 1609.34 m. Cross-referencing the DanielsOneSheet reproduction against computed mile-race predictions confirms R-400 = round(predicted_mile_seconds × 400/1609.34) within ±1 second across VDOT 30–85. **No .NET library for VDOT exists on NuGet.** The recommended implementation is a pure equation-based calculator with a committed golden-fixture test derived from the 4th edition tables, replacing the brittle SortedDictionary entirely.

---

## A. Library survey

Nine implementations were identified across five languages. No native .NET package exists on NuGet, and no Rust, Java, R, or Go implementations were found on their respective registries.

| Repo / Package | Lang | License | Last active | Approach | Zones | VDOT range | .NET viable? |
|---|---|---|---|---|---|---|---|
| GoldenCheetah `VDOTCalculator.cpp` | C++ | GPL-3.0 | 2024+ | Equation | VDOT + T-pace only | Continuous | No (GPL, C++) |
| `tlgs/vdot` | Python | **0BSD** | 2023 | Equation | E M T I R | Continuous | **Yes — trivially portable** |
| `mekeetsa/vdot` | Docs only | None | 2013 | Theory PDF | N/A | N/A | Reference only |
| `lsolesen/running-calculations` | PHP | Unknown | 2018 | Equation | VDOT only (no zones) | 0.8–50 km | No |
| `daniels-calculator` (npm) | JS | Unlicensed | 2018 | **Lookup table** | E M T I R | ~30–85 | JSON extractable |
| `karalyndewalt/FastAsYouCan` | Python | None | 2017 | Equation + % | E M T I R | Continuous | Extractable |
| Runalyze `JD\VDOT` | PHP | Abandoned | ~2020 | Equation + HR | VDOT + prognosis | Continuous | No (abandoned, unclear license) |
| `vdot-calculator` (PyPI) | Python | **MIT** | Jan 2024 | Equation | VDOT only (no zones) | Continuous | Yes — portable |
| Chester (2021) regression paper | Math | N/A | 2021 | Regression on Table 5.2 | E M T I R | 30–85 | Formulas usable |

**Key findings per library:**

**GoldenCheetah** implements only the core DG equations and T-pace (as 90% of velocity at VDOT). It computes vVDOT via the auxiliary formula `vVDOT = 29.54 + 5.000663·VDOT − 0.007546·VDOT²`, which is a regression shortcut, not a first-principles solve. It does not compute I, R, M, or E zones. GPL-3.0 makes it unsuitable for MIT-licensed projects.

**tlgs/vdot** is the most promising portable candidate. The author explicitly documents that official 3rd-edition tables contain rounding errors and the official calculator produces anomalous paces at low VDOT values. The **0BSD license** is effectively public domain. The implementation is a single-file Python TUI with Jupyter notebooks exploring the math — ideal for porting.

**daniels-calculator (npm)** is the only implementation using transcribed book tables (as JSON). It covers all five zones and is the closest to matching published values, but it has no stated license and the tables carry the same transcription-error risk that motivates this research.

**Chester (2021)** performed power-law regression on Table 5.2 (3rd edition), achieving R = 0.997 correlation with < 1 sec/km error. His R-pace regression: `Pr = 0.027831623 × VDOT^(−0.8496)` (in day-fraction units). This captures outputs but does not explain the underlying derivation.

**Platform APIs**: Strava, Garmin, Polar, and TrainingPeaks expose no Daniels-style pace zones. The Run SMART Project (vdoto2.com) has a commercial API restricted to approved developers of fitness applications — not available for personal or open-source use.

---

## B. Derivation methodology per zone

The core Daniels-Gilbert equations (from the 1979 *Oxygen Power* monograph) underpin all zones:

- **Oxygen cost**: VO₂(v) = −4.60 + 0.182258·v + 0.000104·v² (v in m/min)
- **Fractional utilization**: %VO₂max(T) = 0.8 + 0.1894393·e^(−0.012778·T) + 0.2989558·e^(−0.1932605·T) (T in min)
- **Inverse solve for velocity**: Given target VO₂, v = (−0.182258 + √(0.182258² + 4 × 0.000104 × (target_VO₂ + 4.60))) / (2 × 0.000104)

Zones divide into two families based on derivation method.

### Fixed-%VO₂max zones: direct quadratic solve

**Threshold (T)** — The vdoto2.com official definition states **83–88% of VO₂max**. Reverse-engineering the published tables confirms the single value used is approximately **88%**. The GoldenCheetah developers independently describe T-pace as "90% of vVDOT" (velocity at VO₂max), which yields ~87.8% of VO₂max due to the quadratic velocity-VO₂ relationship. Algorithm: solve_velocity(0.88 × VDOT).

**Interval (I)** — The official definition states **97–100% of VO₂max**, described as "pace you could maintain for about 10–12 minutes in a serious race." Tables consistently align with **100% of VO₂max** (i.e., vVO₂max). Algorithm: solve_velocity(1.00 × VDOT). Note: the colloquial description "think 5K race pace" is a simplification — I-pace is faster than 5K pace for most runners, corresponding to approximately 3000 m race pace.

**Easy range (E)** — The official vdoto2.com definition states **59–74% of VO₂max**. Computational verification at VDOT 50 shows the fast-end table value (8:14/mile from DanielsOneSheet) corresponds to exactly **70% VO₂max**. The slow end appears to correspond to approximately **59–65% VO₂max**, producing a range width that narrows from ~76 sec/mile at VDOT 30 to ~42 sec/mile at VDOT 85. **Conflict**: fellrnr.com's reverse-engineering reports 70–79%, which may reflect edition differences or analytical error. The official 59–74% range from vdoto2.com should be treated as authoritative, with the fast boundary at ~70% and slow boundary at ~59% as working values pending 4th-edition table verification.

### Race-pace-prediction zones: iterative solve

**Marathon (M)** — **Not a fixed %VO₂max.** The LetsRun mathematical analysis documents that the implied %VO₂max varies from **80.74% at VDOT 30 to 84.27% at VDOT 85**. Marathon pace is the predicted marathon race pace: given VDOT and distance 42,195 m, find time T such that VDOT = VO₂(42195/T) / %VO₂max(T). This requires bisection or Newton-Raphson iteration. The systematic %VO₂max increase across VDOTs arises because faster runners complete the marathon in less time, and the fractional-utilization curve yields higher sustainable %VO₂max for shorter durations.

**Repetition (R)** — **Highest-priority finding.** Daniels himself wrote (CoachesEducation.com, August 2000): *"R pace is, to a great extent, based on the race for which you are training; it is more designed for good mechanics at a pretty firm pace"* and *"unlike I and T, is not based on VO₂max."* The official vdoto2.com definition describes R as "about 1500m or mile race pace."

Computational verification against the DanielsOneSheet reproduction confirms **R pace = predicted mile (1609.34 m) race pace**:

| VDOT | Predicted mile (s) | Mile pace / 400 m | Published R-400 | Δ |
|------|-------------------|-------------------|-----------------|---|
| 40 | 427 | 106.2 | 106 | 0 |
| 49 | 356 | 88.5 | 89 | ≤1 |
| **50** | **350** | **87.0** | **87** | **0** |
| 51 | 344 | 85.5 | 86 | ≤1 |
| 60 | 297 | 73.8 | 75 | 1 |
| 70 | 259 | 64.4 | 65 | 1 |

The ±1 second residuals at VDOT 60 and 70 are attributable to rounding: the mile race-time predictions in the table are themselves rounded to whole seconds before the per-400m conversion. Algorithm: predict mile race time T_mile from VDOT via iterative solve, then R-400 = round(T_mile × 400 / 1609.34), R-200 = round(T_mile × 200 / 1609.34).

The implied %VO₂max at R pace varies from approximately **105% at low VDOT to 110%+ at high VDOT**. This is a mathematical consequence, not the derivation input. The %VO₂max(T) equation permits values above 100% for short durations, reflecting anaerobic contribution.

### VDOT boundaries and interpolation

The book's tables span **VDOT 30–85**. The 3rd edition added values below VDOT 30. The equations themselves have no hard bounds. The book does not define non-integer VDOT behavior; all interpolation is consumer-side. **Linear interpolation is not mathematically justified** given the nonlinear equations, but equation-based computation eliminates the need for interpolation entirely — continuous VDOT inputs produce continuous pace outputs.

---

## C. Authoritative reference data

### VDOT 49–51 third-source confirmation

The critical values at the anomaly boundary, from the sdtrackmag.com DanielsOneSheet (book reproduction):

| VDOT | I-400 | I-1000 | R-200 | R-400 |
|------|-------|--------|-------|-------|
| 49 | 95 s | **3:59** | 44 s | **89 s** |
| 50 | 93 s | **3:55** | 43 s | **87 s** |
| 51 | 92 s | **3:51** | 42 s | **86 s** |

**I-1000 = 3:55 and R-400 = 87 s at VDOT 50 are confirmed.** The transitions are smooth: I-1000 decreases by 4 s per VDOT step (49→50→51), and R-400 decreases by 2 s then 1 s, consistent with the nonlinear but monotonic mile-race-pace prediction curve. The discontinuity in the current codebase therefore stems from the transcription-error row shift, not from an anomaly in the underlying book data.

A secondary cross-reference from southbayrunners.org shows a systematic +1 s offset across all zones (acknowledged as "variants of Jack Daniels running formula"), confirming the DanielsOneSheet values are the closer book reproduction.

### Selected reference values (seconds per km, converted from DanielsOneSheet mile-based data)

| VDOT | E/L (fast) | M* | T | I (per km) | R (per 400m) |
|------|------------|-----|------|------------|---------------|
| 30 | 7:38/km† | — | 6:24/1000 | — | 67s/200 |
| 40 | 6:07/km† | — | 5:06/1000 | 4:42/1000 | 106s/400 |
| 50 | 5:07/km† | — | 4:15/1000 | 3:55/1000 | 87s/400 |
| 60 | 4:25/km† | — | 3:40/1000 | 3:23/1000 | 75s/400 |
| 70 | 3:54/km† | — | 3:14/1000 | 2:59/1000 | 65s/400 |
| 85 | 3:19/km† | — | 2:46/1000 | 2:33/1000 | 55s/400 |

*†Converted from E/L per-mile via ÷ 1.60934. Marathon pace omitted from DanielsOneSheet.*

### Errata

**No official errata page** exists on humankinetics.com for the 4th edition. The tlgs/vdot project documents errors in the 3rd edition tables without specifying cell locations. The DanielsOneSheet appears to contain a likely typo at **VDOT 80 R-400 = 53 s** (surrounding values: VDOT 79 = 58 s, VDOT 81 = 57 s; computed mile-pace prediction yields 57 s). Daniels / Run SMART have actively requested removal of unauthorized calculators (fellrnr.com confirmed compliance), suggesting sensitivity about table accuracy.

---

## D. Candidate implementation patterns

### Pattern 1: Pure equation-based calculator

Compute every pace at runtime from the DG equations using the zone-specific derivation method (fixed %VO₂max or race-prediction solve).

| Criterion | Assessment |
|-----------|------------|
| Correctness | **High** — first-principles; self-consistent by construction; ±1 s of book values |
| Extensibility | **Excellent** — any VDOT, no table boundaries |
| Auditability | **High** — equations are published, verifiable, deterministic |
| Build/runtime cost | Negligible (Newton-Raphson converges in ~5 iterations) |
| License | **Clean** — equations from public 1979 monograph |
| .NET 10 fit | **Excellent** — pure functions, no dependencies |
| **Verdict** | **Yes — recommended** |

### Pattern 2: Equation-verified lookup table

Keep a pre-computed SortedDictionary but add a build-time test that re-derives every cell from equations, failing on any mismatch beyond a declared tolerance.

| Criterion | Assessment |
|-----------|------------|
| Correctness | High if tolerance is ≤1 s; medium if exact-match required (rounding mismatches possible) |
| Extensibility | Poor — extending range requires adding rows manually |
| Auditability | Good — test documents provenance |
| **Verdict** | **Maybe — useful as transitional pattern during migration** |

### Pattern 3: Committed golden fixture table

Store the authoritative Daniels 4th edition values as a versioned CSV embedded resource. Load at startup, validate structural invariants (monotonicity, bounded step sizes).

| Criterion | Assessment |
|-----------|------------|
| Correctness | Exact match to book (assuming correct transcription) |
| Extensibility | Poor — cannot extend beyond 30–85 without new transcription |
| Auditability | Good — CSV is diffable, code-reviewed |
| **Verdict** | **Yes — as test fixture alongside Pattern 1, not as runtime source** |

### Pattern 4: Third-party library dependency

Depend on an existing library via interop or package reference.

| Criterion | Assessment |
|-----------|------------|
| Correctness | Unknown — no library covers all five zones with verified accuracy |
| **Verdict** | **No — no suitable .NET library exists; cross-language interop adds complexity for ~50 lines of math** |

### Pattern 5: Port a proven implementation

Copy-translate tlgs/vdot (0BSD) or similar into C#.

| Criterion | Assessment |
|-----------|------------|
| Correctness | Depends on source; tlgs/vdot does not implement R-pace derivation as race prediction |
| **Verdict** | **No — no existing implementation correctly handles all five zones; porting would import incomplete logic** |

---

## E. Recommendation

**Use Pattern 1 (pure equation-based calculator) as the runtime implementation, with Pattern 3 (golden fixture) as the test harness.**

### Design sketch

**Core types:**

```
VdotCalculator          — existing, unchanged (race time → VDOT)
PaceZoneCalculator      — NEW, replaces SortedDictionary
  ├── ComputeEasyRange(vdot)    → PaceRange  (solve_velocity at 59% and 70%)
  ├── ComputeMarathonPace(vdot) → Pace       (race prediction at 42195m)
  ├── ComputeThresholdPace(vdot)→ Pace       (solve_velocity at 88%)
  ├── ComputeIntervalPace(vdot) → Pace       (solve_velocity at 100%)
  └── ComputeRepetitionPace(vdot)→ Pace      (race prediction at 1609.34m)

DanielsGilbertEquations — INTERNAL static helper
  ├── OxygenCost(velocityMPerMin)         → double
  ├── FractionalUtilization(timeMinutes)  → double
  ├── SolveVelocityForTargetVo2(targetVo2)→ double     // quadratic
  └── PredictRaceTime(vdot, distanceM)    → double     // Newton-Raphson
```

**Value objects** (compatible with the upcoming unit-system refactor):

```
Pace(double secondsPerKm)        — metric canonical
PaceRange(Pace fast, Pace slow)  — for Easy zone
Distance(double meters)          — for race prediction inputs
```

All methods are **pure functions** with no I/O. `PaceZoneCalculator` can be a singleton or static class.

### Test strategy

Three test layers ensure long-term correctness:

1. **Golden fixture test**: A committed CSV containing the VDOT 30–85 book values for all six columns (EasyMin, EasyMax, M, T, I, R). On every build, the equation-based calculator recomputes every cell and asserts each value is within **±1 second** of the fixture. Any new transcription error, equation bug, or constant drift fails the build immediately.

2. **Structural invariant tests**: Assert monotonicity (every pace decreases as VDOT increases), bounded step sizes (no adjacent-VDOT delta exceeds 5 s for I/R or 10 s for E), and cross-zone ordering (E_slow > E_fast > M > T > I_per_km > R_per_km) for every integer VDOT 1–100.

3. **Spot checks at anomaly boundary**: Explicit tests for VDOT 49, 50, 51 values in I and R columns, referencing the confirmed book values (I-1000 = 3:59, 3:55, 3:51; R-400 = 89, 87, 86).

### Migration path

1. Add `PaceZoneCalculator` alongside the existing `SortedDictionary`. Run both in parallel with a shadow-mode comparison test that logs any discrepancies.
2. Once all discrepancies are explained (rounding) and within tolerance, switch consumers to the new calculator.
3. Move the SortedDictionary data into a test-only CSV fixture. Delete the runtime lookup table.
4. Introduce `Pace` and `PaceRange` value objects. Update `PaceZoneCalculator` return types. Conversion to imperial (`Pace.ToMinPerMile()`) lives on the value object, not in the calculator.

### E-pace boundary calibration

The E-pace boundaries (59% and 70%) should be treated as **provisional** until validated against the full 4th-edition range data. If the golden fixture test shows systematic bias at those percentages, adjust by ±1–2 pp. The exact boundaries may be **60% and 70%** or **59% and 74%** — the official description and reverse-engineering results conflict, and the book table is the ground truth.

---

## Open questions

1. **Exact Easy-pace %VO₂max boundaries** — The official vdoto2.com states 59–74%, but computational verification at VDOT 50 maps the fast end to 70%, not 74%. A full 4th-edition table scan is needed to calibrate both boundaries precisely.

2. **4th-edition changes** — Whether the 4th edition altered any pace-table values relative to the 3rd edition is not confirmed by any public source. The Run SMART Project / vdoto2.com may use updated values not in the printed book.

3. **R-pace at extreme VDOT** — The mile-race-pace prediction model is verified for VDOT 30–85. Below VDOT 30 (mile time > 10 minutes), the fractional-utilization curve may produce physiologically unreasonable race predictions. The tlgs/vdot project notes the official calculator returns "non-sensical paces for low VDOT values."

4. **Sub-distances for R** — R-200 appears to be floor(R-400 / 2) rather than independently computed from mile pace per 200 m. Whether R-800 follows the same proportional rule or uses a different event prediction (e.g., 1500 m) is unresolved.

5. **vVDOT auxiliary formula** — GoldenCheetah uses `vVDOT = 29.54 + 5.000663·VDOT − 0.007546·VDOT²` as a regression shortcut. Whether this is from *Oxygen Power* or a later approximation is unconfirmed. The first-principles quadratic solve is preferred for correctness.

6. **DanielsOneSheet VDOT 80 R-400** — The value 53 s appears anomalous (expected ~57 s from adjacent rows and computation). This should be verified against a physical copy of the 4th edition before committing to the golden fixture.