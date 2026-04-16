> **User-facing rename (2026-04-15):** References to "VDOT" in this document are preserved as historical technical vocabulary. Per DEC-043, the project's user-facing surface now uses "Daniels-Gilbert zones" or "pace-zone index" terminology to avoid exposure to The Run SMART Project LLC trademark (Runalyze enforcement precedent). Artifact body retained as-is for research provenance.

# Zone structure and VDOT continuity in Daniels' Running Formula

**The 4th edition of Daniels' Running Formula defines exactly five pace-table zones (E, M, T, I, R) but references up to nine training-type abbreviations in its plans, and VDOT is mathematically continuous despite the book presenting only integer tables.** For an AI running coach implementation, the five-zone assumption holds for the pace-table layer, but the computation layer should use direct equation evaluation rather than table interpolation. The official Daniels-affiliated calculator at vdoto2.com already outputs decimal VDOT values and provides a sixth zone (Fast Reps), confirming that the integer-only book presentation is a print-era simplification rather than a design constraint.

---

## The book defines five pace zones but nine training types

The 4th edition's VDOT pace tables contain columns for exactly **five intensity zones**: Easy/Long (E/L), Marathon (M), Threshold (T), Interval (I), and Repetition (R). However, the training plans reference up to nine abbreviations: **E, L, M, T, I, H, R, F, and ST**. The distinction matters: only five have table-derived paces, while the others are either effort-based, derived from an existing zone, or workout elements rather than zones.

**L (Long)** is not a separate intensity — the table headers explicitly read "E/L," and long runs use Easy pace. **H (Hard)**, introduced in the 3rd edition and carried into the 4th, is an effort-based approximation of I-pace with no dedicated table column. Daniels instructs runners to "go by feel and conservatively imagine 5K race pace." **F (Fast Repetition)** is approximately current 800m race pace, defined as roughly **3 seconds per 200m faster than R pace**. It appears as a sixth zone in the official vdoto2.com calculator and app but does not have its own column in the book's printed tables. **ST (Strides)** are ~20-second pickups at approximately R-pace effort with 60+ seconds recovery — a workout element, not a zone.

To the specific sub-questions:

- **Cruise intervals** are a workout format within the Threshold zone (repeated 1-mile efforts at T-pace with ~1 min rest), not a separate zone. **Tempo and threshold are synonymous** — same intensity, different structure (steady-state run vs. cruise intervals).
- **Long-run pace** equals Easy pace. **Recovery pace** falls under Easy pace. Neither has a separate table column or zone designation.
- **No pace exists between Marathon and Threshold**. Daniels explicitly stated that training at 10K pace "is not really better than alternating between threshold and Interval paces."

## Fourteen columns span the VDOT pace tables

The full VDOT training-pace table in the 4th edition contains approximately **13–14 columns** (including the VDOT identifier). The exact structure, confirmed through cross-referencing the sdtrackmag consolidated one-sheet against book descriptions:

| Zone | Sub-distance columns | Value format |
|------|---------------------|--------------|
| **E/L** | Mile pace, km pace | Pace range (e.g., 8:00–8:44) |
| **M** | Mile pace | Single pace value |
| **T** | 400m, 1000m, mile | Time per distance |
| **I** | 400m, 1000m, 1200m, mile | Time per distance |
| **R** | 200m, 400m, 800m | Time per distance |

Two important sparsity patterns exist in these tables. **I-mile** is only populated at approximately VDOT 46 and above, because at lower fitness levels an I-pace mile would exceed the practical interval duration ceiling. **R-800** is only populated at approximately VDOT 60 and above, since slower runners would exceed 2 minutes at R-pace for 800m, violating Daniels' rep-duration guidelines. Easy pace is unique in being expressed as a **range** (~59–74% VO₂max) rather than a single value — spanning roughly 76 seconds per mile at VDOT 30, narrowing to ~42 seconds per mile at VDOT 85.

