# Designing an AI running coach's knowledge layer

**Your core belief is correct — and undersold.** Training methodology should absolutely be configurable, not hardcoded. But the research reveals a three-tier architecture more nuanced than "configurable vs. fixed": universal physiological guardrails that no methodology violates, methodology-specific parameters the user or AI selects, and real-time coaching judgment that mirrors how human coaches adapt session-by-session. The 2024 Knopp et al. quantitative analysis of 92 marathon training plans (*Sports Medicine Open*) found that despite wildly different philosophies, **all plans converge on ~79% easy-zone training** — suggesting the guardrail layer is larger than most people assume, while the configurable layer is about *how* you implement shared principles, not *whether* you follow them. This report maps every parameter you need.

---

## 1. Methodology-by-methodology analysis

### Jack Daniels (VDOT system)

The most mathematically rigorous system. Daniels' central innovation is **VDOT**, a single fitness index combining VO₂max and running economy, derived from a recent race result. Every training pace flows from this number through lookup tables published in *Daniels' Running Formula* (4th ed., Human Kinetics, 2022). The system defines five zones — Easy (59–74% VO₂max), Marathon (75–84%), Threshold (83–88%), Interval (97–100%), Repetition (>100%) — each targeting a specific physiological adaptation. Phase I (Foundation) → Phase II (Early Quality, introducing R-pace) → Phase III (Transition Quality, peak stress with I-pace focus) → Phase IV (Final Quality, race-specific). Each phase lasts ~6 weeks in a 24-week cycle.

**What makes it distinct:** Absolute insistence that training paces reflect *current* fitness, never goal fitness. Volume caps per intensity zone: T-pace ≤10% of weekly mileage, I-pace ≤8%, R-pace ≤5%. Daniels recommends holding intensity constant for 4 weeks before progressing and recalculating VDOT every 4–8 weeks from new race results. The 2Q (Two Quality) marathon plans prescribe only two structured workout days per week, leaving scheduling flexible.

**Rigidity:** Paces must match current VDOT (non-negotiable). Phase sequence must be maintained. Zone volume caps are strict. **Flexibility:** Which days quality sessions fall on, mileage level, number of running days per week. Works across all distances (800m to ultra) and ability levels (VDOT 30 to 85+).

### Pete Pfitzinger

Science-driven and **lactate-threshold-centric**, Pfitzinger's system targets serious competitive runners aiming to race faster, not just finish. His signature innovation is the **medium-long run** (11–15 miles midweek) as a distinct training element beyond the long run. He prescribes eight workout categories — more granular than any other system — including general aerobic, lactate threshold (four variants: classic tempo, cruise intervals, LT hills, change-of-pace), VO₂max intervals, marathon-pace long runs, and recovery runs. Published in *Advanced Marathoning* (4th ed., Human Kinetics, 2024) and *Faster Road Racing* (2015).

**Periodization:** 12-, 18-, or 24-week plans at four mileage tiers (up to 55, 55–70, 70–85, 85+ mpw). Builds endurance first, adds LT work, then VO₂max closer to race day. Recovery weeks every 3rd week with ~30% mileage reduction. Long runs peak at **20–22 miles** with 3–5 runs at 20+ miles in a cycle — one of the highest long-run volumes of any system. LT pace ≈ 15K to half-marathon race pace.

**Rigidity:** Day-by-day plans are precisely laid out. Hard/easy alternation is strict. **Flexibility:** Multiple plan tiers allow self-selection. Recovery runs can be replaced with rest. Cross-training acceptable for some easy runs. Best suited for runners already at 25+ mpw with 12-mile long run capability.

### Hal Higdon

The accessibility champion. Higdon's philosophy is **simplicity and progressive distance building** — get accustomed to the distance and the rest follows. His Novice 1 plan is "arguably the most popular marathon training program used by first-time marathoners anywhere." The defining characteristic: **minimal pace prescription**. Novice plans contain zero speedwork. Runs are described simply as "Run X miles" at conversational pace. Walking breaks are explicitly encouraged.

Plans span Novice 1/2, Intermediate 1/2, and Advanced 1/2 tiers over 18 weeks with linear long-run progression peaking at 20 miles. Advanced plans add marathon-pace runs and Saturday/Sunday back-to-back long efforts. Peak mileage is relatively modest: Novice ~35–40 mpw, Advanced ~55–60 mpw. No proprietary scoring, no zone system, no HR targets.

**Rigidity:** Long run progression is the backbone. Rest days are prescribed. **Flexibility:** Everything else. Higdon explicitly says "don't be afraid to juggle workouts day to day." Best for: first-time marathoners, 4:00–5:30+ finishers, busy professionals. Not designed for aggressive time goals.

