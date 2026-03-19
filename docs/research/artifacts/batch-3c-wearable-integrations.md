# Wearable platform integration for an AI running coach

**Garmin is the clear primary integration target: it permits AI/ML usage with user consent, provides the richest running data through both structured API summaries and raw .FIT files, and dropped its $5K developer fee in favor of free access.** Strava's API explicitly prohibits all AI/ML usage — confirmed in contract language updated November 2024 and reinforced by the April 2025 Runna acquisition. Apple HealthKit is on-device only, making it architecturally incompatible with a web-first product without a native iOS companion app. Among secondary platforms, Polar stands out with a fully public, self-service API and rich running data, while Oura and WHOOP provide valuable complementary recovery data but no running metrics.

---

## 1. Garmin Connect API: full integration assessment

### Developer onboarding is business-oriented but achievable

Apply at garmin.com/en-US/forms/GarminConnectDeveloperAccess/. The program is explicitly described as "for business use" and "free to approved business developers." Garmin confirms application status **within 2 business days**, and typical integration takes **1–4 weeks** from approval to data flowing.

Requirements include an app description, company background, website, and privacy policy URL. The developer agreement defines the Licensee as a "corporation, governmental organization, or other legal entity" — an individual without a business entity faces uncertain approval odds. Multiple developer forum posts confirm this friction point. Practical workaround: register an LLC, or use a middleware service like **Terra API** or **Thryve** that includes Garmin access without requiring your own developer program approval.

After initial approval, you receive an evaluation API key (rate-limited). Promotion to production requires: at least 3 endpoints enabled, data from 2+ Garmin users in the last 24 hours, zero errors in the last 24 hours, and manual review by Garmin Developer Support. This means you need real test users before going live.

### Five APIs, but Activity and Health are what matter

The developer program provides five APIs, of which three are relevant to an AI running coach:

**Activity API** delivers structured running activity data — summaries with laps, samples, and full .FIT/.GPX/.TCX files. This is the primary data source for completed workouts. **Health API** provides daily wellness context — sleep, stress, HRV status, Body Battery, resting HR, and critically, the User Metrics Summary containing **VO2max, Training Status, Training Readiness, and HRV status** (these are Garmin-computed per-user metrics not available in the Activity API). **Training API** enables pushing structured workouts back to a user's Garmin device — valuable for the coaching use case where the LLM generates a workout plan.

Both Activity and Health APIs support **push** (Garmin POSTs full payloads to your webhook) and **ping/pull** (Garmin sends a notification with a callback URL you then fetch from). Garmin strongly prefers push-based architecture — developer reports indicate pull-based polling is actively discouraged and may cause approval issues.

### Activity summary data covers the basics; .FIT files fill critical gaps

The Activity Summary push payload provides these fields directly: `DistanceInMeters`, `DurationInSeconds`, `AveragePaceInMinutesPerKilometer`, `MaxPaceInMinutesPerKilometer`, `AverageHeartRateInBeatsPerMinute`, `MaxHeartRateInBeatsPerMinute`, `AverageRunCadenceInStepsPerMinute`, `TotalElevationGainInMeters`, `TotalElevationLossInMeters`, `ActiveKilocalories`, `DeviceName`, plus a **Samples array** with per-second time-series data (HR, speed, cadence, lat/long, elevation, power, temperature) and a **Laps array** with timestamps.

However, the summary API has notable gaps for coaching-quality data. **Training Effect** (aerobic/anaerobic) lives only in the .FIT file session message. **Ground contact time, vertical oscillation, and stride length** are only in .FIT record messages. **HR zones breakdown** must be calculated from samples. **Running power** is stored in .FIT developer fields. VO2max, HRV, Body Battery, sleep, and Training Readiness all come from the Health API, not the Activity API.

The practical split for the app's two-layer architecture: use the **Activity Summary JSON** for the LLM coaching layer's ~100-150 token summaries (distance, duration, avg pace, avg HR, elevation — sufficient for conversational coaching), and **parse .FIT files** for the deterministic computation layer's detailed analysis (per-lap pace/HR, running dynamics, Training Effect, precise split calculations).

### .FIT files are essential and manageable

