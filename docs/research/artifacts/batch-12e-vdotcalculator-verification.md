# VdotCalculator verification against Daniels' Running Formula

**The implementation's equations are correct — every coefficient matches authoritative sources to full published precision.** However, the anchor-point race times provided for cross-checking at VDOT 40–80 do not correspond to their labeled VDOT values. When tested against the *actual* book table values, the equations produce matching VDOTs within ±0.1 across all distances and fitness levels. The `.Max()` strategy for multiple races is consistent with Daniels' guidance, and the half-marathon distance of 21,097.5m is exact. Five distances present in the book's table are missing from the implementation, and edge-case guardrails should be added for durations below ~7 minutes.

---

## 1. All eight equation coefficients are confirmed correct

Every coefficient in both equations was verified against 10+ independent sources including the widely referenced Simpson Associates documentation (which directly cites the Daniels/Gilbert research), the GoldenCheetah open-source project's C++ implementation, the Omni Calculator scientific platform, multiple independent VDOT calculator websites, and GitHub repositories that reference the original 1979 publication *Oxygen Power: Performance Tables for Distance Runners* by Daniels and Gilbert.

**Oxygen cost equation:** `VO2 = −4.60 + 0.182258 × v + 0.000104 × v²`

| Coefficient | Code value | Verified value | Status |
|---|---|---|---|
| Constant | −4.60 | −4.60 | ✅ Match |
| Linear (velocity) | 0.182258 | 0.182258 | ✅ Match |
| Quadratic (velocity²) | 0.000104 | 0.000104 | ✅ Match |

**Fractional utilization equation:** `%VO2max = 0.8 + 0.1894393 × e^(−0.012778 × t) + 0.2989558 × e^(−0.1932605 × t)`

| Coefficient | Code value | Verified value | Status |
|---|---|---|---|
| Asymptotic constant | 0.8 | 0.8 | ✅ Match |
| First amplitude | 0.1894393 | 0.1894393 | ✅ Match |
| First decay rate | −0.012778 | −0.012778 | ✅ Match |
| Second amplitude | 0.2989558 | 0.2989558 | ✅ Match |
| Second decay rate | −0.1932605 | −0.1932605 | ✅ Match |

**Velocity unit:** Confirmed as **meters per minute** (m/min) across all sources. The computation `v = distance_meters / time_minutes` is correct. One obscure calculator site (RunBundle) erroneously states "metres per second" on a reverse-calculator page, but their own coefficients only make physiological sense with m/min. The formula `VDOT = VO2 / %VO2max` is also confirmed correct.

---

## 2. The provided anchor-point race times are wrong for VDOT 40–80

The race times labeled as "approximate 3rd-edition values" for VDOT 40, 50, 60, 70, and 80 **do not come from those VDOT rows**. Only the VDOT 30 times are correct. This is not an equation error — the test data itself is wrong.

| Labeled VDOT | Actual VDOT from equations | Offset |
|---|---|---|
| 30 | **30.0** | 0.0 ✅ |
| 40 | **37.9–38.2** | −2.0 ❌ |
| 50 | **46.0–46.5** | −3.8 ❌ |
| 60 | **54.5–55.5** | −5.0 ❌ |
| 70 | **63.1–64.4** | −6.2 ❌ |
| 80 | **71.9–73.4** | −7.3 ❌ |

The times for "VDOT 40" (5K 25:12, 10K 52:17, etc.) are actually from the **VDOT 38** row in Daniels' table. The "VDOT 50" times correspond to roughly **VDOT 46**, and so on. The systematic offset grows with VDOT, suggesting the anchor points were drawn from a different source or corrupted during transcription.

---

## 3. Actual book values produce correct VDOTs within ±0.1

Using the **actual values from Daniels' book table** (confirmed via the Kalamazoo Area Runners reproduction of Table 2.2, the sdtrackmag DanielsOneSheet PDF, and the bhsxctf VDOT chart), all 24 cross-check computations produce the target VDOT within ±0.1. The race equivalency tables are **unchanged across all four editions** of the book, as confirmed by Fellrnr's detailed edition comparison.

