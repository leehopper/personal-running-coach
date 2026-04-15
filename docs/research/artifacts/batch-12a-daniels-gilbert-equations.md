# Daniels-Gilbert equations: a complete implementation reference

**The Daniels-Gilbert VDOT system rests on exactly two equations — a quadratic oxygen cost curve and a double-exponential duration curve — whose coefficients have remained unchanged since 1979.** Computing VDOT from a race result is a direct algebraic calculation requiring no iteration. Computing training paces requires inverting the quadratic (also analytical), while predicting equivalent race times requires Newton-Raphson iteration. The training zones are derived through a hybrid approach: Threshold and Interval paces use fixed %VO₂max targets, but Marathon and Repetition paces are race-prediction equivalents whose %VO₂max varies with VDOT level. This distinction is critical for a faithful implementation.

---

## The two core equations and all their coefficients

The entire VDOT system derives from two empirical regression equations published in Daniels & Gilbert's 1979 monograph *Oxygen Power: Performance Tables for Distance Runners*.

### Equation 1: Oxygen cost of running

$$\text{VO}_2 = -4.60 + 0.182258 \cdot v + 0.000104 \cdot v^2$$

| Variable | Units | Description |
|----------|-------|-------------|
| VO₂ | ml O₂/kg/min | Oxygen demand at velocity v |
| v | meters per minute | Running velocity |

This quadratic models how oxygen demand increases with speed. The **negative intercept (-4.60)** means the equation is only physically meaningful above approximately 130 m/min (~7:40/km pace). The **quadratic term (0.000104v²)** captures the nonlinear rise in cost at higher speeds due to air resistance, ground reaction forces, and biomechanical inefficiency. The equation was calibrated against treadmill data from competitive distance runners and first published in Daniels' 1978 chapter in *Conditioning for Distance Running — the Scientific Aspects* (John Wiley & Sons).

### Equation 2: Sustainable fraction of VO₂max (the "Drop Dead Formula")

$$F = 0.8 + 0.1894393 \cdot e^{-0.012778 \cdot t} + 0.2989558 \cdot e^{-0.1932605 \cdot t}$$

| Variable | Units | Description |
|----------|-------|-------------|
| F | Dimensionless (decimal fraction) | Maximum sustainable fraction of VO₂max |
| t | minutes | Duration of all-out effort |

This models how the percentage of VO₂max a runner can sustain decreases with effort duration. The two exponential decay terms represent distinct physiological processes: the **fast-decaying term** (rate 0.1932605) captures rapid depletion of anaerobic/phosphocreatine reserves in the first few minutes, while the **slow-decaying term** (rate 0.012778) represents gradual aerobic fatigue over longer durations. The **asymptote at 0.80** means no runner can sustain less than ~80% of VO₂max in the model, regardless of duration. At t = 0, the sum equals **1.2884** (>100%), reflecting anaerobic energy contribution for very short efforts.

| Duration | F (%VO₂max) | Approximate event |
|----------|-------------|-------------------|
| 3.5 min | ~100% | 1500m |
| 5 min | ~98% | Mile |
| 12 min | ~95% | 5K (elite) |
| 30 min | ~90% | 10K |
| 60 min | ~88% | Half marathon (fast) |
| 120 min | ~84% | Marathon (elite) |
| 240 min | ~81% | Marathon (recreational) |

### The combined VDOT equation

$$\text{VDOT} = \frac{\text{VO}_2}{F} = \frac{-4.60 + 0.182258 \cdot v + 0.000104 \cdot v^2}{0.8 + 0.1894393 \cdot e^{-0.012778 \cdot t} + 0.2989558 \cdot e^{-0.1932605 \cdot t}}$$

where $v = \text{distance (m)} / t$ and $t$ is the race duration in minutes. **VDOT is not true VO₂max** — it is an "effective" VO₂max that combines aerobic capacity with running economy into a single performance index. A runner with excellent economy but moderate VO₂max may have the same VDOT as a runner with poor economy but high VO₂max. Typical VDOT values range from **30** (beginner, ~5:30 marathon) to **85** (world-class, ~2:04 marathon).

