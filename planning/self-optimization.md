# Self-Optimization Model

Self-optimization is the core value proposition. The system continuously improves the plan based on real-world feedback. This operates at multiple levels of sophistication.

## Reactive Adjustments

Immediate responses to deviations. If the user ran 2 miles instead of the prescribed 5, the system decides how to handle the deficit: absorb it, redistribute across upcoming days, or flag a larger pattern.

Single missed workouts are generally noise; the system should avoid overreacting.

## Pattern Recognition

Over time, the system detects trends:

- Consistently failing to hit tempo paces → targets may be too aggressive
- Crushing every long run → user may be ready to progress faster
- Repeated skipping of midweek sessions → schedule constraint the plan should accommodate

This is where the AI's reasoning becomes genuinely valuable.

## Holistic State Assessment

When integrated with health data (sleep, HRV, resting heart rate) or when the user reports how they feel, the system builds a picture of overall readiness.

A user who slept poorly for three nights and reports feeling tired should not be pushed through a hard interval session, even if the plan says so. The system should adapt to the human, not the other way around.

## Goal Recalibration

If accumulated data shows that the original goal (e.g., sub-2-hour half marathon) is unrealistic or too conservative given actual performance, the system should surface this insight and suggest a revised target.

This is a macro-level adjustment triggered by micro-level data over time.

## Key Architecture Insight (from R-001)

The self-optimization model has a critical architectural implication: **the LLM handles reasoning and explanation, NOT raw computation.** All load management, pace calculation, and safety checking must be deterministic code. The AI's job is:

1. Interpreting user feedback (subjective input, life context)
2. Explaining adjustments in plain language ("here's why I'm changing your plan")
3. Making judgment calls within computed safe boundaries
4. Pattern recognition across training history
5. Goal recalibration conversations

The computation layer handles: VDOT/pace lookups, ACWR calculations, mileage progression math, single-run spike checks, safety guardrail enforcement. These are never LLM-generated — the model literally cannot prescribe something the code layer blocks.

## Daily Adaptation Framework (from R-001)

Research on Norwegian world-class coaches found they use a traffic-light system for daily decisions:

- **Green (continue as planned):** Mild soreness fading after warmup, normal tiredness, stable mood, RHR within 3-5 beats of baseline
- **Amber (reduce 15-25%):** Heavy legs 3+ days, poor sleep multiple nights, lingering aches, RHR 5-7 above normal, loss of motivation
- **Red (major modification):** Illness symptoms, injury pain, severe fatigue, RHR 10+ above baseline

Universal coaching rule for missed workouts: **never try to make them up.** Move forward. Never compress recovery to squeeze in missed work.