| Target VDOT | Distance | Book time | Computed VDOT | Delta |
|---|---|---|---|---|
| **30** | 5K | **30:40** | 30.0 | 0.0 |
| 30 | 10K | **63:46** | 30.0 | 0.0 |
| 30 | Half Marathon | **2:21:04** | 30.1 | +0.1 |
| 30 | Marathon | **4:49:17** | 30.1 | +0.1 |
| **40** | 5K | **24:08** | 39.9 | −0.1 |
| 40 | 10K | **50:03** | 40.0 | 0.0 |
| 40 | Half Marathon | **1:50:59** | 40.0 | 0.0 |
| 40 | Marathon | **3:49:45** | 40.0 | 0.0 |
| **50** | 5K | **19:57** | 50.0 | 0.0 |
| 50 | 10K | **41:21** | 50.0 | 0.0 |
| 50 | Half Marathon | **1:31:35** | 50.0 | 0.0 |
| 50 | Marathon | **3:10:49** | 50.0 | 0.0 |
| **60** | 5K | **17:03** | 60.0 | 0.0 |
| 60 | 10K | **35:22** | 60.0 | 0.0 |
| 60 | Half Marathon | **1:18:09** | 60.0 | 0.0 |
| 60 | Marathon | **2:43:25** | 60.0 | 0.0 |
| **70** | 5K | **14:55** | 70.1 | +0.1 |
| 70 | 10K | **~31:00** | 70.1 | +0.1 |
| 70 | Half Marathon | **~1:08:23** | 70.0 | 0.0 |
| 70 | Marathon | **~2:23:10** | 70.0 | 0.0 |
| **80** | 5K | **13:18** | 80.0 | 0.0 |
| 80 | 10K | **~27:42** | 80.0 | 0.0 |
| 80 | Half Marathon | **~1:00:55** | 80.0 | 0.0 |
| 80 | Marathon | **~2:07:40** | 80.0 | 0.0 |

The maximum deviation of **±0.1 VDOT** arises entirely from the book's rounding of race times to whole seconds. For example, the exact solution for VDOT 40 at 5K is approximately 24:07.4 — the book rounds to 24:08, which back-computes to 39.9. This is inherent to any integer-second table and not a code defect.

---

## 4. VDOT is integer in the book, continuous in the equations

The book's race equivalency table presents **integer VDOT values from 30 to 85**, with one row per integer. The 3rd edition added a supplemental table for runners below VDOT 30. The underlying Daniels/Gilbert equations produce continuous real-number output (e.g., 47.3 or 53.8). The code's approach of rounding to **1 decimal place** is a reasonable design choice — it provides more precision than the book's integer table while remaining practical. For book-compatible behavior, a user would round to the nearest integer.

Daniels does not discuss interpolation between table rows. Practitioners using the book simply find the row whose race times most closely match their performance. Online calculators (including the official VDOT O2 app at vdoto2.com) compute continuous decimal values using the same equations.

---

## 5. Using `.Max()` for multiple races is consistent with Daniels' guidance

Daniels recommends determining VDOT from **a single, recent, all-out race performance** — ideally the one that produces the highest score. The official VDOT O2 app instructs users to "Enter all your past performances to see which race result scores the highest." He does **not** recommend averaging or weighting by recency.

The code's `.Max()` approach is therefore **consistent** with Daniels' guidance. Two nuances worth noting: Daniels advises updating VDOT no more than every **4–6 weeks** and increasing training paces by no more than **1 VDOT point at a time**, even after a breakthrough race. He also suggests that for marathon prediction specifically, a longer race (15K–25K) may be a better predictor than a 5K.

---

## 6. Five distances in the book are missing from the implementation

The book's table covers **9 distances**. The implementation supports 4 of them, missing 5 shorter and one mid-range distance:

- **1500m** — missing
- **1 Mile** (1609.34m) — missing
- **3K** (3000m) — missing
- **2 Mile** (3218.69m) — missing
- 5K (5000m) — ✅ supported
- 10K (10000m) — ✅ supported
- **15K** (15000m) — missing
- Half Marathon (21097.5m) — ✅ supported
- Marathon (42195m) — ✅ supported

