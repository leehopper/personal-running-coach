# Self-Optimization Model

Self-optimization is the core value proposition. The system continuously improves the plan based on real-world feedback. This operates at multiple levels of sophistication.

## Five-Level Escalation Ladder (from R-004)

The central risk in adaptation is cascading over-correction: one bad workout triggers a change, which disrupts the next workout, which triggers another change. The solution is a graduated response system with dampening.

**Level 0 — Absorb.** No plan change. Log the deviation, update metrics. One missed easy run, one workout 3% slower than target — this is noise. No LLM invocation needed.

**Level 1 — Micro-adjust.** Modify the next 1-2 workouts. Triggered by 2-3 data points showing minor deviation, or a single missed key workout. Deterministic layer handles this (e.g., swap workout days). No LLM reasoning needed for the swap itself.

**Level 2 — Week restructure.** Rearrange the current week. Triggered when ACWR exits the 0.8-1.3 sweet spot, 3+ consecutive missed days, or illness onset. First level requiring LLM coaching judgment; deterministic layer validates proposed changes against load constraints.

**Level 3 — Phase reconsideration.** Adjust mesocycle goals and targets. Triggered by 2+ weeks of sustained deviation, injury diagnosis, or major life changes. LLM re-evaluates periodization; computation layer recalculates progression.

**Level 4 — Plan overhaul.** Fundamental plan regeneration. Triggered by new race goals, major injury, or extended illness (>2 weeks). Requires user confirmation before executing.

### Specific Scenario Routing

**Missed workouts:** Single missed easy run → absorb. Single missed key workout → micro-adjust (reschedule if possible, otherwise absorb — never stack two key workouts on consecutive days). 2 missed key workouts same type in a week → week restructure. 3+ consecutive missed days → phase reconsideration.

**Under-performance:** <5% off pace → normal daily variation, absorb. 5-15% off → note it, check confounders (heat, altitude, sleep). After 2-3 consecutive sessions at this level, lower intensity and re-evaluate targets. >15% off a single workout → possible illness/fatigue, insert extra rest day, check ACWR.

**Over-performance:** On easy days, running faster than prescribed is the #1 amateur mistake — warrants a coaching conversation, NOT a plan upgrade. On key workouts, consistent over-performance (3+ sessions trending up) may indicate fitness breakthrough — increase targets conservatively, 2-3% at a time.

**Illness:** 1-2 days no fever → resume normally after 24hr symptom-free. 3-7 days mild → resume at 50-60% volume for 2-3 days. Any fever → wait 48-72hr. 1-2 weeks → restructure mesocycle with 30-50% volume reduction in week one back. >2 weeks → plan overhaul.

### PID-Inspired Dampening

Proportional term: size response to current error magnitude. Integral term (EWMA): capture accumulated drift over time — three weeks of gradually declining performance indicates a systematic issue even if no single workout was alarming. Derivative term: consider whether the situation is getting worse, stable, or improving. Without derivative dampening, the system oscillates between over-correction and under-correction.

### Hysteresis Prevents Flip-Flopping

Enter "needs adjustment" when ACWR drops below 0.7; don't return to "on track" until it recovers above 0.85. Different thresholds for entering vs. exiting a state creates a stabilizing dead zone — drawn from financial portfolio rebalancing drift bands.

### Signal Discrimination

Use EWMA (Exponential Weighted Moving Average) over simple rolling averages for trend detection. Only when an EWMA crosses a threshold — not when a single data point does — should plan changes trigger.

## Pattern Recognition

Over time, the system detects trends:

- Consistently failing to hit tempo paces → targets may be too aggressive
- Crushing every long run → user may be ready to progress faster
- Repeated skipping of midweek sessions → schedule constraint the plan should accommodate

This is where the AI's reasoning becomes genuinely valuable. Key insight from TrainerRoad (trained on millions of workouts): adapt the **difficulty**, not the **structure**, for routine adjustments. The plan shape (what type of workout on which day) stays constant; the prescription adapts.

## Holistic State Assessment

When integrated with health data (sleep, HRV, resting heart rate) or when the user reports how they feel, the system builds a picture of overall readiness.

A user who slept poorly for three nights and reports feeling tired should not be pushed through a hard interval session, even if the plan says so. The system should adapt to the human, not the other way around.

## Goal Recalibration

If accumulated data shows that the original goal (e.g., sub-2-hour half marathon) is unrealistic or too conservative given actual performance, the system should surface this insight and suggest a revised target.

This is a macro-level adjustment triggered by micro-level data over time.

## Cross-Domain Insights (from R-004)

**Adaptive learning (FSRS/Duolingo):** Fitness gains have "stability" values — how long they persist without stimulus. VO2max: 14-21 days. Lactate threshold: ~14 days. Speed: 7-10 days. Aerobic base: 30+ days. Periodic fitness assessments (time trials, benchmark workouts) re-assess the entire fitness state rather than trusting the model blindly.

**Game AI (Left 4 Dead Director):** Adjust PACING, not DIFFICULTY. Modulate training frequency and spacing (rest day placement, session distribution) rather than constantly tweaking workout intensity. Key race-prep workouts are "protected" from auto-modification, like boss encounters exempt from pacing modulation.

**Financial rebalancing:** Drift bands (±15% of weekly mileage target) create explicit tolerance zones where deviation is absorbed because the cost of changing exceeds the benefit. Use upcoming easy sessions to gradually correct allocation rather than restructuring the entire week. Monte Carlo goal projection → race readiness probability simulation.

## Key Architecture Insight (from R-001)

The self-optimization model has a critical architectural implication: **the LLM handles reasoning and explanation, NOT raw computation.** All load management, pace calculation, and safety checking must be deterministic code. The AI's job is:

1. Interpreting user feedback (subjective input, life context)
2. Explaining adjustments in plain language ("here's why I'm changing your plan")
3. Making judgment calls within computed safe boundaries
4. Pattern recognition across training history
5. Goal recalibration conversations

The computation layer handles: pace-zone index/pace lookups, ACWR calculations, mileage progression math, single-run spike checks, safety guardrail enforcement. These are never LLM-generated — the model literally cannot prescribe something the code layer blocks.

## Daily Adaptation Framework (from R-001)

Research on Norwegian world-class coaches found they use a traffic-light system for daily decisions:

- **Green (continue as planned):** Mild soreness fading after warmup, normal tiredness, stable mood, RHR within 3-5 beats of baseline
- **Amber (reduce 15-25%):** Heavy legs 3+ days, poor sleep multiple nights, lingering aches, RHR 5-7 above normal, loss of motivation
- **Red (major modification):** Illness symptoms, injury pain, severe fatigue, RHR 10+ above baseline

Universal coaching rule for missed workouts: **never try to make them up.** Move forward. Never compress recovery to squeeze in missed work.