### Hansons Marathon Method

Built on **cumulative fatigue** — the idea that arriving at workouts mildly fatigued mimics late-marathon conditions. The most controversial feature: **long runs capped at 16 miles**, justified by the rule that long runs should not exceed 25–30% of weekly mileage and ~2.5–3 hours duration. Because the 16-miler follows days of steady-state training, the Hansons argue it simulates the *last* 16 miles, not the first.

All paces derive from **goal marathon pace** (not current fitness — a key difference from Daniels). Three SOS (Something of Substance) workouts weekly: speed intervals (5K–10K pace), strength intervals (~10 sec/mile faster than marathon pace), and tempo runs at marathon pace building from 5 to 10 miles. Six running days per week with only one rest day. Published in *Hansons Marathon Method* (2nd ed., VeloPress, 2016).

**Rigidity:** 6-day structure non-negotiable. 16-mile long run cap. Paces strictly prescribed. Racing during the cycle is discouraged. **Flexibility:** Goal pace adjustable. The "Just Finish" plan (added in 2nd edition) provides a gentler entry point. Best for: mid-pack runners (3:00–5:00 marathon), those who thrive on daily consistency. Key criticism: only 1 rest day per week creates burnout/overtraining risk for injury-prone runners, and basing paces on goal rather than current fitness risks overtraining if the goal is too ambitious.

### 80/20 Running (Fitzgerald/Seiler)

Grounded in **Stephen Seiler's research** on elite endurance athletes across multiple sports, the core finding is that athletes self-organize toward **~80% low-intensity, ~20% moderate-to-high-intensity** training. Matt Fitzgerald operationalized this in *80/20 Running* (Penguin, 2014) with a proprietary 7-zone system. Zones are anchored to lactate threshold heart rate (LTHR) determined via a 20-minute time trial. Three measurement options: pace, heart rate, or running power.

An important nuance: Seiler's original observations used a "session goal" approach (if the session's primary aim was high-intensity, the entire session counted as hard), yielding ~80/20 by session count. Fitzgerald operationalizes it by total training *time*, where the actual minutes in high zones may be closer to 90/10. Periodization runs base (~90/10) → peak (~80/20) → taper.

**Evidence base is the strongest** of any methodology: Muñoz, Seiler et al. (2014) found polarized training improved 10K times by 5% vs. 3.6% for threshold-focused training. Stöggl & Sperlich (2014) showed polarized produced greater improvements than threshold, HIT, or high-volume approaches. Neal et al. (2013) confirmed similar results in trained cyclists.

### Brad Hudson (Adaptive Running)

The explicit anti-template. Hudson's *Run Faster from the 5K to the Marathon* (Broadway Books, 2008, co-authored with Matt Fitzgerald) articulates **four pillars**: training targets precise physiological adaptations, programs adapt to individual strengths/weaknesses, training adapts daily based on athlete feedback, and training evolves season-to-season.

Key structural innovation: **non-linear periodization** where all training stimuli are present throughout the cycle but proportions shift — introductory (general fitness + hill sprints) → fundamental (peak volume) → sharpening (race-specific emphasis). Unlike Lydiard's block periodization, Hudson never abandons a training type entirely. His signature workout is the **8–10 second steep hill sprint** for neuromuscular power. Paces reference race-pace equivalents via the McMillan Running Calculator, not a proprietary system. Best for intermediate-to-advanced runners with self-awareness. Hudson explicitly rejects rigid cookie-cutter systems.

### Arthur Lydiard

**The foundational system**. Lydiard's sequential periodization — aerobic base (8–12+ weeks at 100+ miles/week for elites) → hill phase (4–6 weeks) → anaerobic/track phase (4–6 weeks) → coordination → racing — influenced virtually every subsequent methodology. His core insight: the aerobic system supports all race distances from 800m to marathon, and anaerobic capacity plateaus after 4–6 weeks while aerobic capacity continues building for months.

Crucially, Lydiard's base phase was **not "long slow distance."** It included varied efforts at ¼, ½, ¾, and ⅞ effort, plus strides. The ⅞ effort corresponds roughly to lactate threshold. He prescribed effort-based (not pace or HR) running and believed in daily running with no complete rest days. His athletes — Peter Snell, Murray Halberg, Barry Magee — won Olympic medals across 800m to marathon. Modern interpretation: *Healthy Intelligent Training* by Keith Livingstone is the definitive contemporary Lydiard text.

### MAF Method (Phil Maffetone)

The most conservative system. Maffetone's central thesis: most athletes are "fit but unhealthy." His **180 Formula** (MAF HR = 180 − age, adjusted ±5–10 based on health/fitness status) sets an absolute heart rate ceiling for all aerobic training. The base period (3–6+ months) permits *zero* training above the MAF HR. Progress is tracked via the **MAF Test**: 3–5 miles on a track at MAF HR, recording each mile split monthly.