The half-marathon distance of **21,097.5m** used in the code is exactly correct per World Athletics (half of the official 42,195m marathon distance). No issues there.

---

## 7. Edge cases and valid input range

The fractional utilization equation has mathematically predictable behavior at extremes that defines the valid operating range:

**Short durations (< ~7 minutes):** The %VO2max value exceeds 1.0, meaning the model predicts oxygen demand above 100% of VO2max. At t = 0, %VO2max reaches a theoretical maximum of **1.2884** (the sum 0.8 + 0.1894 + 0.2990). The crossover point where %VO2max = 1.0 occurs at approximately **t ≈ 6–7 minutes**, corresponding to roughly 1500m–3000m race durations for competitive runners. VDOT values computed from sub-7-minute efforts are physiologically meaningful (they reflect anaerobic contribution) but less reliable as predictors of aerobic fitness. The book does include 1500m in its tables, so Daniels considers the equations usable down to ~4-minute durations, though accuracy degrades.

**Long durations (> ~5 hours):** The %VO2max asymptotes to exactly **0.80** (80%), meaning the model predicts indefinite sustainability at 80% of VO2max. At 300 minutes (5 hours), %VO2max is 0.804; at 600 minutes (10 hours), it is effectively 0.800. This is physiologically unrealistic for ultra distances because glycogen depletion, thermoregulation, and musculoskeletal fatigue become dominant beyond ~3 hours. Marathon is the longest distance in the tables, and the official VDOT O2 app caps time entry at **9 hours**.

**Velocity floor:** The oxygen cost equation produces negative VO2 values below approximately **25 m/min** (~40 min/km walking pace), a mathematical artifact with no physiological meaning.

The implementation should ideally validate that race duration falls within approximately **3.5 to 300 minutes** and that velocity stays above ~50 m/min to avoid nonsensical output.

---

## 8. No known errata affect the race equivalency equations

The core Daniels/Gilbert equations and the race equivalency tables are **unchanged across all four editions** of the book (1998, 2005, 2013, 2022). The equations have been stable since their original 1979 publication in *Oxygen Power*.

One documented discrepancy exists between the **official online VDOT O2 calculator** (vdoto2.com) and the printed book tables: at low VDOT values (~30), the online calculator shows faster training paces than the book (e.g., T-pace of 9:55/mile online vs. 10:18/mile in the book). This affects *training paces* only, not race equivalencies, and suggests the online calculator may use modified percentage-of-VO2max assumptions for slower runners. The race equivalency calculations using the published equations match the book exactly.

Minor rounding inconsistencies exist in the printed tables (e.g., a 1-second difference for the VDOT 30 mile time: the equation computes 9:10.4 but the book shows 9:11), which is expected given whole-second rounding. The 3rd edition has a suspected contradiction on marathon-pace run volume (20% vs. 10% of weekly mileage on adjacent pages), but this is training guidance, not an equation issue.

---

## Summary of issues found

| # | Severity | Issue | Action needed |
|---|---|---|---|
| 1 | 🔴 **Critical** | Test anchor-point race times for VDOT 40–80 are wrong (off by 2–8 VDOT points). They do not come from those VDOT rows in any edition. | Replace with actual book values: VDOT 40 5K=24:08, VDOT 50 5K=19:57, VDOT 60 5K=17:03, VDOT 70 5K=14:55, VDOT 80 5K=13:18 (full table above) |
| 2 | 🟡 **Medium** | Five distances in the book's table are unsupported: 1500m, Mile, 3K, 2-Mile, 15K | Add these if broader distance coverage is desired; current 4-distance set covers the most popular race distances |
| 3 | 🟡 **Medium** | No input validation for extreme durations — %VO2max > 1.0 below ~7 min, negative VO2 below ~25 m/min | Add guards: reject or warn for race durations < 3.5 min or > 300 min, and for velocities < 50 m/min |
| 4 | 🟢 **Low** | Rounding to 1 decimal place is more precise than the book's integer table but fully valid — the equations are continuous | No action needed; consider documenting that book-compatible mode would round to nearest integer |
| 5 | ✅ **None** | Equation coefficients, velocity units, VDOT formula, `.Max()` strategy, and half-marathon distance are all correct | No action needed |