> **User-facing rename (2026-04-15):** References to "VDOT" in this document are preserved as historical technical vocabulary. Per DEC-043, the project's user-facing surface now uses "Daniels-Gilbert zones" or "pace-zone index" terminology to avoid exposure to The Run SMART Project LLC trademark (Runalyze enforcement precedent). Artifact body retained as-is for research provenance.

> **Errata (2026-04-15):** This artifact did not verify VDOT 30–49 cell values against any authoritative source — the recommendation was "likely correct, spot-check" based on step-size consistency alone. A 2026-04-14 computational audit found residual anomalies at the VDOT 49→50 boundary in the Interval (+1.55 pp) and Repetition (+3.69 pp) columns after the DEC-040 row-shift fix was applied. Cause: the fix was applied by shifting pre-existing (also erroneous) data rather than re-deriving from the book. Superseded by the equation-based rewrite direction in DEC-042. See R-025 (`batch-11-daniels-implementation-patterns.md`) and R-028 (`batch-12c-pace-zone-derivation-constants.md`) for the corrected path.

# Daniels VDOT 50 is an off-by-one row shift, not an edition mismatch

**The anomaly at VDOT 49→50 is a confirmed transcription error.** Every value in the VDOT 50 row actually contains VDOT 51's correct paces — and the user's VDOT 51 row contains VDOT 52's paces — indicating a systematic off-by-one row shift starting at VDOT 50 that likely propagates through the entire table to VDOT 85. The Daniels-Gilbert equations are unchanged across all four editions of the book, so mixing 3rd-edition VDOT formulas with 4th-edition pace tables is safe. No 5th edition exists as of March 2026.

---

## The off-by-one shift: what went wrong and how to fix it

The evidence for a row-shift error is unambiguous. Cross-referencing the user's data against the published Daniels tables (verified via the DanielsOneSheet reproduction of the book, the official race prediction tables, and independent equation computation) reveals an exact pattern:

| User's row | User's I-pace (s/km) | Correct VDOT N value | Correct VDOT N+1 value | Match |
|---|---|---|---|---|
| VDOT 50 | **231** | 235 (3:55/km) | **231** (3:51/km = VDOT 51) | ✅ N+1 |
| VDOT 51 | **228** | 231 (3:51/km) | **228** (3:48/km = VDOT 52) | ✅ N+1 |

The same pattern holds for Marathon pace: the user's VDOT 50 Marathon of **267 s/km** matches a 3:07:39 marathon (VDOT 51's prediction), not the 3:10:49 marathon that corresponds to VDOT 50 (271 s/km). The user's VDOT 51 Marathon of **263 s/km** matches VDOT 52's 3:04:36. Every pace zone tested confirms the shift.

This explains the anomalous step sizes perfectly. The 49→50 jump appears 2–3× normal because it spans two VDOT levels (49→51 in the real data), while the 50→51 step appears normal because it spans 51→52 — a single-level gap.

**The corrected VDOT 50 entry (seconds per kilometer):**

| Zone | Current (wrong) | Corrected | Source / verification |
|---|---|---|---|
| **EasyMin** | 301 | **~306** | Interpolation from surrounding entries (step ≈ −5 s/km) |
| **EasyMax** | 331 | **~339** | Interpolation from surrounding entries (step ≈ −5 s/km) |
| **Marathon** | 267 | **271** | Race prediction: 3:10:49 ÷ 42.195 km = 271.3 s/km |
| **Threshold** | 250 | **255** | Book T-1000 = 4:15; equation at 88% VO₂max = 255.2 s/km |
| **Interval** | 231 | **235** | Book I-1000 = 3:55; equation at 98% VO₂max = 234.0 s/km |
| **Repetition** | 216 | **~218** | Book R-400 = 87s → 87 × (1000/400) = 217.5 s/km |

The highest-confidence corrections are **Threshold (255)** and **Interval (235)**, each verified by two independent methods: the published book tables and direct computation from the Daniels-Gilbert oxygen cost equations. Marathon (271) is also high-confidence, derived from the well-established race prediction.

**Critical implication: the entire table from VDOT 50 through VDOT 85 is likely shifted by one row.** Every entry at VDOT N probably contains the correct paces for VDOT N+1. The fix is not simply patching VDOT 50 — every row from 50 onward needs to be shifted back, and a genuine VDOT 85 row (currently missing) must be computed or sourced from the book.

---

## Edition consistency: the equations never changed

The Daniels-Gilbert equations originate from the 1979 monograph *Oxygen Power: Performance Tables for Distance Runners* and have remained **byte-for-byte identical** across all four editions of *Daniels' Running Formula* (1998, 2005, 2013, 2021). Multiple independent implementations on GitHub (GoldenCheetah, tlgs/vdot, mekeetsa/vdot, lsolesen/running-calculations) all use the same coefficients:

**Oxygen cost of running** (v in meters/min):
```
VO₂ = −4.60 + 0.182258·v + 0.000104·v²
```

**Fractional utilization** (T in minutes):
```
%VO₂max = 0.8 + 0.1894393·e^(−0.012778·T) + 0.2989558·e^(−0.1932605·T)
```

**VDOT** = VO₂ / %VO₂max

Because the underlying equations are unchanged, **using 3rd-edition VDOT calculation formulas with 4th-edition pace tables creates no mismatch**. A given race time produces the same VDOT in every edition. The race-time-to-VDOT lookup table is also unchanged.

