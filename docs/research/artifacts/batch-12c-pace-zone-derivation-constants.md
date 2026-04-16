> **User-facing rename (2026-04-15):** References to "VDOT" in this document are preserved as historical technical vocabulary. Per DEC-043, the project's user-facing surface now uses "Daniels-Gilbert zones" or "pace-zone index" terminology to avoid exposure to The Run SMART Project LLC trademark (Runalyze enforcement precedent). Artifact body retained as-is for research provenance.

> **Errata (2026-04-15):** The R-pace derivation in the "Race-prediction zones" subsection — 3K race prediction scaled by distance-specific multipliers (0.9295 / 0.9450 / 0.9528) — was confirmed authoritative by R-035 (`batch-13-r-pace-disambiguation.md`) with max \|error\| ≤ 1.1 s and RMS 0.53 s across VDOT 30–85, the tightest fit of the three formulations compared. One small tightening: DEC-042 adopts `R-800 = 2 × R-400` instead of the 0.9528 multiplier because the simpler rule is within ±1 s at every anchor where R-800 is defined (VDOT ≥ 60) while 0.9528 overshoots by ~1 s. R-035 also revised the F (Fast Repetition) zone: it is the predicted 800 m race time scaled linearly (F-400 = t_800m / 2), not `R − 3 s/200 m` as often cited. The T = 88.0% and I = 97.3% constants in the "Fixed-percentage zones" subsection are unchanged and remain authoritative.

# Exact VDOT pace-zone constants for Daniels' Running Formula

**The training-pace zones E, T, and I are derived by applying fixed %VO₂max fractions — specifically 88.0% for T and 97.3% for I — to the runner's VDOT, then solving the Daniels-Gilbert oxygen-cost equation for velocity.** These two constants reproduce the published tables across VDOT 30–85 to within ±0.5 sec/km. A critical finding from this research: the reference T-pace values in the original query (4:27/km at VDOT 50, 5:27/km at VDOT 40) are actually **Marathon (M) pace values**, not Threshold pace. The true T pace at VDOT 50 is **4:15/km** (6:51/mile), confirmed across multiple independent transcriptions of the Daniels tables. This misidentification fully explains the anomalous back-solved percentages that prompted this investigation.

## The reference data contained an M/T pace swap

The discrepancy that motivated this research — T pace "commonly stated as 88% but back-solving gives ~83%" — dissolves once the correct table values are used. The values cited as T pace in the query correspond exactly to Marathon pace at adjacent VDOT levels:

| VDOT | Query's "T pace" | Actual T pace | Actual M pace | What the query value is |
|------|-----------------|---------------|---------------|------------------------|
| 40 | 5:27/km | **5:06/km** (8:12/mi) | ~5:27/km (~8:46/mi) | M pace at VDOT 40 |
| 50 | 4:27/km | **4:15/km** (6:51/mi) | ~4:27/km (M at VDOT 51) | M pace at VDOT 51 |
| 60 | 3:46/km | **3:40/km** (5:54/mi) | 3:52/km (6:14/mi) | Between T and M |

These T pace values (6:51, 8:12, 5:54 per mile) are confirmed by the sdtrackmag.com consolidated Daniels table, the prospectxctf.com TIR chart, and the Scribd "training splits by VDOT" document. Critically, **T, I, and R pace values have not changed between the 2nd, 3rd, and 4th editions** of the Running Formula, as documented by fellrnr.com's edition comparison. The I pace at VDOT 50 of 3:55/km is correct and consistent across all sources.

## T pace = 88.0% and I pace = 97.3% of VO₂max

Back-solving from the verified table values yields remarkably stable percentages. For each VDOT, I computed the oxygen cost at the published pace velocity, then divided by VDOT to obtain the effective %VO₂max:

**Threshold pace back-solved %VO₂max:**

| VDOT | T pace (per km) | T pace (per mile) | Velocity (m/min) | VO₂ at pace | Back-solved % |
|------|----------------|-------------------|-------------------|-------------|---------------|
| 30 | 6:24 | 10:18 | 156.3 | 26.43 | **88.10%** |
| 40 | 5:06 | 8:12 | 196.3 | 35.17 | **87.92%** |
| 50 | 4:15 | 6:51 | 235.0 | 43.98 | **87.95%** |
| 60 | 3:40 | 5:54 | 272.7 | 52.85 | **88.08%** |
| 70 | 3:14 | 5:13 | 308.6 | 61.51 | **87.87%** |
| 80 | 2:54 | 4:41 | 344.8 | 70.30 | **87.87%** |

