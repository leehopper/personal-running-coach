# Feature Ideas Backlog

A living document for capturing feature ideas, rough priority signals, and notes. Not a spec — just a place to collect thinking before PRD stage.

## Priority Legend

- **MVP** — Required for first usable version (personal use / friends)
- **Near-term** — Strong candidates for fast follow after MVP
- **Future** — Interesting but not blocking anything now
- **Exploring** — Needs more thinking before even rough prioritization

---

## MVP

| Feature | Notes |
|---------|-------|
| Conversational onboarding | Structured flow that builds user profile and initial plan. Deterministic in structure, conversational in tone. |
| Three-tier plan generation | Macro, meso, micro layers generated from onboarding data. |
| Manual workout logging | User reports what they did. Chat-based, form-based, or hybrid — TBD. |
| Plan re-optimization after logging | System adjusts upcoming workouts based on what actually happened. |
| Open conversation | Ad-hoc questions and adjustments grounded in user context. |
| Calendar or list view of workouts | Primary surface for seeing what's coming up. |
| Basic proactive coaching | Missed workout handling, simple pattern detection. |
| Coach training phase | Explicit first ~2 weeks where the AI is learning the user's fitness, schedule, and preferences. Sets expectations that the plan will improve. Gives the AI a natural window to ask more questions without feeling intrusive. |

## Near-Term

| Feature | Notes |
|---------|-------|
| Apple Health / Strava / Garmin integration | Automatic workout logging from wearables and apps. |
| Sleep and HRV data integration | Feed readiness assessment with passive health data. |
| Richer proactive coaching | Fatigue modeling, taper initiation, multi-signal readiness. |
| Workout detail expansion | Warm-up/cool-down routines, dynamic stretching suggestions. |
| Progress visualization | Weekly mileage charts, pace trends, goal progress. |

## Future

| Feature | Notes |
|---------|-------|
| Multi-sport support | Cycling, swimming, strength training as cross-training modalities. |
| Nutrition and hydration guidance | Tied to training load and upcoming workout intensity. |
| Injury prediction | Based on training load patterns and biodata trends. |
| Social features | Shared challenges, group training plans. |
| Race-day strategy planning | Pacing strategy, fueling plan, mental prep. |
| Coach mode | Human coach overlays or adjusts the AI's plan. |
| Voice interface | Log workouts mid-run or post-run via voice. |
| Coach personalities | Named AI personalities with different coaching styles (e.g., encouraging vs. data-driven vs. tough love). Differentiator that reinforces the "relationship" feel. |
| AI-managed notification cadence | The coach learns when and how to reach out. Respects context ("I'm traveling"), optimizes over time based on user response patterns. Not a dumb cron job. |

## Exploring

| Idea | Thinking |
|------|----------|
| Running-only vs. multi-sport MVP | Keeping running-only simplifies everything, but some users may expect basic cross-training support from day one. |

## Monetization Ideas (Deferred)

Parking lot for monetization thinking. Not an active planning concern — revisit after POC validation and product-market fit exploration. Captured here so ideas aren't lost.

| Idea | Thinking |
|------|----------|
| Regeneration-based pricing tiers | Higher tiers allow more frequent full replans. Lower tiers get periodic regenerations with manual micro-adjustments. Maps naturally to cost structure (each full replan = API call). Specific numbers TBD. Not tied to this model — it depends on plan architecture decisions that haven't been validated yet. |
| What qualifies as a "regeneration"? | Need a clear line between a minor adjustment (cheap) and a full replan (expensive). This distinction drives the regeneration model but may not be the right framing at all. |
| BYOM as a tier | Users who bring their own API key get unlimited usage. In-house model users get a capped experience. Could eliminate cost concerns for power users. See R-005. |
| Freemium with AI depth as the lever | Free tier gets basic plan generation. Paid tiers unlock richer adaptation, proactive coaching, deeper analysis. Keeps the product accessible while monetizing the "intelligence layer." |

---

*Add new ideas below the relevant section. Move items between sections as thinking evolves.*
