# Research Prompt: Batch 9b — R-020
# Unit System Design for a Running Application (Metric/Imperial/Mixed)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: Designing a clean unit system for a running coach application that handles metric, imperial, and mixed-unit contexts

Context: I'm building an AI-powered adaptive running coach (RunCoach). The system currently stores everything internally in metric units:
- Distances in kilometers (`decimal DistanceKm`)
- Paces in seconds per kilometer (`TimeSpan AveragePacePerKm`, `PaceRange(MinPerKm, MaxPerKm)`)
- Race distances as strings like "5k", "10k", "half-marathon", "marathon"
- A `UserPreferences.PreferredUnits` string field (currently always "metric") that gets passed to the LLM prompt but has zero backend logic

The problem is that running is inherently mixed-unit in practice:
- **Race distances** are always metric names (5K, 10K, half-marathon) worldwide
- **Track workouts** are in meters (400m repeats, 800m intervals, 1600m reps)
- **Road running** varies by locale: miles in the US/UK, kilometers elsewhere
- **Paces** vary: min/km in metric countries, min/mile in US/UK
- **Weekly volume** follows the same split (km/week vs miles/week)
- **Garmin/Strava/Apple Health** data comes in the user's configured unit, not a standard

The AI coaching layer adds complexity: the LLM needs to speak in the user's preferred unit, but the deterministic computation layer (VDOT, pace zones, ACWR) needs consistent internal units.

What I need to learn:

### 1. Internal Storage Design
- What's the best practice for internal unit representation in a running app? Always metric? Always SI? A "canonical unit" pattern?
- How do Strava, Garmin Connect, TrainingPeaks, and similar platforms handle this internally?
- Should I store the original unit alongside the value (e.g., `Distance { Value: 5.0, Unit: Kilometers }`) or convert on ingest?
- How do you handle the fact that "5K" is a meaningful concept (not just 5.0 km) — should race distances be an enum/known-distance type rather than a raw number?

### 2. Conversion Architecture
- Where should conversions happen — at the API boundary, in the service layer, or in the presentation layer?
- Pattern options: converter utilities, unit-aware value types (like `UnitsNet` or `Quantity<T>`), or simple extension methods?
- How do you avoid the Mars Climate Orbiter problem (implicit unit assumptions causing bugs)?
- What .NET libraries exist for unit handling? Is `UnitsNet` the standard? Is it overkill for just distance/pace/speed?

### 3. Pace Representation
- Paces can be expressed as min:sec/km, min:sec/mile, km/h, mph, or m/s. What's the cleanest internal representation?
- TimeSpan per unit distance vs seconds-per-unit vs speed (distance/time)?
- How do running apps handle the fact that faster paces are smaller numbers (counterintuitive)?

### 4. LLM/AI Coach Interaction
- The AI coach speaks in the user's preferred units. Should the prompt template handle unit conversion, or should the assembled context data be pre-converted to the user's preferred units before injection?
- How do you validate that the LLM's output (structured or free-text) uses correct unit-converted values?
- If the LLM generates a plan with "run 6 miles easy at 9:30/mi pace" for a metric user, something went wrong. Where's the right place to enforce this?

### 5. User Preference Design
- What preferences should exist? Just "metric/imperial"? Or granular per-context (race distances: always metric names, daily running: imperial, etc.)?
- How do apps like Strava handle the unit preference UX?
- Should the system auto-detect based on locale, or always ask?
- How do you handle the transition if a user changes their preference mid-plan?

### 6. Data Ingestion from Wearables
- Garmin, Strava, and Apple Health APIs — what units do they return? Is it configurable?
- If a user's Garmin is set to miles but our system stores km, where and when does conversion happen?
- Precision and rounding concerns when converting between km and miles repeatedly

### 7. Recommendations for a Phased Approach
- We're building MVP-0 (solo use, metric-only initially). What's the minimum viable unit architecture that doesn't paint us into a corner?
- What should we build now vs defer to MVP-1 (friends/testers, some will be imperial users)?
- What patterns are easy to add later vs require foundational work upfront?

Output I need:
- A recommended internal unit strategy (what to store, what to convert, when to convert)
- A recommended architecture pattern for .NET (library choices, type design, where conversions live)
- How the AI coaching layer should interact with units (pre-conversion vs prompt-level)
- A phased implementation plan: what to build for MVP-0 (metric-only), what to prepare for MVP-1 (imperial support), what can wait
- Gotchas and anti-patterns to avoid (based on what other running apps got wrong)