The range is **87.87%–88.10%**, a variation of just 0.23 percentage points — well within the ±0.5% tolerance the query specified. The arithmetic mean is **87.97%**, and **88.0%** reproduces every table entry to within ±0.5 sec/km after rounding.

**Interval pace back-solved %VO₂max:**

| VDOT | I-1000m | Velocity (m/min) | VO₂ at pace | Back-solved % |
|------|---------|-------------------|-------------|---------------|
| 30 | ~5:55 | 169.0 | 29.18 | **97.27%** |
| 40 | 4:42 | 212.8 | 38.89 | **97.22%** |
| 50 | 3:55 | 255.3 | 48.71 | **97.42%** |
| 60 | 3:23 | 295.6 | 58.36 | **97.26%** |
| 70 | 2:59 | 335.2 | 68.19 | **97.41%** |
| 80 | 2:41 | 372.7 | 77.76 | **97.20%** |

The range is **97.20%–97.42%**, a variation of only 0.22 percentage points. **97.3%** reproduces the table to within ±0.5 sec/km. This confirms the query's suspicion that "100% VO₂max" is incorrect — the true computational constant sits firmly at **97.3%**, not 98% (as the LetsRun "math major" recalled) and not 100% (as commonly stated). The book's description of I pace as "97–100% VO₂max" is a physiological zone characterization, not the computational constant.

## How each zone is actually derived

The derivation method differs by zone. Three zones use a fixed %VO₂max; two use race-distance predictions requiring iterative solves.

**Fixed-percentage zones (E, T, I) — algebraic, closed-form solution:**

1. Compute `target_VO₂ = fraction × VDOT`
2. Solve the oxygen-cost quadratic for velocity: `v = (−0.182258 + √(0.182258² + 4 × 0.000104 × (target_VO₂ + 4.60))) / (2 × 0.000104)`
3. Convert velocity (m/min) to pace: `pace_sec_per_km = 1000 / v × 60`

The constants for each fixed-percentage zone:

- **T pace: 88.0% VO₂max** — a single value, not a range. Daniels describes T as "the pace you could sustain for about 60 minutes in a race." The F(t) sustainable-fraction equation at t = 60 min yields F(60) ≈ 0.888, which is consistent with 88%.
- **I pace: 97.3% VO₂max** — a single value. Daniels describes I as "the pace you could sustain for about 11–12 minutes." F(11) ≈ 1.000 and F(12) ≈ 0.992, so a race of roughly 11–12 min is near VO₂max. The training pace is set ~2.7% below the theoretical race-effort ceiling, which provides the slight cushion needed for repeated intervals.
- **E pace (2nd edition single value): 70.0% VO₂max** — verified to ±0.2% across VDOT 30–80. In the 3rd and 4th editions, E pace became a range. The fast boundary of that range sits near 70% and the slow boundary near 59%, though the E range boundaries show slightly more VDOT-dependent variation (~±1.5%) than T and I, suggesting they may incorporate minor manual adjustments or a different derivation method.

**Race-prediction zones (M, R) — require Newton-Raphson iteration:**

- **M pace** is the predicted marathon race pace (d = 42,195 m). Because the marathon takes different durations at different VDOT levels, the sustainable fraction F(t) varies, and the effective %VO₂max ranges from **80.7%** (VDOT 30, ~5-hour marathon) to **84.3%** (VDOT 85, ~2:08 marathon). An iterative solver finds the time `t` satisfying `VDOT = VO₂(d/t) / F(t)`, then computes pace from `t`.
- **R pace** is not expressible as a fixed %VO₂max at all. The LetsRun "math major" analysis found R pace best fits as a percentage of predicted 3K race-split times: **92.95%** of 200m-at-3K-pace, **94.50%** of 400m-at-3K-pace, **95.28%** of 800m-at-3K-pace. Daniels himself has stated R pace should be event-specific.

## The E pace range is the least precisely determined zone

The E pace range in the 3rd/4th edition is harder to pin down than T or I because two boundaries must be defined and the back-solved percentages show more variation across VDOT levels. Using the 2nd edition single E pace value (which was not a range):

| VDOT | E pace (2nd ed.) | Back-solved % |
|------|-----------------|---------------|
| 40 | 6:07/km (9:50/mi) | **70.04%** |
| 50 | 5:07/km (8:14/mi) | **70.01%** |
| 60 | 4:25/km (7:07/mi) | **69.88%** |
| 70 | 3:54/km (6:17/mi) | **69.84%** |
| 80 | 3:30/km (5:38/mi) | **69.95%** |

