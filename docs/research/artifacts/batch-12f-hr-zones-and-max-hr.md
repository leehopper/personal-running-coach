> **User-facing rename (2026-04-15):** References to "VDOT" in this document are preserved as historical technical vocabulary. Per DEC-043, the project's user-facing surface now uses "Daniels-Gilbert zones" or "pace-zone index" terminology to avoid exposure to The Run SMART Project LLC trademark (Runalyze enforcement precedent). Artifact body retained as-is for research provenance.

# Daniels' HR zones, max HR formulas, and what to implement

**Jack Daniels' 4th edition provides %HRmax ranges for each training zone but deliberately avoids recommending any age-based max HR formula — he insists on field testing.** This means an AI running coach needs to look beyond Daniels for its default HRmax estimation, and the best evidence-backed replacement for `220 - age` is the Tanaka formula (`208 - 0.7 × age`), validated across 18,712 subjects. Critically, HR zones should be implemented as a separate, independent system from VDOT pace zones — Daniels treats them as parallel intensity markers, not coupled calculations. Every major coaching authority and recent exercise physiology research supports this architecture.

## What Daniels' 4th edition actually says about heart rate

Daniels is fundamentally a pace-based coach. His VDOT system derives training paces from race performances, and those pace tables are the book's centerpiece. Heart rate appears in **Chapters 3–4** as a supplementary intensity marker, not the primary prescription tool.

For max HR estimation, Daniels recommends a practical field test: **"Run several hard 2-minute uphill runs. Get a heart-rate reading at the top of the first hill run, and if your heart rate is higher the second time up, go for a third time."** He does not endorse 220−age, Tanaka, or any formula. His position is that knowing your actual max HR through testing is the only reliable way to use HR for training.

The book provides **%HRmax ranges** for four of the five training intensities:

| Zone | %VO₂max | %HRmax | Notes |
|------|---------|--------|-------|
| **Easy (E)** | 59–74% | **65–79%** | Base building, recovery |
| **Marathon (M)** | 75–84% | **80–85%** | Some sources report up to 90% |
| **Threshold (T)** | 83–88% | **88–92%** | "Comfortably hard" |
| **Interval (I)** | 95–100% | **98–100%** | Just below max HR |
| **Repetition (R)** | >100% | **N/A** | Too short for HR to stabilize |

These ranges are **independent of VDOT**. A runner's VDOT determines specific paces in minutes per mile; the %HRmax ranges are fixed bands that apply universally. Daniels does not provide HR lookup tables like his extensive pace tables. His direct quote captures the philosophy: *"I prefer using VDOT values to determine speeds of training, but HR during sessions of training can be useful because conditions play a role in exercise HR."* Heart rate serves as a governor — detecting when heat, hills, fatigue, or overtraining make a given pace physiologically harder than intended.

## Why 220−age should be replaced, and with what

The `220 - age` formula has no scientific basis. Robergs and Landwehr (2002) traced its origin to Fox, Naughton, and Haskell's 1971 review paper, where it was an observational estimate sketched from roughly 11 references. The authors themselves noted "no single line will adequately represent the data." Its **standard error of estimate (SEE) ranges from 7–12 bpm**, meaning the 95% confidence interval spans **±20–24 bpm** — a range so wide it can misplace a runner's easy zone ceiling by an entire training zone.

The **Tanaka formula (`208 - 0.7 × age`)** is the strongest replacement. Published in 2001 in the Journal of the American College of Cardiology, it combined a meta-analysis of **351 studies covering 18,712 subjects** with a cross-validation study of 514 subjects in controlled laboratory conditions. Key advantages over 220−age: it does not systematically overestimate HRmax in younger adults or underestimate it in older adults (the primary flaw of 220−age). Tanaka found **no significant sex difference** and no significant difference between sedentary, active, and endurance-trained populations.

The **HUNT formula (`211 - 0.64 × age`)** from Nes et al. (2013) offers a credible alternative, derived from 3,320 directly-tested subjects in Norway. It predicts slightly higher HRmax than Tanaka at older ages. The Gellish formula (`207 - 0.7 × age`, 2007) independently confirms Tanaka's slope with a nearly identical equation from longitudinal data.

However, the uncomfortable engineering truth is that **no formula substantially outperforms any other at the individual level**. A 2025 PLOS ONE comparison of 7 equations (n=230) found all non-sex-specific equations explained only R² = 0.40–0.45 of variance, with limits of agreement spanning ±18–24 bpm regardless of formula. The HERITAGE Family Study found SEE of **12.4 bpm for Fox vs. 11.4 bpm for Tanaka** — an improvement, but not transformative. This is why Daniels, Pfitzinger, Friel, and Fitzgerald all recommend field testing over any formula.

| Formula | SEE (bpm) | Sample basis | Best for |
|---------|-----------|-------------|----------|
| 220 − age (Fox) | 7–12 | ~35 data points, not regression | Legacy compatibility only |
| **208 − 0.7 × age (Tanaka)** | **~10** | **18,712 subjects meta-analysis** | **Best general default** |
| 211 − 0.64 × age (HUNT) | 10.8 | 3,320 direct measurements | Active populations |
| 207 − 0.7 × age (Gellish) | ~10 | 132 subjects longitudinal | Confirms Tanaka |
| 206 − 0.88 × age (Gulati) | 11.8 | 5,437 women | Poor validation performance |

**Recommendation for `PaceCalculator.EstimateMaxHr(age)`**: Replace `220 - age` with `208 - (0.7 * age)` (Tanaka). Round to nearest integer. Always display a caveat about ±10 bpm error and prompt users to input their tested max HR.

