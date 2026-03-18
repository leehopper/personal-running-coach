# Research Prompt: Wearable & Platform Integration Feasibility

## What I Need

A practical, implementation-focused analysis of integrating wearable fitness platforms (Garmin, Apple Health, Strava, etc.) into an AI running coach application. I need to understand the real effort, constraints, and architectural implications — not marketing overviews of what each platform offers.

## Context About the Product

I'm building an AI running coach that sits on top of wearable data as a "planning intelligence layer." The app is NOT a workout tracker — it consumes workout results from external sources to manage and adapt a training plan through natural conversation with an LLM.

Key architectural context:
- **Deterministic computation layer** handles safety guardrails, pace calculations, and load monitoring. This layer needs structured workout data (distance, duration, pace, HR, elevation, splits).
- **LLM coaching layer** uses summarized workout data for conversation and adaptation reasoning. Per-workout summaries are ~100-150 tokens each.
- **Event-sourced plan state** (Marten on PostgreSQL) — workout completions are events that trigger plan adaptation via a 5-level escalation ladder.
- **Five-layer summarization hierarchy** — raw data (Layer 0, never in LLM context) → per-workout summary → weekly → phase → trend narrative.

From competitive research (R-002): **Strava's API explicitly prohibits using its data for AI/ML purposes.** Garmin dropped its $5K developer fee and provides free API access with full .FIT data. This makes Garmin the primary integration target.

From legal research (R-003): The app qualifies as a "vendor of personal health records" under FTC's Health Breach Notification Rule once it consumes health data from wearables. Data handling must be deliberate.

## Specific Questions

### 1. Garmin Connect API (Primary Target)
- What is the actual developer onboarding process? How long does approval take? What are the requirements (registered business, privacy policy, etc.)? Can an individual developer / side project get approved?
- What APIs are available? Specifically: Push API (webhook notifications), Pull API (polling), Health API, Activity API. What's the difference and which is appropriate for this use case?
- What data fields come through for a running activity? I need: distance, duration, pace (overall + splits), heart rate (avg, max, zones), elevation, cadence, ground contact time, training effect, VO2max estimate, body battery, sleep data, HRV status, and any other fields relevant to coaching.
- What does a .FIT file contain vs. what the API summary provides? Is parsing .FIT files necessary, or does the API provide sufficient structured data?
- Rate limits, quotas, data freshness — how quickly does data become available after a workout syncs?
- What are the actual terms of service constraints? Can the data be used for AI/ML? Can it be stored? For how long? Are there usage restrictions beyond what's publicly documented?
- OAuth flow — what does the user authorization experience look like? Scope of permissions requested?
- Webhook reliability — do push notifications work consistently, or do you need polling as fallback?
- What Garmin devices are most common among serious recreational runners? Does data availability vary by device tier (e.g., Forerunner 265 vs. Forerunner 55)?

### 2. Apple Health / HealthKit
- What data is available for running workouts? How does it compare to Garmin's data richness?
- Is server-side access possible, or is HealthKit iOS-only? What does this mean for a web-first product (the current plan)?
- Can a web app access Apple Health data through any pathway (Apple Health export, third-party bridges, etc.)?
- If native iOS is eventually needed: what's the HealthKit integration effort? What permissions are required? App Store review implications?
- How does Apple Health handle data from third-party watches (Garmin, COROS, etc.)? Is it a viable aggregation point?

### 3. Strava API
- Confirm and detail the AI/ML prohibition. What exactly does the API agreement say? Has this been enforced?
- Is there any legitimate pathway to use Strava data for an AI coaching product? (Webhooks for activity notifications without storing raw data? User-initiated export?)
- What data is available if the prohibition were not an issue? How does it compare to Garmin direct?
- What's the risk of building on Strava given the prohibition — could they retroactively revoke access?
- Given Strava acquired Runna: is the prohibition likely to tighten or loosen?

### 4. Other Platforms
- **COROS**: API availability, data richness, developer program status. Growing market share among serious runners.
- **Polar**: API availability and data access. Popular in European running community.
- **Suunto**: API status. Smaller but present in trail/ultra running.
- **Google Fit / Health Connect**: Data availability, API access, relevance for Android users.
- **WHOOP**: API availability (they're notoriously closed). Recovery/strain data would be valuable for coaching.
- **Oura**: API for sleep/readiness data. Potential complement to running watch data.

### 5. Architecture Recommendations
- What's the minimum viable integration for MVP-0 (just me)? Manual CSV/FIT upload? Garmin webhook?
- What's the recommended integration architecture for MVP-1 (friends/testers)?
- How should workout data flow from wearable → app → computation layer → LLM summarization? What's the data pipeline?
- How to handle sync failures, duplicate activities, partial data, and stale data?
- Should the app store raw .FIT/activity data, or only the extracted structured fields it needs? Storage vs. privacy tradeoff.
- How to handle users with multiple devices (e.g., Garmin watch + Oura ring + Apple Watch)?
- What's the data deconfliction strategy when the same workout appears from multiple sources?

### 6. Practical Effort Estimates
- For each platform: realistic effort to go from zero to "workout data flows into the app" — not just API integration time but including developer program approval, OAuth implementation, data parsing, error handling, and testing.
- What are the ongoing maintenance concerns? API version changes, deprecation timelines, breaking changes history.

## What I DON'T Need
- Marketing comparisons of wearable devices
- Consumer buying guides
- Generic "wearables are important" arguments
- Feature comparisons between Garmin watches (I know the ecosystem)

## Output Format

Structure the findings as:
1. **Garmin deep dive** — full integration assessment (this is the primary target, give it the most depth)
2. **Platform comparison matrix** — data availability, API maturity, restrictions, effort for each platform
3. **Architecture recommendation** — data pipeline design, storage strategy, multi-source handling
4. **Integration roadmap** — what to build at each product stage (MVP-0 → MVP-1 → public → scale)
5. **Risks and gotchas** — things that look easy but aren't, restrictions that aren't obvious, maintenance traps