**70.0%** matches the 2nd edition single E pace to within ±0.2%. For the 3rd/4th edition range, the fast end appears to remain near 70% and the slow end near 59%, though both boundaries show ±1–2% VDOT-dependent variation. The range width decreases from roughly **76 sec/mile at VDOT 30 to 42 sec/mile at VDOT 85** according to the fellrnr.com edition comparison. For implementation, using **E-fast = 70%** and **E-slow = 59%** will reproduce the table within approximately ±3 sec/km for most VDOT values, but will not achieve ±1 sec/km precision across the full range. If tighter E-pace accuracy is required, a lookup table or VDOT-dependent interpolation is necessary.

## Verification across the full VDOT range

Applying T = 88.0% and I = 97.3% to the quadratic velocity solve, then rounding to the nearest second:

| VDOT | T predicted | T table | T error | I predicted | I table | I error |
|------|------------|---------|---------|-------------|---------|---------|
| 30 | 6:24.2/km | 6:24/km | +0.2s | 5:54.8/km | ~5:55/km | −0.2s |
| 40 | 5:05.5/km | 5:06/km | −0.5s | 4:41.7/km | 4:42/km | −0.3s |
| 50 | 4:15.2/km | 4:15/km | +0.2s | 3:55.3/km | 3:55/km | +0.3s |
| 60 | 3:40.1/km | 3:40/km | +0.1s | 3:23.0/km | 3:23/km | 0.0s |
| 70 | 3:14.3/km | 3:14/km | +0.3s | 2:59.1/km | 2:59/km | +0.1s |
| 80 | 2:54.4/km | 2:54/km | +0.4s | 2:41.1/km | 2:41/km | +0.1s |

**All T-pace errors ≤ 0.5 sec/km. All I-pace errors ≤ 0.3 sec/km.** This is well within the ±1 sec/km requirement. The residual errors are consistent with integer-second rounding in the published tables.

## Implementation recipe for a deterministic VDOT calculator

The complete algorithm for computing training paces from a race result:

```
Constants:
  T_FRACTION = 0.88
  I_FRACTION = 0.973
  E_FAST_FRACTION = 0.70
  E_SLOW_FRACTION = 0.59

Step 1 — Estimate VDOT from race performance:
  Given: race distance d (meters), race time t (minutes)
  velocity v = d / t
  VO2 = -4.60 + 0.182258 * v + 0.000104 * v^2
  F = 0.8 + 0.1894393 * exp(-0.012778 * t) + 0.2989558 * exp(-0.1932605 * t)
  VDOT = VO2 / F

Step 2 — Compute training paces for E, T, I:
  For each fraction f in {E_SLOW, E_FAST, T, I}:
    target_VO2 = f * VDOT
    v = (-0.182258 + sqrt(0.182258^2 + 4 * 0.000104 * (target_VO2 + 4.60))) / (2 * 0.000104)
    pace_sec_per_km = 60000 / v   # (1000 m / v m/min) * 60 s/min

Step 3 — Compute M pace via Newton-Raphson:
  Find t_m such that VDOT = VO2(42195/t_m) / F(t_m)
  M_pace = t_m / 42.195  (min/km)

Step 4 — Compute R pace from 3K race prediction:
  Find t_3k such that VDOT = VO2(3000/t_3k) / F(t_3k)
  R_200 = (200 / 3000) * t_3k * 0.9295
  R_400 = (400 / 3000) * t_3k * 0.9450
  R_800 = (800 / 3000) * t_3k * 0.9528
```

The Newton-Raphson solve for M and R paces converges in 5–10 iterations from an initial guess of `t₀ = d / (0.83 * v_at_VDOT)`.

## Conclusion

The Daniels VDOT pace system uses **three distinct derivation methods**, not one universal approach. E, T, and I paces use fixed %VO₂max fractions (**T = 88.0%, I = 97.3%**) applied through a closed-form quadratic solve — the cleanest and most precisely determined constants. M pace uses full race-prediction iteration at the marathon distance, producing a VDOT-dependent effective percentage (80.7–84.3%). R pace derives from 3K race predictions with distance-specific time multipliers.

The widely cited "100% VO₂max" for I pace and "88%" for T pace are, respectively, an overstatement of the actual 97.3% computational constant and a correct identification of the T constant — though the latter only matches the tables when used with the **correct T pace data** (4:15/km, not 4:27/km, at VDOT 50). The book's stated zone ranges (59–74% for E, 83–88% for T, 97–100% for I) describe the physiological intensity bands, not the single computational values that generate the table. For a production calculator targeting ±1 sec/km accuracy, the constants **T = 0.88 and I = 0.973** are sufficient for those zones. E pace requires either the 70%/59% approximation (±3 sec/km) or a lookup table for tighter precision. M and R paces require iterative numerical solvers.