Published in *The Big Book of Endurance Training and Racing* (Skyhorse, 2010). The 180 formula is **not derived from VO₂max, lactate threshold, or max HR** — it's an empirical heuristic. Criticism: too conservative for fit young athletes (producing very slow paces), lacks structured speed work. Best suited for beginners, injury-prone runners, ultramarathoners, and rehabilitation. Mark Allen (6× Ironman champion) credited MAF for his late-career breakthrough.

### Norwegian Method (lactate-guided double threshold)

The trending methodology. Pioneered by Marius Bakken and popularized through the Ingebrigtsen family's success, the key innovation is **threshold intervals at precisely controlled lactate levels** (2.0–3.5 mmol/L) measured with a portable lactate meter. This permits enormous volumes of threshold work without excessive fatigue. The structure features **double-threshold days** (AM + PM sessions) twice weekly within an 80/20 volume framework totaling ~180 km/week for elites. Bakken's sweet spot: 2.3–3.0 mmol/L. The system emphasizes internal physiological response (lactate) over external output (pace), meaning paces adjust session-to-session.

Requires a lactate meter for proper execution. Adaptable for recreational runners by replacing doubles with single threshold sessions. Evidence base is emerging: Casado, Foster, Tjelta & Bakken (2023) published a systematic review in the *Scientific Journal of Sport and Performance*.

### Renato Canova

The most race-specific system. Canova's philosophy: all non-specific training exists only to support race-specific training. His signature workout is the **long-fast run** at 87–105% of marathon race pace, with recoveries that are themselves run at near-marathon pace (85–91% RP). His "Special Blocks" — 45–50 km training days across two sessions at ~90% effort — are uniquely demanding. All workouts expressed as percentages of race pace.

Periodization runs general (1500m–5K pace intervals) → special (long-fast runs emerge) → specific (race-pace domination). Coached 50+ Olympic/World Championship medalists including Gelindo Bordin, Stefano Baldini, and Kenenisa Bekele. Not for beginners. Principles are adaptable for sub-elites using duration-based (not distance-based) adjustments and the same percentage-of-RP framework.

### Jeff Galloway (Run-Walk-Run)

The inclusivity methodology. Galloway's strategic walk breaks reduce cumulative impact stress and make running accessible to everyone. Walk breaks are taken from the start (not just when tired), with ratios from 30:30 seconds (beginners) to 8:1 minutes (experienced). The **Magic Mile** pacing assessment predicts race paces: marathon pace = Magic Mile time × 1.3. Galloway claims a 98%+ race completion rate in his programs. He was a 1972 US Olympian and ran 230+ marathons. Published in *Galloway's Book on Running* and *The Run Walk Run Method* (Meyer & Meyer, 2016).

---

## 2. Where methodologies agree vs. where they diverge

This is the architectural blueprint for your AI. Agreements become hard guardrails. Divergences become configurable parameters.

### Universal agreements (hard-code these)

Every serious methodology converges on these principles. The 2024 Knopp et al. analysis of 92 marathon plans (*Sports Medicine Open*) quantitatively confirmed that despite different labels, **all plans cluster around ~79% easy-zone training**.

