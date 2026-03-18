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