These eight coefficients (-4.60, 0.182258, 0.000104, 0.8, 0.1894393, 0.012778, 0.2989558, 0.1932605) have been verified across **10+ independent sources** including the GoldenCheetah C++ codebase, the PHP running-calculations library, Larry Simpson's mathematical analysis at simpsonassociatesinc.com, LetsRun forum reverse-engineering threads, and multiple academic papers citing the original work. **No refinements or updates to these coefficients have been published since 1979.**

---

## Computing VDOT from a race result requires no iteration

Given a race distance $d$ (meters) and finishing time $t$ (minutes), VDOT is a **direct algebraic calculation**:

```
v = d / t                                                          // velocity in m/min
VO2 = -4.60 + 0.182258 * v + 0.000104 * v * v                    // oxygen demand
F   = 0.8 + 0.1894393 * exp(-0.012778 * t)
          + 0.2989558 * exp(-0.1932605 * t)                       // sustainable fraction
VDOT = VO2 / F                                                    // result
```

No Newton-Raphson, no bisection — the forward computation is fully closed-form. This makes VDOT calculation from race results deterministic and trivially testable.

**Validation example**: A 5K (5000m) in 20:00 → v = 250 m/min → VO₂ = -4.60 + 0.182258(250) + 0.000104(250²) = -4.60 + 45.5645 + 6.50 = **47.4645** → F = 0.8 + 0.1894393·e^(-0.2556) + 0.2989558·e^(-3.865) = 0.8 + 0.14658 + 0.00627 = **0.95285** → VDOT = 47.4645 / 0.95285 = **49.81**.

**Valid input ranges**: The model was calibrated for distances from **1500m to the marathon**. Inputs outside this range will produce numerical outputs but with degrading accuracy. Below 800m, anaerobic metabolism dominates; beyond ~50K, fueling, thermoregulation, and structural fatigue override the aerobic model. For a safety-critical system, consider clamping inputs to 800m–50,000m and flagging results outside 1500m–42,195m as approximate.

---

## Inverting the oxygen cost equation is analytical, but predicting race times requires Newton-Raphson

### Velocity from VO₂ (analytical — quadratic formula)

To find the running velocity that demands a specific VO₂, rearrange the oxygen cost equation into standard quadratic form:

$$0.000104 \cdot v^2 + 0.182258 \cdot v + (-4.60 - \text{target\_VO}_2) = 0$$

Apply the quadratic formula, taking only the positive root:

$$v = \frac{-0.182258 + \sqrt{0.182258^2 + 4 \times 0.000104 \times (4.60 + \text{target\_VO}_2)}}{2 \times 0.000104}$$

Simplified:

$$v = \frac{-0.182258 + \sqrt{0.033218 + 0.000416 \times (4.60 + \text{target\_VO}_2)}}{0.000208}$$

This is exact — no iteration needed. The discriminant is always positive for physiologically meaningful VO₂ values (>0 ml/kg/min). Convert to pace: **min/km = 1000/v**, **min/mile = 1609.344/v**.

### Race time from VDOT and distance (Newton-Raphson required)

Predicting how fast a runner with a given VDOT would race a specific distance requires solving for time $t$ in:

$$\text{VDOT} = \frac{-4.60 + 0.182258 \cdot (d/t) + 0.000104 \cdot (d/t)^2}{0.8 + 0.1894393 \cdot e^{-0.012778 \cdot t} + 0.2989558 \cdot e^{-0.1932605 \cdot t}}$$

Here $t$ appears in both numerator (via velocity $v = d/t$) and denominator (the duration curve), creating a transcendental equation with no closed-form solution. The GoldenCheetah implementation uses **Newton-Raphson** with an analytically computed derivative via the quotient rule:

**Initial guess**: $t_0 = d \;/\; \text{vVDOT} \;/\; 0.9$, where $\text{vVDOT} = 29.54 + 5.000663 \cdot \text{VDOT} - 0.007546 \cdot \text{VDOT}^2$ is a regression approximation of velocity at VO₂max.

**Iteration**: $t_{n+1} = t_n - f(t_n)/f'(t_n)$ where $f(t) = \text{VO}_2(d/t) / F(t) - \text{VDOT}$ and $f'(t)$ is computed analytically using the quotient rule applied to the numerator and denominator functions. Convergence to **1e-3 minutes** (~0.06 seconds) typically requires fewer than 10 iterations. GoldenCheetah caps at 100 iterations as a safety bound.

The derivative $f'(t)$ has the form:

$$f'(t) = \frac{F(t) \cdot \text{VO}_2'(t) - \text{VO}_2(t) \cdot F'(t)}{F(t)^2}$$

where $\text{VO}_2'(t) = -0.000208 \cdot d^2 \cdot t^{-3} - 0.182258 \cdot d \cdot t^{-2}$ and $F'(t) = -0.012778 \times 0.1894393 \cdot e^{-0.012778t} - 0.1932605 \times 0.2989558 \cdot e^{-0.1932605t}$.

---

## Training zones use a hybrid derivation strategy

This is the most nuanced aspect of the Daniels system and the most commonly misunderstood. **Different zones are derived by different methods.** The official vdoto2.com descriptions, combined with reverse-engineering of the published tables, reveal a three-part approach.

### Zones derived from fixed %VO₂max (analytical)

**Easy (E) pace** is defined as a range: **59–74% of VDOT**. To compute the pace band, set target_VO₂ = 0.59 × VDOT and target_VO₂ = 0.74 × VDOT, then invert the quadratic for each to get the slow and fast bounds of the E pace range. Daniels emphasizes E pace is a range, not a point — runners should stay within the band.

**Threshold (T) pace** uses approximately **88% of VDOT**. Daniels describes it as "the intensity sustainable for about 60 minutes of racing" and "83–88% effort." The LetsRun reverse-engineering analysis confirmed that 88% reproduces the published T-pace tables well across VDOT 36–85. This convergence is not coincidental: at 88% VO₂max, the duration curve predicts a sustainable effort of roughly 50–60 minutes, which is exactly Daniels' race-equivalent definition. To compute: target_VO₂ = 0.88 × VDOT → invert quadratic → velocity → pace.

**Interval (I) pace** uses approximately **97.5–100% of VDOT**. Daniels describes it as "the speed at which you would race a distance requiring about 10–12 minutes of effort." At 100% VO₂max, the velocity equals vVO₂max, which can be found by solving: VDOT = -4.60 + 0.182258v + 0.000104v² for v. GoldenCheetah uses 98% of vVDOT as its approximation. For implementation, using target_VO₂ = VDOT (100%) and inverting the quadratic produces I pace directly.

### Zones derived from race predictions (Newton-Raphson required)

**Marathon (M) pace** is explicitly defined by Daniels as "the run speed you anticipate maintaining in the upcoming marathon race." It is computed by predicting the equivalent marathon time for the runner's VDOT using the Newton-Raphson solver at distance 42,195m, then dividing: pace = 42,195 / predicted_time. The resulting %VO₂max is **not constant** — it ranges from ~80.7% at VDOT 30 to ~84.3% at VDOT 85, because faster runners finish in less time and can sustain a higher fraction of VO₂max over the shorter duration. Using a fixed 82% is a reasonable approximation but will not exactly match the published tables.

**Repetition (R) pace** is defined as "about 1500m or mile race pace." It is computed by predicting the equivalent mile (1609m) or 1500m race time for the runner's VDOT, then deriving pace from that prediction. Because 1500m/mile race duration is short (~3.5–7 minutes depending on ability), the sustainable %VO₂max exceeds 100% in the model — typically **105–110%** — reflecting anaerobic energy contribution. GoldenCheetah approximates R pace as 105% of vVDOT for distances ≥400m and 107% for 200m repeats.

**Fast Repetition (FR) pace**, introduced in later editions, equals predicted 800m race pace.

### The practical implementation approach

For maximum fidelity to Daniels' published tables:

- **E pace**: Invert quadratic at 59% and 74% of VDOT → pace range
- **M pace**: Newton-Raphson to predict marathon time → pace (or approximate with ~82% VDOT)
- **T pace**: Invert quadratic at 88% of VDOT → pace
- **I pace**: Invert quadratic at 100% of VDOT → pace (or use 97.5% for a slightly conservative target)
- **R pace**: Newton-Raphson to predict 1609m time → pace (or approximate with 105% of vVO₂max)

For the GoldenCheetah simplified approach (less accurate but simpler): compute vVDOT = 29.54 + 5.000663·VDOT - 0.007546·VDOT², then multiply by [0.72, 0.85, 0.90, 0.98, 1.05] for [E, M, T, I, R] zones. This uses **velocity fractions rather than VO₂ fractions** and produces reasonable but not table-exact results.

---

## The official vdoto2.com calculator diverges from published tables