**The majority of running volume must be easy/aerobic.** No credible system advocates majority-hard training. The floor is ~70% (Hansons' effective easy percentage) and the ceiling is 100% during MAF base phases. The practical universal minimum is **≥70% easy**.

**Easy running means conversational.** Every system uses some version of the talk test. Defined variously as 59–74% VO₂max (Daniels), below ventilatory threshold (Seiler), or 180−age HR (Maffetone), but the subjective marker — able to speak in complete sentences — is universal.

**A weekly long run is essential for marathon preparation.** No methodology eliminates it. All agree it develops glycogen depletion adaptations, fat-burning capacity, and musculoskeletal endurance.

**Hard days hard, easy days easy.** The polarized distribution principle holds across all systems. Running too much at moderate intensity ("black hole training") is universally identified as the most common recreational runner error.

**Training must progress from general to specific fitness.** From Lydiard forward, every system begins with aerobic foundation before race-specific work.

**A pre-race taper is necessary.** Research (Bosquet et al., 2007 meta-analysis in *Medicine & Science in Sports & Exercise*) confirms tapers improve performance by ~2–3%. All methodologies taper.

**During taper, maintain intensity but reduce volume.** Supported by Mujika (2002) and confirmed by all methodologies. Training frequency should stay at ≥80% of normal; session duration decreases.

**Recovery/cutback weeks are required.** No methodology advocates continuous weekly increases. Frequency varies (every 2nd to 4th week) but the principle is universal.

**Progressive overload drives adaptation.** Training must gradually increase stress. All systems agree, differing only on rate.

**Maximum 2–3 quality sessions per week** (counting the long run if it contains intensity segments). Daniels prescribes 2Q, Pfitzinger typically 2 + long run, Hansons prescribes 3 SOS workouts. No methodology prescribes 4+ hard days weekly for non-elite runners.

### Where methodologies genuinely diverge (make these configurable)

These divergences represent real philosophical disagreements. Each is a parameter the AI should be able to set per-user.

**Long run maximum distance** is the single most debated parameter. Hansons caps at 16 miles (based on 25–30% of weekly mileage rule). Higdon and Daniels peak at 20. Pfitzinger goes to 22 with 3–5 runs at 20+ per cycle. Galloway programs runs longer than marathon distance. **Range: 16–26+ miles.**

**Long run intensity** spans from purely easy (MAF, standard Higdon) to workout-embedded (Daniels' 2Q plans include tempo and MP segments within the long run). Pfitzinger includes 4–6 progressive long runs per cycle with MP finish. Hudson advocates progressive-finish long runs. **Range: strictly aerobic → heavily structured.**

**"Tempo" definition** varies enormously and is a common source of confusion. Daniels defines T-pace as ~1-hour race pace (roughly half-marathon effort). Pfitzinger targets ~15K to half-marathon pace. Hansons defines tempo as *goal marathon pace*. Hudson uses multiple threshold paces simultaneously. **The AI must disambiguate tempo for the user based on which methodology is active.**

**Periodization model** divides into three camps: linear/block (Lydiard, MAF — develop one quality at a time), modified linear (Daniels, Pfitzinger — primary focus per phase with maintenance of others), and non-linear/adaptive (Hudson — all stimuli present throughout, emphasis shifts). This is a fundamental architectural choice for plan generation.

**Base phase intensity allowance** ranges from zero above aerobic threshold (MAF, strictest) to including strides and short hill sprints from day one (Hudson, most permissive). Daniels allows only easy running + strides in Phase I. Lydiard's base included varied efforts up to ⅞ pace (near-threshold).

**Pace calibration basis** is a critical design choice. Daniels calibrates from current fitness via recent race results (VDOT). Hansons calibrates from *goal* marathon pace. Pfitzinger uses race pace relationships. MAF uses age-based heart rate formula. The Norwegian method uses real-time lactate readings. **For an AI coach, the Daniels approach (current fitness → paces) is safest as a default, since goal-based calibration risks overtraining when goals are unrealistic.**

**Recovery week frequency:** every 3rd week (Pfitzinger, masters runners) vs. every 4th week (standard coaching practice) vs. as-needed (Hudson). Volume reduction ranges from 15% (Higdon) to 30–40%.

**Taper length:** 10 days (RunnersConnect, aggressive) to 3 weeks (Pfitzinger, Higdon, conservative). The 2021 Strava study of 158,000 marathon runners found a strict 3-week taper associated with **5:32 faster** finish time vs. minimal taper.

**Cross-training role** ranges from zero (Lydiard purist) to integral (FIRST prescribes 3 runs + 2 cross-training days). Most systems treat cross-training as supplementary for injury-prone or time-limited runners.

---

## 3. How human coaches actually blend methodologies

### No coach follows one system

Every source confirms experienced coaches are eclectic blenders. Brad Hudson's *Adaptive Running* is the most explicit articulation: "there is no single training formula that works perfectly for every runner." Steve Magness describes training history as a pendulum between intensity and endurance, with swings getting smaller: "we are no longer arguing over pure endurance or pure intensity. We are arguing over the nuance."

The most common blending patterns observed across coaching literature and interviews:

- **Lydiard base + Daniels quality pacing.** The dominant approach. Coaches use Lydiard's aerobic base principles but calibrate workout intensities using VDOT tables.
- **Canova specificity + traditional periodization.** Coaches borrow Canova's race-pace-percentage workouts and progressive long runs while maintaining a more conservative base phase.
- **80/20 intensity distribution** overlaid on various periodization models as a universal intensity management principle.
- **Hudson's non-linear blending** — all stimuli simultaneously with shifting emphasis.

### What triggers mid-cycle adjustment

A landmark 2025 study of 12 Norwegian world-class coaches (*Sports Medicine Open*, Sandbakk et al.) — responsible for **380+ international medals** — found they combine subjective observations through regular athlete dialogues with objective measurements from training diaries, standardized sessions, and technology. The critical finding: subjective feedback receives as much weight as physiological data.

Multiple coaches converge on a traffic-light decision framework. **Green** (continue as planned): mild soreness fading after warmup, normal post-session tiredness, stable mood/appetite, resting HR within 3–5 beats of baseline. **Amber** (reduce 15–25%): heavy legs persisting 3+ days, poor sleep multiple nights, lingering aches, resting HR 5–7 beats above normal, loss of motivation. **Red** (major modification): illness symptoms, injury pain, severe fatigue.

The universal coaching rule for missed workouts: **never try to make them up.** Move forward. Never compress recovery to squeeze in missed work. For illness, return as many easy days as days sick. For athletes responding better than expected, coaches progress cautiously — Greg McMillan notes that "training results better than race results" is actually an overtraining warning sign.

### Athlete profiling drives methodology selection

McMillan's runner-type classification is the most structured framework for the AI to replicate. He categorizes runners as **Speedsters** (excel at short races, need more endurance focus), **Endurance Monsters** (excel at long races, need more speed work), or **Combo Runners** (balanced). Type is determined by comparing short-race equivalents against long-race actuals using his Running Calculator. The AI should build equivalent logic.

Critical factors for methodology matching: experience level (a 20-mpw beginner needs fundamentally different training than a 70-mpw veteran), age (masters runners need block periodization with extended recovery), available training time (time-based prescriptions for busy athletes), injury history (injury-prone athletes need lower mileage + strength emphasis), and distance transition status (5K→marathon requires extended base-building).

A coach at Miles & Mountains Coaching articulated the key insight for explainability: "When I coach an athlete, subjective feedback is vital. I want to know if their kid is sick, if they slept poorly, if their legs felt heavy. **That context matters far more than whether they completed the workout or how fast they ran it.**"

### The Runna cautionary tale

Your mention of Runna's 2026 injury controversy is validated by the research. Physical therapists reported multiple cases weekly of stress fractures from algorithmically-generated plans that were "simply too aggressive." The root cause: athletes in early stages **struggle to accurately self-assess their fitness**, have pre-existing conditions they don't understand, and treat the app like an authority. This is the exact failure mode your AI must guard against — it needs conservative defaults, mandatory self-assessment validation, and explicit flags when training load is aggressive relative to the athlete's profile.

---

## 4. Safety guardrails: specific and programmable

### Mileage progression: the 10% rule and beyond

The 10% rule — never increase weekly mileage by more than 10% — traces to **Dr. Joan Ullyot (1980)** in *Running Free*. Tim Gabbett called it "at best, a guideline rather than a code." The evidence against rigid application is substantial. Buist et al. (2008) found **no difference** in injury rates between groups increasing at 10%/week vs. 50%/week among 532 novice runners. Nielsen et al. (2014, *JOSPT*, 873 runners) found 10–30% increases showed no difference vs. <10% for overall injury rates, with only >30% showing elevated risk for some injury types.

The programmable replacement rules should be context-dependent:

| Runner category | Max weekly increase | Rationale |
|---|---|---|
| Novice (<6 months running) | 10–15% | Conservative for musculoskeletal adaptation |
| Low mileage (<20 mpw) | 15–25% | 10% is meaninglessly small at low volumes |
| Moderate mileage (20–40 mpw) | 10–15% | Standard guideline range |
| High mileage (>50 mpw) | 5–10% | Absolute volume increases become large |
| Returning to previous mileage | Up to 25% short-term | "Reverse taper" approach is well-supported |

Daniels recommends an **equilibrium model**: increase ~30% every 3–4 weeks instead of 10% weekly, arriving at the same endpoint while accounting for skeletal adaptation cycles. Build in **step-back weeks** every 3–4 weeks with 20–30% volume reduction.

**The most important new finding**: A 2025 BJSM study of 5,205 runners found that **single-run spikes are more predictive of injury than weekly mileage changes**. When any single run exceeded the longest run in the past 30 days by 10–30%, injury risk rose **64%**. Spikes >70% more than doubled risk. Traditional ACWR and week-to-week changes showed little predictive value. Hard-code this: **cap any single run at no more than 30% longer than the longest run in the prior 30 days**.

### Acute:Chronic Workload Ratio (ACWR)

From Gabbett (2016, *BJSM*): ACWR = acute load (last 7 days) ÷ chronic load (28-day average). The **sweet spot is 0.8–1.3**. Ratios of 1.3–1.5 indicate elevated risk. **≥1.5 is the danger zone**. Use the Exponentially Weighted Moving Average calculation (Murray et al., 2017, *BJSM*): EWMA_today = Load_today × λ + (1 − λ) × EWMA_yesterday, where λ_acute = 2/(7+1) = 0.25 and λ_chronic = 2/(28+1) ≈ 0.069.

Important caveat: Impellizzeri et al. (2020, 2021) called for dismissing ACWR entirely due to mathematical coupling issues, and the 2025 BJSM running study confirmed weak predictive value for runners specifically. Use ACWR as a **heuristic monitoring tool, not a validated predictor** — it catches gross training load errors but shouldn't drive fine-grained decisions.

### Overtraining detection thresholds

| Metric | Warning threshold | Red flag threshold |
|---|---|---|
| Resting HR elevation | ≥5 BPM above 7-day rolling avg for 3+ days | ≥10 BPM above 30-day baseline |
| HRV trend | 7-day rolling avg drops >1 SD below 30-day baseline for >2 weeks | Sustained suppression with performance decline |
| Performance at same HR | >5% slower pace at same HR vs. 4-week average | Progressive decline over 3+ weeks |
| RPE mismatch | RPE consistently 2+ points higher than expected for given HR/pace | Combined with RHR elevation and performance decline |
| Heart rate decoupling | >5% on steady aerobic run (suggests above aerobic threshold) | >10% (suggests significant aerobic ceiling exceeded) |

### Environmental adjustment formulas

**Heat:** Use the temperature + dew point method. Below 100°F combined: no adjustment. 101–120: 0–1% slower. 121–140: 1–3% slower. 141–160: 3–6% slower. **Above 170°F combined: hard running not recommended.** Optimal marathon temperature is **44–59°F (7–15°C)**. Above 59°F, recreational runners lose approximately **4–4.5 seconds per mile per 1°C**. Heat acclimatization (10–14 days) reduces the adjustment by 25–50%.

**Altitude:** Daniels' model: ~4–5 sec/mile slower per 1,000 feet above 3,000 feet. VO₂max decreases ~6–7% per 1,000m above ~1,500m. Full acclimatization requires 4–6 weeks.

### Three-tier guardrail system

**Tier 1 — Hard stops (block the action):**
- ACWR > 2.0 → block planned high-intensity workout
- Any single run >30% longer than longest run in past 30 days → block
- Fever ≥100.4°F → block all running
- Below-the-neck illness symptoms → block running
- Weekly mileage increase >30% → block regardless of experience
- Back-to-back hard workouts without easy day between → block
- >30% of weekly volume at moderate-to-hard effort → block

**Tier 2 — Strong warnings (allow with override):**
- ACWR > 1.5
- Weekly mileage increase >20% for non-beginners
- RHR elevated ≥7 BPM above baseline
- Long run >35% of weekly mileage
- Heart rate decoupling >10% on steady run
- Heat index (temp + dew point) >170°F for hard effort
- No taper initiated within 10 days of marathon
- Marathon attempted with peak training <30 mpw

**Tier 3 — Advisory alerts (inform, don't restrict):**
- ACWR 1.3–1.5
- RHR elevated ≥5 BPM for 3+ days
- 4+ consecutive weeks without step-back
- Altitude >3,000 feet (display pace adjustment)
- Temperature >59°F (display heat adjustment)

---

## 5. Where an LLM will get this right vs. wrong

### Likely strengths

An LLM trained on running literature will handle **conceptual periodization** well. The structure of base → build → peak → taper is extensively documented and the LLM will understand phase purposes, sequencing, and general principles. It will also handle **workout description and explanation** capably — describing *why* a tempo run improves lactate clearance or *why* easy runs build mitochondrial density. This aligns perfectly with your explainability requirement.

**Qualitative coaching advice** — how to handle a bad workout, when to take a rest day based on reported symptoms, how to mentally prepare for a race — is well-represented in training data. LLMs will sound like competent coaches for conversational guidance.

### Likely failure modes requiring explicit guardrails or knowledge injection

**Pace calculation precision.** An LLM will understand that VDOT maps race results to training paces but will not reliably perform the lookup. VDOT tables involve specific non-linear relationships (the Daniels-Gilbert oxygen cost equations) that require exact computation. **Hard-code the VDOT calculator as a deterministic function** — never let the LLM estimate training paces from natural language alone. Same for MAF HR calculation, heat/altitude adjustments, and ACWR computation. These must be code, not inference.

**Mileage progression math.** The LLM understands "increase gradually" but will not reliably calculate whether jumping from 32 to 38 mpw is a 19% increase, whether that's safe for this specific user given their history, or whether the single-run spike rule is violated. All load-management arithmetic should be **computed programmatically** and fed to the LLM as context.

**Distinguishing between methodology-specific definitions.** "Tempo run" means different things to Daniels (T-pace, ~1-hour race effort), Pfitzinger (~15K–HM pace), and Hansons (goal marathon pace). An LLM blending training corpus from all sources will conflate these. The AI needs a **methodology context variable** that resolves ambiguous terms to the correct definition for the active methodology. Without this, the LLM might prescribe a "tempo run" at Daniels' T-pace when using a Hansons-style plan, resulting in sessions that are too hard.

**Overconfident prescription for edge cases.** LLMs generate fluent, authoritative-sounding text even when uncertain. A runner returning from a stress fracture, a pregnant runner in their third trimester, or a 65-year-old with cardiac history all require nuance the LLM may not flag as uncertain. **Hard-code mandatory uncertainty declarations** for: return-from-injury protocols, pregnancy, cardiac conditions, runners over 60, runners under 16, and any medical condition mentioned in conversation.

**Anchoring to training data distributions.** LLMs are trained predominantly on content about competitive runners. The modal runner discussed in books and forums is a 30–45-year-old doing 40–60 mpw targeting a 3:00–4:00 marathon. The LLM will likely **under-calibrate for true beginners** (prescribing too much) and **over-calibrate for elites** (not being ambitious enough). Your AI needs explicit profile-based adjustment: for a runner at 15 mpw, the LLM's intuitions about "normal" training volume are wrong.

**Temporal reasoning about cumulative load.** LLMs process text, not time series. They won't naturally track that a runner had three consecutive hard weeks without a step-back, or that their ACWR has been creeping upward. All longitudinal monitoring must be **computed externally and injected into the prompt** as structured data.

**Hallucinated specificity.** The LLM might generate plausible-sounding but fabricated workout parameters: "Run 6 × 1000m at 4:15/km with 2-minute rest" — a reasonable-looking workout that may be completely wrong for a given athlete's VDOT. Training plans should be **generated through structured logic** (phase → workout type → intensity lookup → volume caps check → output), with the LLM responsible for *explanation and adaptation*, not raw plan generation.

---

## 6. The configurable framework

Based on everything above, here is the proposed three-layer architecture for the AI's coaching knowledge.

### Layer 1: Universal guardrails (hard-coded, never overridden)

These are implemented as **deterministic code** — the LLM cannot violate them regardless of conversation context.

**Load management rules:**
- Weekly mileage increase ≤30% absolute cap (context-dependent target: 10–25%)
- Single-run spike ≤30% above longest run in past 30 days
- ACWR maintained between 0.8–2.0 (block workouts pushing above 2.0)
- Step-back week enforced if 4+ consecutive building weeks without one
- ≥70% of weekly volume must be at easy/aerobic effort

**Intensity distribution rules:**
- Threshold work ≤10% of weekly mileage per session
- VO₂max work ≤8% of weekly mileage per session
- Total moderate-to-hard effort ≤30% of weekly volume
- Maximum 3 quality sessions per week (including the long run if it contains structured intensity)
- Minimum 1 easy/rest day between any two hard sessions

**Medical/safety stops:**
- Fever → block all running
- Below-the-neck illness → block running
- Runner reports sharp, localized bone pain → block running, recommend medical evaluation
- Pregnancy → cap intensity at conversational, defer to physician
- Return from >2-week layoff → progressive re-entry protocol (50% of prior volume, build over 3–4 weeks)

**Computational functions (never LLM-generated):**
- VDOT lookup and pace zone calculation
- MAF HR calculation (180 − age ± adjustments)
- ACWR computation (EWMA method)
- Weekly mileage percentage change calculation
- Heat and altitude pace adjustments
- Taper volume reduction scheduling

### Layer 2: Methodology parameters (configurable per user profile)

These are the knobs the AI turns based on user profile, preferences, or explicit methodology selection. Each parameter has a default value, a valid range, and methodology-specific presets.

| Parameter | Range | Daniels default | Pfitzinger default | Hansons default | 80/20 default | Hudson default |
|---|---|---|---|---|---|---|
| Easy volume % | 70–100% | 80% | 78% | 72% | 80% | 75% |
| Long run max (miles) | 14–22 | 20 | 22 | 16 | 20 | 20 |
| Long run intensity | Easy only → workout-embedded | Includes M/T segments | Includes MP finish | Easy (cumulative fatigue) | Easy | Progressive finish |
| Tempo definition | MP → HM pace | ~1hr race pace (T) | ~15K–HM pace (LT) | Goal MP | Zone 3–4 (CV pace) | Multiple thresholds |
| Quality sessions/week | 2–3 | 2 (2Q plan) | 2 + long run | 3 SOS | 2–3 | 3 (varied) |
| Periodization model | Linear/Modified/Non-linear | Modified linear (4 phases) | Modified linear | Modified linear | Phase-based | Non-linear |
| Base phase intensity | Aerobic only → multi-pace | Easy + strides | Easy + strides → LT | Easy + speed intervals | 90/10 → 80/20 | All stimuli from day 1 |
| Recovery week frequency | Every 2nd–4th week | Every 4th | Every 3rd | Every 4th | Every 3rd–4th | As needed |
| Recovery week reduction | 15–40% | 20–30% | 30% | 20% | 20–30% | By feel |
| Taper length (marathon) | 10 days–3 weeks | 2–3 weeks | 3 weeks | 10–14 days | 2–3 weeks | 2–3 weeks |
| Pace calibration source | Current race / Goal pace / HR formula | Current VDOT | Race pace relationships | Goal marathon pace | LTHR test | Race pace equivalents |
| Cross-training role | None → integral | Optional | Optional | Not prescribed | Optional | Selective |
| Runs per week | 3–7 | 5–7 (flexible) | 4–7 (plan tier) | 6 (strict) | 4–6 | 6 |

**Methodology selection logic:** The AI should default to blended mode. For beginners (<6 months, <20 mpw), default to Higdon-like simplicity with 80/20 intensity principles and MAF-informed easy pacing. For intermediate runners (6+ months, 20–50 mpw, time-goal-oriented), default to Daniels-influenced pacing with Pfitzinger-style periodization. For advanced runners (50+ mpw, competitive), offer methodology selection or default to Hudson-style adaptive approach with Daniels zone precision.

**Blending rules:** When blending, the AI should follow the most common human coach pattern — Lydiard-influenced base building (long aerobic foundation phase) combined with Daniels-precise workout pacing, Seiler-validated intensity distribution (80/20), and Hudson-style daily adaptability. Explain to the user which element comes from where and why.

### Layer 3: Real-time coaching judgment (LLM-driven, context-dependent)

This is where the AI's conversational intelligence creates the differentiated coaching relationship. The LLM handles:

**Workout explanation and "why."** When prescribing a tempo run, the AI explains: "Today's 4-mile tempo at 8:15/mile targets your lactate threshold — the pace where your body clears lactate as fast as it produces it. Your recent 10K at 39:50 gives you a VDOT of 42, which sets your T-pace at 8:15. This pace should feel comfortably hard — you can speak in short phrases but not hold a conversation." This is the explainability that differentiates from Runna.

**Daily adaptation based on athlete feedback.** The AI asks about sleep, stress, energy, soreness — and adjusts. "You mentioned sleeping 5 hours and feeling heavy legs. Your RHR is also up 6 beats. I'm converting today's planned tempo to an easy 40-minute run. We'll try the tempo Thursday instead. The fitness gain from a tempo run when you're under-recovered is negative — the stress exceeds the adaptation signal." This mirrors the green/amber/red framework from the Norwegian coaching study.

**Methodology rationale for the non-expert.** When a user asks why their long run is capped at 16 miles while their friend runs 20, the AI explains the Hansons cumulative fatigue philosophy vs. the Pfitzinger approach, names the tradeoffs, and explains why their profile (6 days/week, 55 mpw, history of Achilles issues) makes the shorter/more-frequent approach a better fit.

**Goal recalibration conversations.** When a runner's recent 10K time suggests their marathon goal is unrealistic, the AI initiates a transparent conversation: "Your 10K time of 52:30 corresponds to a VDOT of 35, which predicts a marathon around 4:28. Your goal of 3:45 would require VDOT 44 — that's a big gap. I can build your plan for progressive improvement across two cycles rather than targeting 3:45 in 16 weeks." This prevents the Hansons failure mode of basing training on an unrealistic goal pace.

**Pattern recognition across training history.** The AI notices: "Over the past three training cycles, your performance plateaus around week 10 of 12 whenever we introduce VO₂max intervals. You seem to respond better to extended threshold work. Let's try a Pfitzinger-influenced approach this cycle with more lactate threshold volume and less interval work." This is the Build & Maintain logic from Magness — adapting not just within a cycle but across cycles.

**Uncertainty declarations.** The AI must say "I don't know" or "this requires professional evaluation" for: persistent pain patterns, cardiac symptoms during exercise, significant performance regression without training explanation, medical conditions affecting training, and nutrition/supplement recommendations beyond basic fueling guidance.

### Implementation priorities

Build in this order: First, the deterministic computation layer (VDOT calculator, load management math, safety stops) — this is your moat against the Runna failure mode. Second, the methodology parameter system with sensible defaults and blending logic. Third, the conversational coaching layer, which is where the LLM's strength lies.

The competitive differentiator is not plan generation — as you noted, that's table stakes. It's the combination of **computational safety** (the AI literally cannot prescribe a dangerous workout because the guardrails are code, not suggestions) with **transparent coaching reasoning** (every adaptation is explained in terms the runner understands, citing the training science behind it). No current app does both well. Runna failed at safety. Template plans fail at adaptation. Human coaches do both but don't scale. Your AI can.