What *did* change between editions is the Easy pace format: the 2nd edition published a single Easy pace (~74% VO₂max), while the **3rd edition onward defines Easy as a range** (approximately 65–79% VO₂max), creating the EasyMin/EasyMax pair. Threshold, Interval, and Repetition paces are unchanged across all editions. The 4th edition added ultramarathon/triathlon content and incorporated Run SMART Project refinements but did not alter the pace tables or formulas.

**Recommendation: standardize on the 4th edition** for both equations and pace tables. The equations are identical regardless, and the 4th edition represents the most current presentation of the training pace zones.

---

## Per-mile to per-kilometer conversion is not the culprit

The Daniels book publishes paces in multiple columns: per-mile, per-1000m, per-400m, and per-200m. The underlying computation uses metric velocity (meters per minute), so per-km and per-mile values are both derived from the same source — they are not simple conversions of each other. Both are independently rounded from the computed velocity.

When we examine the original per-mile and per-1000m data from the published tables, the 49→50 boundary is perfectly smooth:

- **T-pace per 1000m**: VDOT 48 = 4:24, 49 = 4:20, **50 = 4:15**, 51 = 4:11, 52 = 4:07 (steps: −4, −5, −4, −4)
- **I-pace per 1000m**: VDOT 48 = 4:03, 49 = 3:59, **50 = 3:55**, 51 = 3:51, 52 = 3:48 (steps: −4, −4, −4, −3)

No anomaly exists in the original data at any distance format. The anomaly is purely a transcription/data-entry error in the user's lookup table.

Rounding during mile-to-km conversion can introduce discrepancies of **1–2 seconds** at most (for example, 7:02/mile = 422s → 262.2 s/km, which rounds to either 262 or 263). This explains minor 1-second mismatches that may appear between different reproductions of the table but cannot explain the 9–12 second jumps observed at the VDOT 49→50 boundary.

**For maximum accuracy, use the per-1000m columns directly from the Daniels book tables** rather than converting from per-mile, since the per-1000m values are independently rounded by Daniels and are native to the metric system used by the equations. However, the practical difference is negligible (≤2 seconds).

---

## Cross-referencing confirms the error across all available sources

No online calculator or open-source implementation reproduces the user's VDOT 50 values. Every source converges on the corrected values:

- **DanielsOneSheet (sdtrackmag.com)** — a verbatim reproduction of the Daniels book training pace table — shows T-1000 = 4:15 (255s) and I-1000 = 3:55 (235s) for VDOT 50, with smooth step progressions throughout.
- **Fellrnr.com** explicitly states that vVO₂max for VDOT 50 corresponds to 3:55/km, confirming I-pace = 235 s/km.
- **Equation-based implementations** (GoldenCheetah, tlgs/vdot) compute Threshold pace at 88% VO₂max as 255.2 s/km and Interval pace at 98% VO₂max as 234.0 s/km for VDOT 50 — both within 1 second of the book table values.
- **Race prediction tables** (Kalamazoo Area Runners, boltontri.com) all give VDOT 50 marathon as 3:10:49, yielding 271 s/km — not the user's 267.
- The **tlgs/vdot Python package** on GitHub explicitly warns that "official tables (Daniels' Running Formula, 3rd edition) contain errors," though this refers to low-VDOT edge cases in the printed book, not the VDOT 50 range specifically. No published errata from Human Kinetics addresses the VDOT 50 region.

The only known errata in the printed book are minor: a rounding discrepancy at VDOT 30 (9:10 vs 9:11 per mile), an inconsistency between pages 205–206 on marathon training volume (10% vs 20% of weekly mileage), and small cross-distance prediction inconsistencies inherent to the model. None affect the VDOT 50 training paces.

---

## Recommended audit of the full VDOT 30–85 table

Because the error is a systematic row shift rather than an isolated typo, the full table requires auditing in two segments:

- **VDOT 30–49**: Likely correct. The 48→49 step sizes in the user's data (−5, −5, −5, −4, −4, −4) are consistent with the published tables. Spot-check a few values against the DanielsOneSheet or equation-computed paces to confirm.
- **VDOT 50–85**: Likely all shifted by one row. Every VDOT N entry probably contains VDOT N+1's paces. The simplest fix is to shift all rows back by one and recompute or source the missing VDOT 85 entry from the book. Alternatively, recompute the entire table from the Daniels-Gilbert equations using the known %VO₂max intensity zones (Easy: 65–79%, Marathon: ~80–84%, Threshold: ~88%, Interval: ~98%, Repetition: event-specific/~105%).

A secondary anomaly to watch for: **minor rounding inconsistencies of 1–3 seconds** may exist at scattered points throughout the table, depending on whether the per-km values were derived from the book's per-mile column, per-1000m column, or computed from equations. These are cosmetic and training-insignificant but should be documented for code review purposes.

## Conclusion

The VDOT 49→50 discontinuity is not a feature of the Daniels system or an edition mismatch — it is a data-entry error where an entire row was skipped, causing every pace from VDOT 50 onward to display the next VDOT level's values. The corrected VDOT 50 Threshold is **255 s/km** (not 250) and Interval is **235 s/km** (not 231), both verified by published tables and independent equation computation. The Daniels-Gilbert equations are unchanged since 1979, so the 3rd/4th edition formula mixing is a non-issue. The practical fix is to audit and re-shift every entry from VDOT 50 through 85, using the published book tables or equation-computed values as the authoritative source. For ongoing maintenance, consider computing paces directly from the Daniels-Gilbert equations rather than maintaining a static lookup table — this eliminates transcription errors entirely and matches the approach used by the official Run SMART Project calculator (vdoto2.com).