The Run SMART Project's calculator at **vdoto2.com** does not publish its formulas. Daniels has been protective of his intellectual property — he requested fellrnr.com remove its VDOT calculator and VDOT is a registered trademark. However, the underlying equations are widely known from the 1979 publication.

**Known discrepancies between the online calculator and the book tables** are significant at low VDOT values. At VDOT 30, the online calculator shows T pace of **9:55/mile** versus **10:18/mile** in the book; I pace for 400m shows **2:11** online versus **2:22** in the book. At high VDOT values (60+), the calculator aligns well with the book. The tlgs/vdot GitHub repository author confirmed in July 2023 that the "official calculator recommends non-sensical training paces for low values of VDOT."

This suggests the online calculator uses **modified training pace derivations** for slower runners, possibly incorporating coaching experience about minimum useful training intensities that the pure equations would set too slow. No public documentation explains these modifications. For a C# implementation aiming to validate against vdoto2.com, expect exact agreement at mid-to-high VDOT values (45+) but systematic differences at low VDOT values.

The **printed book tables also contain errors**. The tlgs/vdot repository documents discrepancies in the 3rd edition of *Daniels' Running Formula*. One LetsRun analysis found that the equation produces a mile time of **9:10** for VDOT 30, while the book prints **9:11** — a 1-second rounding discrepancy. Some table progressions show irregular jumps that suggest manual adjustment or transcription errors rather than strict equation output.

---

## Open-source implementations provide battle-tested reference code

Twelve independent open-source implementations were identified. The most useful for a C# implementation are:

**GoldenCheetah** (C++, GPL v2, 2.1K GitHub stars) provides the most complete reference implementation in `VDOTCalculator.cpp`. It includes the forward VDOT calculation, Newton-Raphson race time prediction with full analytical derivative, the vVDOT regression approximation, and simplified training zone computation. Its Newton-Raphson uses convergence tolerance of 1e-3 minutes with a 100-iteration cap and initial guess of distance/vVDOT/0.9. This is the closest to a production-grade implementation.

**tlgs/vdot** (Python, 0BSD license) is notable for its Jupyter notebooks exploring the mathematics and its author's documentation of errors in both the published book tables and the official online calculator. This is the best source for validation test cases.

**st3v/running-formulas-mcp** (Python, MIT license) implements both Daniels and McMillan methodologies with a test suite covering E/M/T/I/R pace computations. It exposes `daniels_calculate_vdot`, `daniels_calculate_training_paces`, and `daniels_predict_race_time` functions.

**Runalyze** (PHP, large open-source running analytics platform) implements VDOT with unique additions: a configurable correction factor (0.85–0.95) for non-race efforts, heart-rate-adjusted VDOT, and elevation correction. Its `paceAt($percentVDOT)` method provides the quadratic inversion.

**lsolesen/running-calculations** (PHP) contains a clean, minimal implementation but rounds VDOT to 2 decimal places using `number_format()`, returning a string — a subtle bug that causes precision loss in downstream calculations. Avoid this pattern.

All equation-based implementations use identical coefficients. None reported needing to adjust the coefficients to match published outputs.

---

## Where the model breaks down and what to watch for

**Short distances (800m, mile)**: The model was calibrated on 1500m–marathon data. At 800m, anaerobic metabolism contributes ~40% of energy, violating the model's primarily-aerobic assumption. The duration curve predicts >100% VO₂max for efforts under ~3.5 minutes, which is numerically valid but physiologically represents anaerobic "borrowing." VDOT computed from an 800m race will be less reliable than from a 10K.

**Ultra distances (50K+)**: The duration curve asymptotes at 80% VO₂max, but real ultra performance degrades far beyond this due to glycogen depletion, structural fatigue, sleep deprivation, and fueling logistics. Fellrnr.com uses David Cameron's model for ultra predictions instead. **VDOT-predicted ultra times will be systematically too fast.**

**The marathon gap**: For recreational runners, VDOT systematically **overestimates marathon capability** when derived from shorter races. Analysis of real-world data shows half-marathon-derived VDOT predictions miss actual marathon times by **7–20 minutes (6–7%+)**. The error grows for slower runners. The root cause is the model's one-size-fits-all endurance curve — a 5K specialist and a marathon specialist with identical 5K times have the same VDOT but very different marathon capacities. Many coaches derate VDOT by 1–3 points for marathon predictions.