The VDOT-to-race-equivalence tables (separate from training paces) map each integer VDOT to predicted finish times at **eight race distances**: 1500m, mile, 2-mile, 5K, 10K, 15K, half marathon, and marathon.

## Sub-distance paces are proportional, not independently derived

Within any single VDOT row, the sub-distance times for a given zone are derived from **one target velocity** and are therefore proportional up to rounding. Each zone's target velocity comes from inverting the oxygen-cost equation at a specific %VO₂max intensity. The sub-distance times are simply `time = distance / velocity`, rounded to whole seconds.

Verification at VDOT 50 (T-pace): T-400 = 102 seconds yields a velocity of 3.922 m/s. Projecting to T-1000 gives 255.0 seconds (4:15) — matching the table exactly. Projecting to T-mile gives 410.4 seconds (6:50.4) — the table shows 6:51, a difference explained entirely by rounding. This pattern holds consistently across zones and VDOT levels, with discrepancies never exceeding **1–2 seconds** and always attributable to independent rounding at each distance.

However, the %VO₂max target is **not perfectly fixed across all VDOT levels for every zone**. A detailed mathematical analysis from a LetsRun forum thread found that while E, T, and I paces use approximately constant %VO₂max targets, **Marathon pace varies from ~80.7% VO₂max at VDOT 30 to ~84.3% at VDOT 85**, and **Repetition pace** also shifts. This means M-pace and R-pace require iterative numerical solutions rather than simple algebraic inversion at a fixed percentage. For an implementation, E, T, and I can be derived from fixed %VO₂max targets, but M and R need VDOT-dependent percentage curves or lookup tables.

## VDOT is continuous by construction; integer tables are a print artifact

The Daniels-Gilbert oxygen-cost equations, first published in the 1979 monograph *Oxygen Power: Performance Tables for Distance Runners*, are:

**Oxygen cost:** `VO₂ = −4.60 + 0.182258 × v + 0.000104 × v²` (v = meters/minute)

**Sustainable fraction of VO₂max:** `%VO₂max = 0.8 + 0.1894393 × e^(−0.012778 × t) + 0.2989558 × e^(−0.1932605 × t)` (t = minutes)

**VDOT:** `VDOT = VO₂ / %VO₂max`

These coefficients are confirmed identically across more than ten independent sources, including GitHub implementations (tlgs/vdot, mekeetsa/vdot), the GoldenCheetah open-source project's `VDOTCalculator.cpp`, the simpsonassociatesinc.com mathematical exposition, and the RunBundle and OmniCalculator implementations. The ratio of a quadratic polynomial to a sum of exponentials is **continuous, smooth, and infinitely differentiable** over all physically meaningful inputs. VDOT produces arbitrary real-valued output — a 5K in 27:00 yields VDOT **34.963**, not 35.

The book's integer-only tables (VDOT 30 through 85) are a **discretization for print presentation**. Daniels intentionally omitted the mathematical formulas from all editions of *Daniels' Running Formula* — as one source notes, "don't expect to find his oxygen cost or 'drop dead' formulae spelled out mathematically. He only gives you the tables generated from the formulae." The 4th edition does not discuss fractional VDOT values, does not mention interpolation, and provides no guidance for performances falling between integer VDOT rows.

## The official calculator already treats VDOT as continuous

The official calculator at **vdoto2.com/calculator** (formerly runsmartproject.com/calculator), developed by The Run SMART Project in collaboration with Daniels, outputs **decimal VDOT values** — for example, Jacob Kiplimo's 57:31 half marathon maps to VDOT 85.6, and Joshua Cheptegei's 26:11 10K maps to VDOT 85.5. The companion **V.O2 app** (iOS/Android) similarly produces decimal VDOT and includes a premium feature to "calculate by VDOT" by entering any numeric value. The app uses decimal thresholds for its ten age-graded levels (e.g., Beginner-White: Female 34.3, Male 36.8).