A running .FIT file contains session messages (overall totals including Training Effect), lap messages (per-lap summaries at auto-lap or manual split points), record messages (per-second time-series with HR, speed, cadence, position, elevation, and with compatible devices: vertical oscillation, ground contact time, stride length, running power), and device/event metadata.

A critical subtlety: Garmin's default **Smart Recording** mode records only when direction/speed changes, creating variable-interval data points. This makes cross-referencing laps with samples imprecise — "it is NOT guaranteed that a sample is recorded at the start of a new lap," as the Coopah development team documented. Users can switch to Every-Second recording for better accuracy but most don't. File sizes for a 1-hour run: **50-200 KB** (Smart Recording) or **200-500 KB** (Every-Second).

For parsing, the best Python options are **`garmin-fit-sdk`** (official Garmin SDK: `pip install garmin-fit-sdk`) and **`fitdecode`** (community, thread-safe, ~0.9s per 1-hour file: `pip install fitdecode`). For Node.js, `fit-file-parser` on npm is the standard choice. The official SDK supports Python, C, C++, C#, Java, Dart, and Swift — no official Node.js SDK exists.

### Terms of service: AI usage permitted with transparency requirements

This is the most strategically important finding. **Garmin does not prohibit AI/ML usage.** Section 15.10 of the developer agreement requires an "AI Transparency Statement" in your privacy policy if end user data will be used for AI processing or training. The statement must clearly inform users of the nature and purpose of AI processing, obtain **explicit consent** before initiating AI processing, allow easy consent withdrawal, and stay current with regulations.

Data use restrictions require use "(i) solely for internal business purposes" and "(ii) only to the extent necessary to format and display such data." You cannot sell user data, use API data to compete with Garmin, or sublicense data to unapproved third parties. Data retention must be "the minimum length of time needed to fulfill the purpose of processing." Garmin branding attribution is required wherever Garmin-sourced data is displayed.

Garmin's own data retention in the API is approximately **7 days** — you must store data yourself using the push/webhook mechanism. A Backfill Service exists for historical data retrieval.

### OAuth is in transition; webhooks work but need fallbacks

The developer FAQ now references **OAuth 2.0**, but practitioner implementations from 2022-2026 consistently describe **OAuth 1.0a** flows. A forum post mentions "OAuth2 PKCE" for newer integrations. The implementation likely depends on when your integration was established — newer integrations may use OAuth 2.0 while existing ones remain on 1.0a. Budget for OAuth 1.0a initially and confirm with Garmin during your integration call.

Webhook reliability is generally good for normal push notifications but has documented issues: backfill notifications sometimes fail to arrive, **third-party activities synced to Garmin Connect from other apps are NOT forwarded** to API partners (a Garmin team member confirmed this), and webhooks require public endpoints with no Authorization header (mitigate by whitelisting Garmin IP addresses). Garmin may send partial updates throughout the day as devices sync incrementally. A polling fallback is advisable, and Garmin provides a Summary Resender tool for re-triggering missed notifications.

### Device tier determines data richness significantly

For serious recreational runners, the **Forerunner 265** is the baseline target device (~$450, most recommended mid-tier). It provides: Training Readiness, HRV Status, Training Effect, wrist-based running dynamics (ground contact time, vertical oscillation, stride length), running power, barometric elevation, and Elevate V4 HR sensor. The **FR 965** adds maps, load ratio, and stamina data. The **Fenix 7 Pro** and above add solar charging and durability for the ultrarunning segment.

Key data availability gaps by tier: the **FR 55** lacks HRV, sleep score, Training Effect, running dynamics, and barometric altimeter. The **FR 165** adds most wellness metrics but lacks Training Status and Training Readiness. Design the computation layer to **gracefully degrade** when metrics like running dynamics or Training Readiness are missing — check for null fields rather than assuming their presence.

---

## 2. Platform comparison matrix

