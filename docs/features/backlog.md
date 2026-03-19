# Feature Ideas Backlog

A living document for capturing feature ideas, rough priority signals, and notes. Not a spec — just a place to collect thinking before PRD stage.

## Priority Legend

- **MVP** — Required for first usable version (personal use / friends)
- **Near-term** — Strong candidates for fast follow after MVP
- **Pre-Public Release** — Required before anyone beyond founder/friends uses the product (legal, safety, compliance)
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

## Pre-Public Release

Required before the product is available to anyone beyond the founder and trusted friends. Not needed for MVP (personal use / friends circle where risk is accepted).

| Feature | Notes |
|---------|-------|
| Health screening gate (extended) | PAR-Q-inspired intake screening expanded with: pregnancy/postpartum status, date of birth with age verification (COPPA for <13), chronic condition prompts with beta-blocker detection, mental health baseline, injury history. Periodic check-ins: quarterly menstrual regularity, energy levels, stress fracture history. See DEC-018, DEC-029. |
| Medical scope keyword triggers (expanded) | Original 4 categories (DEC-019) expanded to 7: pregnancy/postpartum, female athlete health, youth indicators, chronic conditions, injury-specific, mental health/crisis. Crisis response protocol for suicidal ideation triggers. See DEC-030. |
| Population-adjusted safety guardrails | Deterministic layer enforces per-population limits: pregnancy (ACWR 0.8–1.3, RPE ceiling), postpartum (12-week block), youth (volume ceilings by age), masters (extended recovery spacing), injury return (five-stage framework), chronic conditions (beta-blocker HR→RPE switch). See DEC-028. |
| Beta participation agreement | Clickwrap agreement: "as is" disclaimer, liability cap, assumption of risk, health disclaimer. See DEC-017. |
| LLC formation | Form LLC + separate bank account before any public exposure. ~$500. |
| Privacy policy | Data collection, storage, breach notification procedures. Required for FTC HBNR compliance. See DEC-020. |
| Full Terms of Service | Mandatory arbitration + class action waiver, AI disclosure, health disclaimer, liability cap. See DEC-017. |

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
| Exercise addiction detection | R-011 validated: Exercise Addiction Inventory (Terry, Szabo & Griffiths, 2004) identifies 6 components (salience, conflict, mood modification, tolerance, withdrawal, relapse). Detect: running through injury, distress at missed runs, ever-increasing volume, adding rest-day runs, exercise-eating linkage. Response: refuse to program more volume, reframe rest. Folded into coaching persona and expanded keyword triggers (DEC-030). Pre-Public Release implementation. |
| RED-S screening and pattern detection | R-011 validated with concrete thresholds: 3+ missed periods → referral, 2+ career stress fractures in female runner → RED-S screening referral, rapid unexplained weight loss + declining performance + frequent illness. Folded into extended screening (DEC-029) as quarterly check-ins. Pre-Public Release implementation. |
| Return-to-run coaching for common injuries | R-011 produced injury-specific coaching modifications for 7 injuries (plantar fasciitis, IT band, stress fractures, Achilles tendinopathy, shin splints, patellofemoral, hamstring). Universal five-stage framework. Femoral neck = emergency referral. Traffic-light pain monitoring. Pre-Public Release implementation. |

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