Critically, the official calculator defines **six zones** rather than five, adding **Fast Reps (F)** alongside E, M, T, I, and R. It also provides paces at more sub-distances than the book — including 1200m, 800m, 600m, 400m, 300m, and 200m across multiple zones, plus temperature and altitude adjustments.

No first-party statement from Daniels explicitly addresses whether VDOT should be continuous or discrete. The closest evidence is a 2018 Q&A where Daniels advised making "a (1 unit) VDOT increase about every 4–6 weeks" without racing — suggesting **practical integer-step thinking** for coaching, though this does not constitute a mathematical claim. Daniels died on September 12, 2025, and no posthumous clarification has emerged.

## Linear interpolation works but direct computation is better

**Linear interpolation between adjacent integer VDOT rows is approximately valid** because the underlying functions are smooth with moderate curvature over 1-unit intervals. Standard interpolation error bounds (proportional to the second derivative times step-size squared) predict errors of **1–2 seconds per mile** at most — well within the ±20 seconds of flexibility Daniels himself recommended for Easy pace on any given day.

However, the nonlinear structure of the equations means interpolation introduces **systematic bias** that compounds for derived quantities. The mathematically correct approach — and the one used by every serious implementation including the official calculator — is **direct evaluation of the Daniels-Gilbert equations** for any real-valued VDOT. This eliminates interpolation error entirely and handles edge cases (very low or very high VDOT) more gracefully. The published tables themselves contain minor rounding artifacts: at VDOT 30, direct computation yields a mile time of 9:10.37, but the table shows 9:11, suggesting Daniels applied manual rounding or conservative adjustments during table generation.

For the AI running coach implementation, the recommended approach is: compute VDOT from race performance using the continuous equations, then derive training paces by inverting the oxygen-cost equation at zone-specific %VO₂max targets. Use Newton-Raphson iteration (as Daniels' original work did) to solve for race-equivalent times. Reserve table lookup with linear interpolation only as a fallback or validation check.

## Discrepancies between book and official calculator

| Dimension | Book (4th edition) | Official calculator (vdoto2.com) |
|-----------|-------------------|----------------------------------|
| VDOT precision | Integer only (30–85) | Decimal (e.g., 47.3, 85.6) |
| Training zones | 5 (E, M, T, I, R) | 6 (adds Fast Reps / F) |
| Sub-distance columns | ~13 across all zones | Expanded: adds 600m, 300m, and more |
| Interpolation | None; paces at integer VDOT only | Automatic via equation evaluation |
| Sparse cells | I-mile blank below ~VDOT 46; R-800 blank below ~VDOT 60 | Computed for all VDOT values |
| Environment adjustments | Static tables in book | Interactive temperature/altitude |
| H (Hard) zone | Referenced in plans, effort-based | Not separately listed as a calculator zone |

One additional discrepancy: Fellrnr.com documented that the official calculator recommended "non-sensical training paces for low values of VDOT" as of July 2023, and the published 3rd edition tables contained minor errors. These issues appear to stem from extrapolating the equations beyond their validated calibration range.

## Conclusion

The five-zone model (E, M, T, I, R) is the correct canonical representation for the pace-table layer. Fast Reps (F) exists as a sixth zone in the official digital ecosystem but not in the book's printed tables, and its pace is derivable from R-pace (subtract ~3 sec/200m). Sub-distance paces within each zone are proportional conversions from a single velocity, not independently computed — meaning an implementation needs only one velocity per zone plus distance-based conversion with rounding.

For VDOT handling, **the continuous-equation approach is both mathematically correct and consistent with first-party practice**. The official calculator's decimal output constitutes the strongest available evidence that Daniels' team endorsed continuous VDOT semantics. An implementation using direct Daniels-Gilbert equation evaluation will be more accurate than one interpolating between integer table rows, and the error from linear interpolation — while small enough for practical coaching — is unnecessary given that the equations are publicly known and computationally trivial. The one implementation caveat is that M-pace and R-pace use VDOT-dependent %VO₂max targets rather than fixed percentages, requiring either a sliding-percentage model or direct calibration against the published table values.