| Platform | API access | Auth | Running data depth | Recovery/wellness | AI/ML permitted | Webhooks | Server-side | Effort to integrate |
|---|---|---|---|---|---|---|---|---|
| **Garmin** | Free, business approval | OAuth 1.0a/2.0 | ★★★★★ Full metrics + .FIT | ★★★★★ Sleep, HRV, Body Battery, Training Readiness | **Yes**, with consent + transparency | Yes (push) | Yes | 2-4 weeks |
| **Strava** | Free, self-service | OAuth 2.0 | ★★★★ Good via streams | ★☆☆☆☆ Activity-only | **No** — explicitly prohibited | Yes | Yes | 1-2 weeks (but unusable for AI) |
| **Apple HealthKit** | Requires iOS app | iOS SDK | ★★★★ Rich from Apple Watch | ★★★★ Sleep, HRV | Yes, with disclosure | Background delivery | **No** — on-device only | 4-8 weeks (iOS app required) |
| **Polar** | Free, self-service | OAuth 2.0 | ★★★★★ Full metrics + FIT export | ★★★★ Sleep, Nightly Recharge, alertness | No documented prohibition | Yes | Yes | 1-2 weeks |
| **COROS** | Partner approval, private docs | OAuth 2.0 | ★★★★★ EvoLab metrics | ★★★ Basic | Unknown (private terms) | Unknown | Likely yes | 3-6 weeks |
| **Suunto** | Partner approval | OAuth 2.0 | ★★★★ FIT delivery, trail focus | ★★★ Sleep, recovery, stress | No documented prohibition | Yes | Yes | 2-4 weeks |
| **Google Health Connect** | Public, Android only | Android SDK | ★★★★ Aggregated | ★★★ Sleep, HR | Subject to Play policies | No | **No** — on-device only | 3-6 weeks (Android app required) |
| **WHOOP** | Self-service (membership req'd) | OAuth 2.0 | ★☆☆☆☆ No GPS/pace/distance | ★★★★★ Strain, recovery, HRV | No documented prohibition | Yes (v2) | Yes | 1-2 weeks |
| **Oura** | Self-service | OAuth 2.0 | ★☆☆☆☆ No GPS/pace/distance | ★★★★★ Sleep, readiness, HRV, temp | No documented prohibition | No (polling only) | Yes | 1 week |

### Platform-specific notes worth highlighting

**Strava** caches data for a maximum of **7 days** (Section 7.1), prohibits analytics/analyses even on aggregated data (Section 2.14.7), and prevents showing a user's data to their coach (classified as "another user" under Section 2.10). User consent cannot override restrictions (Section 2.9). With Strava's April 2025 Runna acquisition, these restrictions will almost certainly tighten — Strava now directly competes with AI running coaches.

**Apple HealthKit** is architecturally incompatible with a web-first product. Data stays on-device; the only pathway to your server requires a native iOS companion app reading HealthKit and POSTing to your API. Even third-party bridge services (Terra, Junction) still require their mobile SDK in your iOS app. Garmin → Apple Health sync loses most valuable data: no GPS tracks, no full HR stream (only high/low), no running dynamics, no Training Effect, no VO2max, no pace splits. **Apple Health is NOT a viable aggregation layer for Garmin users.**

**Polar** is the sleeper pick: fully public self-service API with OAuth 2.0, webhook support, rich running data including exercise samples (HR, pace, cadence, altitude, power), FIT/TCX/GPX exports, training load, sleep stages, and Nightly Recharge recovery metrics. No approval process, no documented AI restrictions, and an official Python SDK exists. Popular among European runners.

**COROS** has a growing runner user base (PACE 3, PACE Pro) but API documentation is private — shared only after partner approval. Reverse-engineered libraries exist on GitHub (`xballoy/coros-api`, `jmn8718/coros-connect`) but could break anytime. For reliable COROS data access today, users can export individual .FIT files from the COROS app, or data flows through third-party sync to Strava/TrainingPeaks.

**WHOOP** and **Oura** provide no running-specific metrics (no GPS, pace, distance, splits, elevation) but offer high-value complementary data. WHOOP's strain/recovery scores and Oura's sleep/readiness scores can feed the LLM coaching layer for recovery-aware plan adjustments. Both require active subscriptions for API data access ($30/month for WHOOP; Oura Membership for Gen3+).

---

## 3. Architecture recommendation

### Data pipeline design

The recommended pipeline has four stages, designed around the app's existing two-layer architecture:

**Stage 1 — Ingress** (stateless, fast): Receive Garmin webhook POST → verify sender (IP whitelist since Garmin prohibits auth headers) → store raw payload in a `raw_webhooks` table → return 200 immediately. Zero business logic here. For Strava (if used as notification trigger only): respond within 2 seconds or lose the event after 3 retries.

**Stage 2 — Process** (async worker): Dequeue raw webhook → check idempotency (`processed_webhooks` table with unique constraint on `{provider, event_id}`) → for Activity events: extract structured fields from the Activity Summary JSON AND request/parse the associated .FIT file → for Health events: extract sleep, HRV, Training Readiness, VO2max → write structured data to `canonical_activities` and `daily_wellness` tables.

**Stage 3 — Compute** (deterministic layer): Read structured activity data → calculate derived metrics (pace zones, HR zone time, weekly volume trends, acute:chronic workload ratio, race-readiness indicators) → update five-layer summarization hierarchy → write computed results to event-sourced plan state.

**Stage 4 — Summarize** (LLM layer): Generate ~100-150 token workout summary from structured fields → incorporate daily wellness context (sleep quality, HRV trend, Training Readiness) → feed to LLM as context for coaching conversation.

### Storage strategy: minimize aggressively

Given FTC Health Breach Notification Rule applies, every stored field is a breach liability. The recommended tiered approach:

- **Raw webhook payloads**: PostgreSQL or S3, encrypted, **30-day retention then delete**. Useful for debugging sync issues but not operationally needed long-term.
- **Raw .FIT files**: Do not store long-term. Parse on receipt, extract structured fields, then delete the .FIT file. If users want to keep originals, they have them on Garmin Connect.
- **Structured activity data** (the fields your computation layer needs): PostgreSQL, encrypted at rest (AES-256), retained for account lifetime. Fields: distance, duration, avg/max pace, avg/max HR, elevation gain, per-lap splits (JSONB), cadence, Training Effect, running dynamics if available.
- **LLM summaries and trend narratives**: PostgreSQL, retained for account lifetime. These are the five-layer summarization hierarchy outputs.
- **Daily wellness data**: sleep score/duration, HRV status, Training Readiness, Body Battery trend — retained for account lifetime.

**Event source the plan state** (coaching decisions, plan adaptations, weekly reviews) but **do NOT event source imported workout data**. Workouts are external facts; plan adaptations are your domain events. The distinction matters architecturally: workout imports are idempotent CRUD operations, while plan events are append-only domain events.

Encrypt everything at rest with AES-256. This qualifies data as "secured" under the FTC HBNR, meaning breaches of properly encrypted data don't trigger the 60-day notification requirement to users, FTC, and (if 500+ affected in one state) media outlets. Civil penalties for HBNR violations reach **$50,120 per violation**.

### Multi-source deduplication

When the same workout arrives from multiple sources (Garmin API + Apple Health + Strava), use **time-window matching with source priority**:

Match on start time ±5 minutes + sport type + duration ±10%. When a match is found, keep the highest-priority source's data as the canonical record. Priority hierarchy for running data: **Garmin direct API > COROS/Polar direct API > Strava (better elevation correction) > Apple Health (summary only)**. For sleep data: **Oura > WHOOP > Garmin > Apple Watch**. For HR during activities when multiple sources exist: **chest strap source > any wrist device**.

For complementary data (Garmin watch for running + Oura ring for sleep), no deduplication is needed — these are different data domains. Link by user + date and let the LLM coaching layer reference both: "Your easy run yesterday showed elevated HR, which correlates with the poor sleep quality your Oura reported."

### Handling sync failures and edge cases

Implement a **fetch-before-process** pattern: treat every webhook as a notification only, always fetch the latest state from the API. This makes event ordering irrelevant and naturally handles updates. Garmin may push partial updates throughout the day as devices sync incrementally.

For missed webhooks: run a **reconciliation cron job** every 30 minutes that fetches the last 48 hours of activities and compares against stored data. Garmin provides a Backfill Service and Summary Resender tool for re-triggering notifications. For Strava: after 3 failed delivery attempts, an event is permanently lost — the polling fallback is essential.

Store processed webhook IDs in a dedicated table with unique constraints. Use composite idempotency keys: `{provider}:{user_id}:{activity_id}:{event_type}`. This prevents duplicate processing when Garmin sends updated summaries for the same activity.

---

## 4. Integration roadmap by product stage

### MVP-0: personal use only (1-2 weekends)

Use **`python-garminconnect`** or **`garth`** to poll your own Garmin account directly — no OAuth flow needed, no developer program approval needed, no webhook infrastructure needed. These are unofficial reverse-engineered libraries that authenticate with your Garmin credentials and can fetch activity summaries and .FIT files.

Parse .FIT files with `fitdecode`. Store structured data in SQLite or a single PostgreSQL instance. Trigger data fetch manually or via a cron job after workouts. Feed structured data into your LLM coaching layer.

Alternative MVP-0 path: manual .FIT file upload. Export .FIT from Garmin Connect web, upload to your app, parse and process. Zero API dependency. This is the most stable starting point for validating the coaching intelligence before investing in integration infrastructure.

Another practitioner-recommended option: use **intervals.icu** as an intermediary. Sync Garmin → intervals.icu (which many runners already use), then use intervals.icu's simpler API to pull normalized data. This adds a dependency but simplifies initial data access.

### MVP-1: friends and testers, 10-50 users (2-4 weeks)

Apply for the **Garmin Connect Developer Program** — you'll need a business entity (even a simple LLC) and a privacy policy. While awaiting approval (2 business days typical), build the OAuth flow. Garmin's OAuth is currently in transition — confirm whether you'll implement 1.0a or 2.0 PKCE during your integration call with Garmin.

Set up webhook endpoints for Activity API and Health API push notifications. Implement the four-stage pipeline described above. Build the idempotency and deduplication layer. Add Garmin IP whitelisting for webhook security.

Key architectural decision at this stage: whether to also support **Polar** as a secondary platform. Polar's self-service API with OAuth 2.0 and webhooks makes it the easiest second integration (~1 week additional effort) and captures the European runner segment. This also validates your multi-source architecture early.

At this stage, add the FTC HBNR compliance foundations: encrypt all data at rest, finalize your privacy policy with the Garmin-required AI Transparency Statement, document your breach response plan, and implement data minimization (don't store GPS tracks or raw .FIT files beyond the processing window).

### Public launch (additional 2-4 weeks)

Complete Garmin production key verification (requires 3+ endpoints active, 2+ users with data in last 24h, zero errors). Implement rate limit management with request queuing. Add monitoring for webhook delivery failures and sync status.

Consider adding **Oura** integration at this stage (~1 week effort) for sleep/readiness data — it's a self-service API with Personal Access Tokens for quick prototyping and OAuth 2.0 for production. Sleep and recovery data significantly enriches the coaching intelligence.

Implement the **Training API** to push adapted workouts back to users' Garmin devices — this closes the feedback loop and is a major differentiator. The user sees tomorrow's workout appear on their watch automatically.

For Apple Watch users without Garmin: the choice is between building a native iOS companion app (4-8 weeks of iOS development, App Store review with health data scrutiny) or recommending users install **Health Auto Export** ($5-10/year) configured to POST to your API. The iOS companion app is the right long-term choice but not essential for initial launch.

### Scale (1000+ users)

Evaluate **Open Wearables** (MIT-licensed, self-hosted, Docker-based, supports Garmin/Strava/WHOOP/Apple Health/Polar/Suunto with built-in deduplication and MCP server for AI integration) as an integration layer to reduce per-provider maintenance. Alternatively, commercial aggregators like Terra ($399/month annual) normalize multi-provider data.

Separate webhook ingestion from processing (dedicated ingestion service → message queue → processing workers). Implement dead letter queues for failed processing. Add horizontal scaling for the processing layer. PostgreSQL handles event sourcing well into millions of events; the LLM summarization cost at ~100-150 tokens per workout is negligible.

---

## 5. Risks, gotchas, and maintenance traps

### Things that look easy but aren't

**Garmin's Smart Recording creates insidious data quality issues.** Most users leave the default Smart Recording enabled, which records at variable intervals (only when speed/direction changes). This means you cannot assume regular 1-second data points. Calculating accurate per-kilometer splits requires interpolation between irregular sample timestamps — "it is NOT guaranteed that a sample is recorded at the start of a new lap." Every-Second recording produces clean data but most users won't change the default. Build your split calculation logic to handle both modes.

**OAuth 1.0a is significantly harder than OAuth 2.0.** If Garmin assigns you OAuth 1.0a (which practitioners report as recently as February 2026), expect 2-3x the implementation effort of a standard OAuth 2.0 flow. The request signing, nonce management, and three-legged flow are more complex. Use `requests-oauthlib` in Python to reduce the pain.

**Garmin webhook endpoints cannot have Authorization headers.** Garmin requires your webhook URLs to be publicly accessible with no authentication. This means anyone who discovers your endpoint URL can send fake payloads. Mitigate with Garmin IP whitelisting, but this creates a maintenance burden when Garmin's IPs change.

**Third-party activities synced to Garmin Connect don't forward to API partners.** If a user records a treadmill run on Zwift and it syncs to Garmin Connect, your webhook will NOT receive it. Only activities recorded directly on a Garmin device are forwarded. This is a confirmed Garmin policy that catches developers by surprise.

### Restrictions that aren't obvious

**Strava's prohibition extends far beyond model training.** Section 2.14.7 prohibits processing Strava data "for the purposes of analytics, analyses, customer insights generation, and products or services improvements" — this catches virtually any programmatic use of the data beyond simple display. Even using Strava data to calculate a weekly mileage trend technically violates these terms.

**Strava's 7-day cache limit** (Section 7.1) makes any longitudinal training analysis impossible. You cannot build a training history from Strava data — it must be deleted within 7 days of caching.

**Garmin data retention in the API is only ~7 days.** If your webhook goes down for a week and you miss push notifications, that data may become unrecoverable through normal API channels. The Backfill Service and Summary Resender tools help, but prompt data ingestion and storage is critical.

**FTC HBNR treats "unauthorized disclosure" as a breach.** This includes sharing health data with analytics platforms, ad networks, or even LLM API providers without explicit user consent. The GoodRx enforcement action ($1.5M fine for sharing health data with Facebook/Google) demonstrates the FTC takes this seriously. If you send workout data to an external LLM API (OpenAI, Anthropic), ensure your privacy policy explicitly discloses this and obtain clear consent. Consider whether self-hosted models would reduce compliance risk.

**Oura Ring API requires active Oura Membership**, not just ring ownership. If a user cancels their subscription, your app loses API access to their data. Similarly, WHOOP requires an active $30/month membership. These subscription dependencies create unpredictable data availability.

### Maintenance traps

**Strava has a documented pattern of progressively restricting API access.** The 2018 changes broke many apps with minimal notice. The November 2024 AI/ML prohibition gave 30 days' notice. Strava's Community Hub explicitly states "requesting or attempting to have Strava revert business decisions will not be permitted." Building any dependency on Strava for an AI product means accepting the risk of sudden, unilateral access revocation with no recourse.

**Unofficial Garmin libraries (`python-garminconnect`, `garth`) can break at any time.** These reverse-engineer Garmin's web authentication, which Garmin can change without notice. Suitable for MVP-0 personal use but not for production with real users. Budget for occasional multi-day outages when Garmin updates their auth flow.

**Expect 2-4 hours per month of maintenance per integrated platform** during stable periods, with occasional multi-day efforts when APIs introduce breaking changes. Strava averages 1-2 breaking changes per year. Garmin's official API is more stable but the OAuth transition adds near-term uncertainty. Polar's API has been relatively stable.

**Apple's App Store review process for HealthKit apps adds 1-3 weeks** to any update cycle. Reviewers may ask you to demonstrate and explain your use of health data. Health data apps get extra scrutiny under Guidelines 5.1.2 and 5.1.3 — data may not be used for advertising or data mining, and you must indicate HealthKit integration in marketing text. Plan review cycles accordingly if you build an iOS companion app.

### The recommended minimum-risk path

For a solo developer building an AI running coach: start with manual .FIT upload (zero API dependency, zero approval process, zero maintenance burden), validate the coaching intelligence works, then add Garmin webhook integration when you have users who want automated sync. Add Polar as the easiest second platform. Defer Apple HealthKit and COROS until user demand justifies the investment. Never build a core dependency on Strava data. Encrypt everything from day one.