**Extreme VDOT values**: Below VDOT ~30, paces become very slow and the oxygen cost equation approaches its nonphysical region. Above VDOT ~85, few data points exist for calibration. The published tables cover VDOT 30–85. For a safety-critical system, flag VDOT values outside **25–90** as potentially unreliable and consider preventing training pace generation outside **30–85**.

**Gender and runner type**: The system uses **one set of equations for all runners** regardless of gender, age, or training background. Speed-type versus endurance-type runners will see different prediction accuracy across distances. The IAAF scoring tables demonstrate that male and female performance curves diverge, particularly at extreme distances.

---

## Key academic citations and publication history

The foundational work is **Daniels, J. and Gilbert, J. (1979). *Oxygen Power: Performance Tables for Distance Runners*. Tempe, AZ.** This was a **self-published, spiral-bound pamphlet** of approximately 82 pages, primarily numerical tables. It was sold via advertisements in *Runner's World* magazine. It was **not peer-reviewed** and is not indexed in PubMed. WorldCat lists the publisher as "J. Daniels, J. Gilbert, [Tempe, Ariz.]." Jack Daniels was an exercise physiologist with a PhD from the University of Wisconsin-Madison (studied under Per-Olof Åstrand in Stockholm) and a two-time Olympic medalist in modern pentathlon. Jimmy Gilbert was a mathematician who contributed to the Apollo program at MIT's Instrumentation Laboratory and helped Daniels transform physiological data into the regression equations.

The oxygen cost equation was first published in **Daniels, J., Fitts, R., and Sheehan, G. (1978). *Conditioning for Distance Running — the Scientific Aspects*. John Wiley & Sons, New York.** This predates the *Oxygen Power* monograph by one year.

The training methodology was popularized through **Daniels, J. *Daniels' Running Formula*. Human Kinetics.** Published in four editions: 1st (1998), 2nd (2005), 3rd (2014), and 4th (2022, ISBN 9781718203662). The books publish VDOT tables but **not the underlying equations**. The equations and coefficients remain unchanged across all editions; changes are limited to training plan structures, workout prescriptions, and expanded scope (the 4th edition covers 800m through ultramarathon, ages 6–80, with 31 training plans).

Relevant validation and critique studies include **Chester, C.D. (2021), "A Mathematical Approach to Estimating Pace and Distances for Practice and Competition Running"** (R = 0.997 correlation, <1 sec/km average error versus published tables for VDOT 30–85); **Mulligan, Adam, and Emig (2018), "A minimal power model for human running performance," *PLoS ONE* 13(11): e0206645** (critiques VDOT's fixed-curve assumption and proposes a 4-parameter individual model); and **Vickers and Vertosick (2016), "An empirical study of race times in recreational endurance runners," *BMC Sports Science, Medicine and Rehabilitation*** (validates Riegel's power law for mile-to-half-marathon but finds over-optimism beyond).

---

## Conclusion: implementation blueprint for a C# VDOT calculator

The Daniels-Gilbert system is remarkably compact — **eight coefficients and two equations** produce the entire framework. For a C# implementation:

**Store the coefficients as named constants** with full precision. The oxygen cost equation uses three coefficients (-4.60, 0.182258, 0.000104) and the duration curve uses five (0.8, 0.1894393, 0.012778, 0.2989558, 0.1932605). These are the canonical values verified across all known implementations.

**VDOT from race result** is a single-pass calculation with no iteration. **Training paces for E, T, and I zones** use the analytical quadratic formula inversion at fixed %VO₂max targets (59–74%, 88%, and 100% respectively). **M and R paces** require Newton-Raphson to predict equivalent race times at 42,195m and 1609m. The Newton-Raphson converges reliably with the initial guess formula $t_0 = d / (29.54 + 5.000663 \cdot \text{VDOT} - 0.007546 \cdot \text{VDOT}^2) / 0.9$.

**Expect discrepancies** of 1–2 seconds versus the printed book tables (rounding artifacts) and larger differences versus the vdoto2.com calculator at low VDOT values (<40). Build your validation suite against the GoldenCheetah C++ implementation and the tlgs/vdot Python notebooks, not solely against the official calculator. For safety, clamp inputs to the model's calibrated range (1500m–42,195m, VDOT 30–85) and present results outside this range with explicit uncertainty warnings.