## HR zones should be a separate calculator, not coupled to VDOT

The architecture question has a clear answer: **implement HR zones as an independent system, parallel to but separate from VDOT pace zones.** This is supported by Daniels' own design (fixed %HRmax ranges independent of VDOT), by every major coaching authority, and by the underlying physiology.

The rationale is straightforward. VDOT-derived pace zones reflect an integrated output — muscular efficiency, running economy, aerobic capacity, and biomechanics all contribute to race performance. Heart rate reflects cardiovascular stress alone. These frequently diverge in practice: a runner's VDOT Easy pace may produce 80%+ HRmax on hot days, at altitude, or when fatigued, while the same runner might cruise at 70% HRmax at the same pace on a cool morning. Cardiac drift causes HR to rise **10–20 bpm** during runs exceeding 60–90 minutes even at constant pace, due to dehydration, thermoregulation, and glycogen depletion.

A `HeartRateZoneCalculator` class should accept a max HR (estimated or tested) and optionally a resting HR, then output zone boundaries. It should not depend on VDOT. During workouts, the application can display both metrics and alert when HR exceeds the expected range for the current workout type — functioning as a physiological governor atop the pace-based training structure.

**For zone derivation method**, two approaches are defensible. **%HRmax** is simpler (requires only max HR) and aligns directly with Daniels' published ranges. **%HRR (Karvonen method)** is physiologically superior because %HRR approximates %VO₂Reserve rather than %VO₂max, accounting for individual resting HR variation. Swain and Leutholtz (1997) demonstrated this equivalence definitively: %HRR vs %VO₂R had an intercept of −0.1 and slope of 1.00, essentially a perfect identity relationship. The ACSM endorses both methods. The practical recommendation: implement %HRmax as the default (matching Daniels' published ranges), offer %HRR as an advanced option when resting HR data is available from wearable integration.

## How other coaching authorities handle HR zones

The broader coaching ecosystem has largely moved away from HRmax-based zones toward **lactate threshold heart rate (LTHR)** as the anchor point. Matt Fitzgerald's 80/20 system defines **7 zones as %LTHR**, determined via a 20-minute time trial. Its signature innovation is designating "Zone X" (the moderate-intensity gray zone between easy and threshold) as a zone to *avoid* — the trap where most recreational runners accidentally spend their time. Joe Friel's Training Bible system, which became the TrainingPeaks default, also uses **7 zones anchored to LTHR** from a similar time trial protocol.

Pete Pfitzinger provides both %MHR and %HRR ranges for 7 workout-specific intensities, preferring %HRR for its greater individualization. Notably, his ranges overlap by design — they describe different workout contexts, not rigid boundaries. Phil Maffetone's MAF method takes the most radical approach: a single aerobic zone defined as `180 - age ± modifier`, representing a 10 bpm window below the calculated MAF heart rate. Steve Seiler's polarized model uses just 3 zones anchored to ventilatory thresholds, ideally from lab testing.

For an application already built around Daniels' VDOT system, the most coherent approach is to use **Daniels' own %HRmax ranges** as the default HR zone definitions. These map directly to the same E/M/T/I/R framework the user is already familiar with, require only a max HR estimate, and avoid introducing a competing zone taxonomy. LTHR-based zones (Friel or 80/20 style) could be offered as an advanced option for users who have threshold test data.

## Concrete implementation recommendations

The research converges on five clear engineering decisions for the running coach application:

- **Replace `220 - age` with `208 - (int)(0.7 * age)`** (Tanaka formula) in `PaceCalculator.EstimateMaxHr(age)`. This is the minimum viable improvement backed by the strongest evidence base, endorsed by ACSM, and consistent with Daniels' emphasis on accuracy.

- **Create a separate `HeartRateZoneCalculator` class** that accepts `maxHr` (required) and `restingHr` (optional). When only maxHr is provided, output zones as Daniels' %HRmax ranges. When restingHr is also available, offer %HRR (Karvonen) zones as an option.

- **Use Daniels' five-zone model** (E/M/T/I/R) for HR zones, matching the existing pace zone framework. Default boundaries: E = 65–79% HRmax, M = 80–85% HRmax, T = 88–92% HRmax, I = 98–100% HRmax, R = no HR target.

- **Always allow user override of max HR** and prominently display the ±10 bpm estimation caveat. If the app observes HR exceeding estimated max during a workout, offer to update the stored max HR. Recommend the Daniels uphill field test (3 × 2-minute hard uphill efforts) for users who want accurate zones.

- **Treat HR and pace as parallel, independent systems** during workout execution. Display both. Use HR primarily as a ceiling for aerobic workouts (E, M, T) and as a recovery indicator for interval sessions (jog until HR drops to ~65% HRmax). Do not penalize cardiac drift after 60+ minutes — consider implementing TrainingPeaks-style pace:HR decoupling analysis as a fitness tracking metric.

## Conclusion

Daniels' 4th edition provides a clean, minimal HR zone framework — five fixed %HRmax bands layered on top of his VDOT pace system — but deliberately avoids prescribing how to estimate max HR. This gap is the opportunity: replacing the widely discredited `220 - age` with the Tanaka formula (`208 - 0.7 × age`) is an evidence-backed improvement that requires changing a single line of code. The deeper architectural insight is that HR zones belong in their own calculator, independent of VDOT, because they measure a fundamentally different dimension of training stress. The most sophisticated coaching systems (Friel, 80/20, TrainingPeaks) have moved to LTHR-based zones, which could be a future enhancement. But for an application already built around Daniels' methodology, his own %HRmax ranges provide an immediately implementable, internally consistent HR zone system that complements rather than competes with the existing VDOT pace infrastructure.