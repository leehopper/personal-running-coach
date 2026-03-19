# AI planning architecture for an adaptive running coach

An AI running coach built on PostgreSQL, .NET, and Claude's 200K context window should use **event-sourced plan state with Marten**, a **three-tier plan decomposition stored as lightweight constraints rather than full schedules**, and a **deterministic signal-processing pipeline with PID-inspired dampening** that routes adaptation decisions through a 5-level escalation ladder. The architecture's central insight — drawn from adaptive learning platforms, game AI directors, and portfolio rebalancing systems — is that the plan itself should be treated as a living, regenerable artifact: store constraints and templates at the macro/meso levels, generate detailed prescriptions on demand at the micro level, and overlay continuous ACWR/TSB monitoring as a cross-cutting trigger mechanism that can force replanning at any tier. This approach keeps context injection lean (under 15K tokens per call, or ~7.5% of the available window), gives the deterministic computation layer hard guardrails over the LLM's coaching judgment, and produces a full audit trail of every adaptation decision through event sourcing.

---

## Section 1: Plan state management through event-sourced structured artifacts

The fundamental architectural question — how to store a training plan that both code and an LLM continuously edit — has a clear answer from production AI systems: **use a hybrid document + event log pattern**, where the current plan state lives as a JSON document for easy LLM consumption, and every modification is recorded as an immutable event for auditability.

### The data structure pattern that works

The plan needs three properties simultaneously: structured enough for the computation layer to validate pace targets and load constraints, flexible enough for the LLM to reshape workout sequences and coaching rationale, and versioned so every change is traceable. **JSON Schema with LLM structured outputs** solves the first two. Anthropic's structured outputs (production since late 2025) use constrained decoding — compiling a JSON Schema into a grammar that restricts token generation during inference, guaranteeing schema compliance. The LLM literally cannot produce invalid plan modifications.

The pattern from GitHub Copilot Workspace is directly instructive. Copilot Workspace uses a **Spec → Plan → Implementation** pipeline where each stage is an editable, regenerable artifact. The spec captures current state and desired state; the plan describes per-file actions; the implementation produces diffs. For the running coach, the mapping is clean: the athlete profile + current fitness + goals form the spec, the training plan at macro/meso/micro levels is the plan, and today's validated workout is the implementation. Copilot Workspace auto-saves sessions, supports undo/redo, and makes everything editable at each stage — exactly the properties needed for an adaptive training plan.

Anthropic's own multi-agent research system articulates the key principle: *"Implement artifact systems where specialized agents can create outputs that persist independently. Subagents call tools to store their work in external systems, then pass lightweight references back to the coordinator."* This maps directly to the running coach: the computation layer outputs constraint-validated data to PostgreSQL, the LLM layer outputs reasoning and adaptation decisions to the same store, and both pass lightweight references (plan ID, version number) rather than full payloads.

### Decomposed state with event-sourced coordination

The plan should be decomposed rather than monolithic. The macro/meso/micro decomposition has strong analogues across fields — evolutionary economics, traffic simulation, information architecture, and robotics all use this three-layer structure. In every case, the meso level acts as a bridge between strategic intent and tactical execution.

For a pure document model (one big JSON blob), there's no change history, and concurrent modification between the computation layer and LLM layer creates conflicts. For pure event sourcing, the complexity of projections makes it harder to pass full state to the LLM. The **hybrid approach** — document for current state, event log for audit trail — gives you both. This is precisely what Marten implements.

### Marten: the recommended framework

**Marten** (v8.x, MIT licensed) is a .NET transactional document database and event store running on PostgreSQL using JSONB. It is the ideal tool for this exact stack. Marten provides event streams per entity (one stream per athlete's training plan), inline projections to materialize current state as a JSONB document, async projections for analytical views, optimistic concurrency via version-based conflict detection, and snapshots to avoid replaying long event histories.

The events for the running coach domain look like this:

```csharp
// Domain events — each is an immutable C# record
public record PlanCreated(Guid AthleteId, MacroCycle InitialPlan, string Methodology);
public record MesoCycleAdapted(int WeekNumber, MesoCycle Updated, string Rationale);
public record WorkoutModified(DateOnly Date, WorkoutSpec NewWorkout, string Reason);
public record LoadConstraintApplied(DateOnly Date, string Constraint, LoadMetrics Adjusted);
public record PhaseTransitioned(string FromPhase, string ToPhase, string Trigger);
public record PlanPaused(string Reason, DateOnly ResumeDate);
public record CoachingNoteAdded(DateOnly Date, string Note);
```

Both the computation layer (emitting `LoadConstraintApplied`) and the LLM layer (emitting `MesoCycleAdapted` with a human-readable `Rationale`) append to the same Marten stream. The inline projection rebuilds the current plan snapshot after each event. **The events ARE the audit trail** — no separate audit logging needed.

The companion library **Wolverine** handles reliable message processing with outbox patterns, ensuring that when the computation layer detects an ACWR violation and emits an event, the downstream LLM coaching call happens reliably even if the process crashes mid-flow.

### PostgreSQL-specific versioning patterns

Three complementary PostgreSQL patterns reinforce the Marten foundation. **Temporal tables** (using the `nearform/temporal_tables` PL/pgSQL extension, which works on managed PostgreSQL services like AWS RDS and Azure) add time-travel queries to projected views — useful for "show me what the plan looked like two weeks ago." **JSONB audit triggers** (following the PostgreSQL Wiki's audit trigger pattern or the pgMemento library) provide belt-and-suspenders compliance logging on the snapshot tables. And JSONB's native indexing with GIN indexes enables fast querying of plan attributes (e.g., finding all weeks where tempo volume exceeded a threshold).

The recommended composite approach: Marten for primary event sourcing and document storage, temporal table extensions on the projected snapshot table for time-travel queries, and a lightweight audit trigger as a safety net. Setup complexity for Marten is low — a NuGet package and roughly ten lines of configuration.

---

## Section 2: Context injection that stays lean despite massive headroom

With Claude's 200K context window, the running coach's full context payload consumes only **~15,000 tokens — about 7.5% of capacity**. The bottleneck is not token count but relevance and positional accuracy. Research consistently shows that LLMs exhibit a U-shaped performance curve: highest accuracy when critical information sits at the beginning or end of context, with a **30%+ accuracy drop for information buried in the middle** — a finding that holds even for explicitly long-context models.

### The concrete token budget

A detailed budget calculation for a typical coaching interaction:

| Component | Tokens | Notes |
|-----------|--------|-------|
| System prompt (persona, safety, format) | ~3,000 | Coaching methodology, guardrails, response structure |
| User profile | ~800 | Demographics, goals, PRs, injury history, preferences |
| Current plan state | ~2,500 | Phase overview, current week detail, next 2 weeks, compliance |
| Computed metrics | ~1,500 | ACWR, fitness estimates, pace zones, progress vs. targets |
| Recent training history | ~4,000 | Last 7 days raw, 2-4 weeks summarized, trend narrative |
| Conversation history | ~3,000 | Last 5-10 turns + cross-session summary |
| Current user message | ~200 | |
| **Total input** | **~15,000** | **7.5% of 200K window** |
| Reserved for response | ~4,000 | |
| Safety buffer | ~1,000 | |
| **Remaining headroom** | **~180,000** | |

This means you could include **all 16 weeks of detailed daily workout data** (~16,800 tokens) and still use under 16% of the window. The constraint is not space — it's signal quality.

### Compression through hierarchical summarization

Training history should be stored at five layers, each pre-computed by background jobs rather than generated at query time:

**Layer 0** (raw data, never in context): GPS tracks, per-second heart rate, every split. Lives in PostgreSQL, used by the computation layer.

**Layer 1** (per-workout summary, ~100-150 tokens each): "Tue 3/12: Easy run, 6.2mi, 8:45/mi avg, HR 142, felt 'good', slight left knee tightness." Generated by a background job after each workout sync.

**Layer 2** (weekly summary, ~200-300 tokens each): "Week 8: 38.2mi total (target 40mi, 95% compliance). 4 easy, 1 tempo (6:52/mi), 1 long (14mi). Easy pace improving 8:55→8:42. HR drift improving. Knee resolved Thursday." Generated Sunday night.

**Layer 3** (phase summary, ~300-500 tokens): "Base phase (Weeks 1-6): Built 25→38 mpw. Aerobic threshold ~7:15/mi. 90%+ compliance. No injuries." Generated at phase transitions.

**Layer 4** (trend narrative, ~500 tokens): An LLM-generated synthesis of patterns, concerns, and trajectory. "Over 8 weeks, excellent consistency and steady aerobic development. Easy pace improved 15 sec/mi while HR dropped 5bpm at same pace — strong cardiac adaptation. Tends to run ~5% faster than prescribed on easy days. Left knee is recurring minor concern that resolves with rest." Generated weekly or on demand.

This hierarchy draws from Wang et al.'s (2023) work on recursive summarization for long-term dialogue memory (arXiv:2308.15022), which showed that recursive summaries complement large context windows and produce more consistent responses. Mem0's production system takes this further, claiming **80-90% token reduction** while improving response quality by 26% through selective memory formation over blanket summarization.

### Context assembly and positional optimization

The .NET backend should run a **deterministic context assembler** that selects and positions context based on interaction type. For "how was my run?" queries, inject only the current week and last 1-3 workouts. For "am I ready for my race?" queries, inject the full remaining plan, 2-week summaries, and all trend metrics. For "I'm feeling tired," inject fatigue metrics, last 5-7 days raw, and injury flags.

Anthropic's guidance on positional optimization is critical: **place longform data at the top, queries at the end** — this can improve response quality by up to 30%. The recommended ordering for the running coach:

```xml
<system>Coaching persona, safety rules, response format</system>
<user_profile>Static/semi-static athlete data</user_profile>
<current_plan>Phase overview, current week, next weeks</current_plan>
<computed_metrics>ACWR, pace zones, fitness estimates</computed_metrics>
<training_history>Recent raw + older summaries</training_history>
<conversation_history>Recent turns</conversation_history>
<user_message>Current input</user_message>
```

This positions the most critical reference data (plan state, metrics) near the start and the conversational context near the end, matching the U-curve's high-accuracy zones.

### Prompt caching for cost efficiency

Anthropic's prompt caching reduces cached read tokens to **0.1x base input price** — a 90% cost saving. The system prompt (~3K tokens) plus user profile (~800 tokens) plus plan state (~2,500 tokens) form a ~6.3K token cached prefix that changes infrequently (weekly at most). Place this static content at the beginning of every request. At Sonnet's pricing, this saves approximately $2.70 per million tokens on cached reads for every conversation turn. The minimum cache size is 1,024 tokens, which the prefix easily exceeds.

### Production validation from health AI systems

Google's PH-LLM (Personal Health LLM, published in Nature Medicine 2025) validates this architecture. Fine-tuned on Gemini Ultra using **857 expert-annotated sleep and fitness case studies**, each containing up to 30 days of aggregated daily metrics (mean, variance, percentiles), the model scored **79% on sleep medicine exams and 88% on fitness exams** — exceeding human expert samples. The key insight: structured, pre-aggregated daily-resolution data outperforms raw data injection. This confirms that the computation layer should pre-digest all metrics before they reach the LLM.

Khan Academy's Khanmigo reinforced a parallel finding: accuracy improves significantly when the LLM has access to pre-computed exercise solutions before generating responses, rather than reasoning about raw content. For the running coach, this means the LLM should receive calculated ACWR values, pace zone computations, and fitness trend analyses from the deterministic layer — not raw GPS data it must interpret.

---

## Section 3: The adaptation loop — routing signals without overreacting

When new data arrives — a logged workout, a reported feeling, a life event — the system must decide how aggressively to respond. The central risk is cascading over-correction: one bad workout triggers a plan change, which disrupts the next workout, which triggers another change, creating oscillation. The solution draws from control theory, game AI, and coaching science: **a 5-level escalation ladder with hysteresis thresholds and PID-inspired dampening**.

### The five-level escalation ladder

**Level 0 — Absorb.** No plan change. Log the deviation, update exponentially weighted moving averages. One missed easy run, one workout 3% slower than target — these are noise. The deterministic layer handles this alone; the LLM is never invoked.

**Level 1 — Micro-adjust.** Modify the next 1-2 workouts. Triggered by 2-3 data points showing minor deviation, or a single missed key workout. The deterministic layer adjusts pace targets or swaps workout order. If Wednesday's interval session was missed, Thursday's easy run might become Wednesday's intervals and Thursday shifts to easy. No LLM reasoning needed for the swap itself, though the LLM may explain the change in conversation.

**Level 2 — Week restructure.** Rearrange the current week's plan. Triggered when ACWR exits the **0.8-1.3 sweet spot**, after 3+ consecutive missed days, or at illness onset. The LLM reasons about the best restructure; the deterministic layer validates the proposed changes against load constraints. This is the first level requiring actual LLM coaching judgment.

**Level 3 — Phase reconsideration.** Adjust mesocycle goals and targets. Triggered by 2+ weeks of sustained deviation, injury diagnosis, or major life changes (new job, relocation). The LLM re-evaluates periodization priorities; the deterministic layer recalculates mileage progression and intensity distribution.

**Level 4 — Plan overhaul.** Fundamental plan regeneration. Triggered by new race goals, major injury, or extended illness (>2 weeks). Requires the LLM to regenerate the macro plan and cascade changes downward. Should require user confirmation before executing.

### PID-inspired dampening prevents oscillation

The PID controller — the workhorse of industrial control systems, used in **95% of closed-loop manufacturing systems** — provides the right mental model even though implementation is via prompts and code rather than continuous control. The three terms map cleanly to coaching:

The **proportional term** sizes the response to the current error magnitude. A workout 5% off target gets a proportionally smaller response than one 20% off. The **integral term** (implemented as an exponentially weighted moving average) captures accumulated drift over time — three weeks of gradually declining performance indicates a systematic issue even if no single workout was alarming. The **derivative term** is the critical dampening mechanism: it considers whether the situation is getting worse, stable, or improving, and modulates response accordingly. Without derivative dampening, the system oscillates between over-correction and under-correction.

```python
class AdaptationDampener:
    """PID-inspired dampening for training plan adaptations."""
    
    def __init__(self, kp=0.3, ki=0.1, kd=0.2, alpha=0.3):
        self.kp, self.ki, self.kd = kp, ki, kd
        self.ema_error = 0.0       # Integral term (EMA, not raw sum)
        self.prev_error = None     # For derivative calculation
        self.alpha = alpha         # EMA smoothing factor
    
    def compute_response_magnitude(self, current_deviation: float) -> float:
        # Proportional: respond to current magnitude
        p = self.kp * current_deviation
        
        # Integral: accumulated trend (EMA prevents windup)
        self.ema_error = self.alpha * current_deviation + (1 - self.alpha) * self.ema_error
        i = self.ki * self.ema_error
        
        # Derivative: rate of change (dampening)
        d = 0.0
        if self.prev_error is not None:
            d = self.kd * (current_deviation - self.prev_error)
        self.prev_error = current_deviation
        
        # Clamp to prevent over-correction
        return clamp(p + i + d, MIN_ADJUSTMENT, MAX_ADJUSTMENT)
```

### Hysteresis prevents state-machine flip-flopping

A simple threshold creates oscillation: ACWR drops to 0.79 → enter "needs adjustment" → make changes → ACWR rises to 0.81 → return to "on track" → ACWR drops again. **Hysteresis** requires different thresholds for entering versus exiting a state. Enter "needs adjustment" when ACWR drops below 0.7; don't return to "on track" until it recovers above 0.85. This creates a stabilizing "dead zone" between states — a pattern drawn directly from portfolio rebalancing, where drift bands prevent excessive trading.

### Signal discrimination through EWMA trend detection

The EWMA (Exponential Weighted Moving Average) is specifically recommended over simple rolling averages for ACWR calculation in sports science. Murray et al. found EWMA shows stronger correlations with injury occurrence in team sport athletes because it's more sensitive to day-to-day workload changes while still smoothing noise. The computation layer should maintain EWMAs for performance deviation (actual vs. expected pace), subjective effort ratings, and recovery markers. Only when an EWMA crosses a threshold — not when a single data point does — should plan changes trigger.

### The decision tree for common coaching scenarios

The adaptation routing for specific scenarios follows clear patterns validated by coaching science and automated training platforms:

**Missed workouts** follow a graduated response. A single missed easy run is absorbed with no change — the body benefits from extra rest. A single missed key workout (intervals, tempo, long run) triggers a micro-adjust: can it be rescheduled tomorrow? If yes, swap days. If no, absorb it — never stack two key workouts on consecutive days. Two missed key workouts of the same type within a week trigger a week restructure to investigate whether the athlete is avoiding hard effort due to fatigue, schedule conflicts, or motivation. Three or more consecutive missed days escalate to phase reconsideration.

**Under-performance** uses percentage bands. Less than 5% off pace target is normal daily variation — absorb it. Between 5-15% off, note it and check confounding factors (heat, altitude, sleep, time of day). Only after 2-3 consecutive under-performances at this level should the system lower the intensity of the next key workout and re-evaluate pace targets. More than 15% off a single workout suggests possible illness onset or excessive fatigue — insert an extra rest day and check ACWR.

**Over-performance** requires a counterintuitive response. On easy days, running faster than prescribed is the **number one amateur mistake** and warrants a coaching conversation, not a plan upgrade. On key workouts, consistent over-performance (3+ sessions trending up via EWMA) may indicate a fitness breakthrough — but pace targets should increase conservatively, **2-3% at a time**, not chasing a single great workout.

**Illness** follows evidence-based return-to-running protocols. One to two days with no fever: resume normally after 24 hours symptom-free. Three to seven days mild: resume at 50-60% volume for 2-3 days. After any fever: wait 48-72 hours before running. One to two weeks: restructure the mesocycle with 30-50% volume reduction in week one back, no intensity work for 3-5 days. Longer than two weeks: plan overhaul with full macro re-evaluation.

### TrainerRoad as the most instructive commercial reference

TrainerRoad's Adaptive Training system, trained on **millions of completed workouts**, provides the closest production analogue. Its architecture uses **Progression Levels** (1-10 scale per training zone) as its state model, with every completed workout plus a post-workout RPE survey triggering adaptation. Crucially, TrainerRoad does not modify workouts themselves — it **replaces them with equivalent workouts at the appropriate difficulty level**. The plan structure (what type of workout on which day) stays constant; the prescription adapts. This is a key design insight: adapt the difficulty, not the structure, for routine adjustments.

---

## Section 4: Five domains that solved this problem before you

Cross-domain analysis reveals that adaptive plan management is a solved problem in multiple fields — each offering specific, transferable architectural patterns. The strongest insights come from adaptive learning (Bayesian individual parameter estimation), game AI (pacing state machines with hysteresis), and financial planning (drift-threshold rebalancing with explicit disruption costs).

### Adaptive learning platforms perfected individual state tracking

The education domain's core contribution is the **student model** — a probabilistic representation of what the learner knows, updated after every interaction. Three production systems demonstrate different approaches to this same idea.

**ALEKS** (Assessment and Learning in Knowledge Spaces) uses Knowledge Space Theory to maintain a knowledge state — a set of mastered topics from among trillions of possible states in a combinatorial lattice. It re-assesses the entire state every ~15 topics learned through periodic "progress knowledge checks," actively probing for decay rather than assuming mastery persists. The key paper is Cosyn, Uzun, Doble & Matayoshi (2021) in the *Journal of Mathematical Psychology*.

**Duolingo** runs a dual-model architecture. Birdbrain (logistic regression inspired by Item Response Theory) processes **1.25 billion daily exercises** to model P(correct) as a function of learner ability and exercise difficulty. Half-Life Regression (HLR) models per-word memory decay using exponential forgetting curves, where the "half-life" represents when recall probability drops to 50%. The half-life increases with correct recalls and decreases with errors. This is open-source at `github.com/duolingo/halflife-regression`.

**FSRS** (Free Spaced Repetition Scheduler), now native in Anki, is the most directly transferable algorithm. It models three components: **Retrievability** (probability of recall, exponentially decaying), **Stability** (days for retrievability to decay from 100% to 90%), and **Difficulty** (how hard it is to increase stability). FSRS uses **21 ML-optimized parameters** fitted to each user's review history, achieving 20-30% fewer reviews than SM-2 for the same retention rate.

The transfers to running coaching are precise. Stability maps to **detraining curves** — how long a fitness gain persists without stimulus. VO2max has S ≈ 14-21 days; lactate threshold S ≈ 14 days; neuromuscular speed S ≈ 7-10 days; aerobic base S ≈ 30+ days. Difficulty maps to individual training responsiveness — how easily a specific runner adapts to a given stimulus. Retrievability maps to current capability — what the runner can actually do today given time since last training stimulus. And periodic knowledge checks map to **periodic fitness assessments** (time trials, benchmark workouts) that re-assess the entire fitness state rather than trusting the model blindly.

### Game AI directors manage intensity pacing through state machines

Left 4 Dead's AI Director (Mike Booth, Valve, GDC 2009) is the canonical system. It tracks a per-survivor **Intensity value** (0-1 scalar) that increases when the player takes damage and decays over time. The Director cycles through four states: **Build Up** (full threat population until intensity peaks), **Sustain Peak** (continue for 3-5 seconds to ensure minimum peak duration), **Peak Fade** (minimal threats, let intensity decay naturally), and **Relax** (30-45 seconds of minimal threats before restarting the cycle).

The critical design insight, stated explicitly by the developers: **"The algorithm adjusts PACING, not DIFFICULTY."** The amplitude of individual encounters stays constant; their frequency and spacing change. This maps directly to periodization: modulate training **frequency and spacing** (rest day placement, session distribution) rather than constantly tweaking workout intensity. The Director's Build → Peak → Fade → Relax cycle is structurally identical to Build → Peak → Taper → Recovery in periodization.

The hysteresis in the Director's state transitions prevents premature exit from any phase. Peak Fade doesn't allow new threats until intensity actually drops — analogous to not cutting a recovery week short just because the athlete reports feeling good after two days. And boss encounters are **exempt from pacing modulation** — they happen regardless of intensity state. The running coach equivalent: key race-preparation workouts and the race itself are "protected" from auto-modification.

### Financial rebalancing taught us when NOT to intervene

Robo-advisors like Betterment and Wealthfront, managing **hundreds of billions of dollars**, face an identical tension: stick to the plan or adapt to reality. Their solution is **drift-threshold rebalancing**. Each asset class has a tolerance band (typically ±3-5%), and the portfolio is monitored daily but rebalanced only when any asset drifts outside its band. This creates an explicit **"dead zone"** where deviation is tolerated because the cost of changing (transaction fees, tax consequences, behavioral disruption) exceeds the benefit of perfect alignment.

Three specific concepts transfer directly. **Drift bands** become tolerance bands for training metrics: if weekly mileage is within ±15% of target and key workouts are completed, no intervention needed. **Cash-flow rebalancing** — using deposits and dividends to gradually correct allocation without selling — becomes using upcoming easy sessions to absorb deviations rather than restructuring the entire week. And **Monte Carlo goal projection** — running probability simulations of portfolio outcomes — becomes race readiness projection, where the system simulates possible fitness trajectories to determine whether the runner is still on track for their goal, intervening only when the projected probability drops below a threshold.

### Healthcare AI formalized sequential adaptive decisions

Dynamic Treatment Regimes (DTRs) from medical research formalize what the running coach needs: a sequence of decision rules where each rule maps patient history to treatment action, optimizing cumulative outcomes. SMART trials (Sequential Multiple Assignment Randomized Trials) implement this by re-randomizing non-responding patients to alternative treatments at each decision point — directly analogous to switching training approaches when a runner doesn't respond to a training block. The therapeutic window concept (maintaining drug dosage between ineffective and toxic levels) parallels the **training sweet spot** between insufficient stimulus and overtraining.

### Project management contributed "earned fitness" tracking

Earned Value Management's framework of Planned Value, Earned Value, and performance indices offers a compelling analog. **Planned Training Load** (what was scheduled), **Earned Training Load** (what was actually completed), and a **Training Performance Index** (ETL/PTL) provide a simple, intuitive metric for plan adherence. When TPI drops below threshold (e.g., <0.85), trigger adaptive response. Rolling wave planning — detailed plans for the near term, high-level for the future, progressively elaborated — validates the approach of generating micro-level prescriptions on demand while keeping macro/meso levels as lightweight templates.

---

## Section 5: The three-tier model survives — with four crucial modifications

The macro/meso/micro decomposition is **validated by coaching science** (it is the canonical model, used by every major framework from Matveyev to modern Norwegian world-class coaches), **reinforced by AI systems design** (the deliberative/sequencing/reactive three-layer architecture from robotics), and **confirmed as token-efficient** (~3,150 tokens for a full coaching context, under 2% of the 200K window). But the naive interpretation — storing detailed schedules at all three levels — fails under real-world conditions. Four modifications transform it from a rigid data storage model into a resilient planning architecture.

### Modification 1: tiers as decision boundaries, not stored schedules

The macro tier should store a lightweight **phase schedule with constraints** — phase type (base, build, peak, taper, race, recovery, maintenance), date ranges, target weekly volume range, intensity distribution targets, and allowed workout types. This costs ~200 tokens in context. The meso tier stores a **weekly template with slot types** — which days are hard, easy, long run, or rest, plus emphasis weights and volume targets. ~150 tokens. The micro tier is **generated on demand** for the current or next day, using macro constraints + meso template + recent performance data. ~500 tokens per workout.

This hybrid approach — store constraints at upper tiers, generate prescriptions at the lowest tier — gives the token efficiency benefit while maintaining plan coherence. It also makes the plan inherently adaptable: regenerating a day's workout from constraints is cheap, while the macro vision remains stable.

### Modification 2: continuous monitoring as a cross-cutting concern

ACWR and TSB (Training Stress Balance) monitoring operates independently of the tier hierarchy and can trigger replanning at any level. ACWR > 1.5 forces an immediate micro adjustment (reduce today's workout). ACWR persisting above 1.3 for 2+ weeks triggers meso adjustment (insert recovery week). CTL dropping more than 15% triggers macro re-evaluation (the athlete is detraining and the timeline may need extension). These thresholds use hysteresis — different values for entering vs. exiting concern states — to prevent flip-flopping.

### Modification 3: graceful degradation for goalless users

For runners without a race target, the macro tier degrades to a simple **"current emphasis" tag** (aerobic focus, speed maintenance, strength block, general maintenance) that rotates every 4-8 weeks. The meso tier becomes a rolling 4-week block with an automatic deload week (the Norwegian world-class coaches' pattern of deloading every 3rd or 4th week with 25-35% reduction). The micro tier generates based on constraints and recent load. The architecture is identical — three tiers — but the macro tier is lightweight enough to be almost invisible. A separate 2-tier codepath is unnecessary.

### Modification 4: event-driven recomposition for edge cases

When edge cases arise (injury, goal change, extended vacation), don't patch the existing plan — **regenerate from the appropriate tier downward**, following the Hierarchical Task Network (HTN) pattern from AI planning. Injury → regenerate macro (new phase schedule for recovery → rebuild → return to training), cascade to meso and micro. Vacation → regenerate meso for the return week, keep macro intact. Bad weather → regenerate micro only, keep meso/macro. Multi-race seasons → model the macro tier as containing multiple peak targets with mini-cycles between them, using block periodization's Accumulation → Transmutation → Realization sequence for each race.

The key architectural insight from edge case analysis: the **primary failure mode of the three-tier model is rigidity of stored plan data**. By storing constraints and templates rather than detailed schedules, and regenerating downward on disruption, every edge case becomes a regeneration trigger rather than a data corruption problem.

---

## Section 6: Recommended architecture — concrete implementation

This section synthesizes all findings into an implementable architecture. It provides a PostgreSQL data model, context injection payload structure, adaptation routing logic, and versioning approach — with pseudocode and schema sketches.

### The PostgreSQL data model

```sql
-- ================================================================
-- CORE PLAN STATE (Marten-managed event-sourced entities)
-- ================================================================

-- Macro tier: Phase schedule with constraints
-- Stored as Marten document, versioned via event stream
CREATE TABLE training_plans (
    id              UUID PRIMARY KEY,
    athlete_id      UUID NOT NULL REFERENCES athletes(id),
    version         BIGINT NOT NULL DEFAULT 1,
    status          VARCHAR(20) NOT NULL DEFAULT 'active',  -- active, paused, completed
    goal_type       VARCHAR(20) NOT NULL,                   -- race, maintenance, return_to_running
    goal_details    JSONB,        -- {race_name, race_date, target_time, distance} or null
    methodology     JSONB NOT NULL, -- 12+ configurable parameters
    phases          JSONB NOT NULL, -- Array of phase definitions (see below)
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- phases JSONB structure:
-- [
--   {
--     "phase_type": "base",
--     "start_week": 1, "end_week": 6,
--     "weekly_volume_range": {"min_miles": 25, "max_miles": 40},
--     "intensity_distribution": {"easy": 0.80, "moderate": 0.10, "hard": 0.10},
--     "allowed_workout_types": ["easy", "long_run", "strides", "tempo_intro"],
--     "constraints": {"max_long_run_pct": 0.30, "min_rest_days": 2}
--   },
--   {
--     "phase_type": "build",
--     "start_week": 7, "end_week": 12,
--     ...
--   }
-- ]

-- Meso tier: Weekly templates
CREATE TABLE weekly_templates (
    id              UUID PRIMARY KEY,
    plan_id         UUID NOT NULL REFERENCES training_plans(id),
    week_number     INT NOT NULL,
    phase_type      VARCHAR(20) NOT NULL,
    is_deload       BOOLEAN NOT NULL DEFAULT false,
    volume_target   DECIMAL(5,1) NOT NULL,  -- target miles
    slot_pattern    JSONB NOT NULL,          -- day-by-day slot definitions
    emphasis_weights JSONB,                  -- {"endurance": 0.6, "speed": 0.2, "strength": 0.2}
    coaching_notes  TEXT,                    -- LLM-authored rationale
    generated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(plan_id, week_number)
);

-- slot_pattern JSONB structure:
-- [
--   {"day_of_week": 1, "slot_type": "rest"},
--   {"day_of_week": 2, "slot_type": "easy", "target_miles": 6},
--   {"day_of_week": 3, "slot_type": "key", "workout_category": "intervals"},
--   {"day_of_week": 4, "slot_type": "easy", "target_miles": 5},
--   {"day_of_week": 5, "slot_type": "rest_or_easy"},
--   {"day_of_week": 6, "slot_type": "key", "workout_category": "tempo"},
--   {"day_of_week": 7, "slot_type": "long_run", "target_miles": 14}
-- ]

-- Micro tier: Daily prescriptions (generated on demand, stored after generation)
CREATE TABLE daily_prescriptions (
    id              UUID PRIMARY KEY,
    template_id     UUID NOT NULL REFERENCES weekly_templates(id),
    date            DATE NOT NULL,
    workout_type    VARCHAR(30) NOT NULL,
    prescription    JSONB NOT NULL,          -- Full workout specification
    planned_load    JSONB NOT NULL,          -- {estimated_tss, estimated_miles, estimated_duration}
    coaching_notes  TEXT,                    -- LLM-authored explanation
    status          VARCHAR(20) NOT NULL DEFAULT 'upcoming',  -- upcoming, completed, skipped, modified
    actual_result   JSONB,                   -- Filled after workout completion
    generated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at    TIMESTAMPTZ,
    UNIQUE(template_id, date)
);

-- prescription JSONB structure:
-- {
--   "warmup": {"miles": 1.5, "pace": "easy", "notes": "Start very easy"},
--   "main_set": {
--     "type": "intervals",
--     "repeats": [
--       {"distance_meters": 800, "target_pace": "6:30/mi", "recovery": "400m jog"}
--     ],
--     "sets": 6
--   },
--   "cooldown": {"miles": 1.5, "pace": "easy"},
--   "total_miles": 7.2,
--   "target_zones": {"z4": 0.40, "z1": 0.60}
-- }

-- ================================================================
-- EVENT STORE (Marten-managed, shown here for clarity)
-- ================================================================
CREATE TABLE plan_events (
    seq_id          BIGSERIAL NOT NULL,
    id              UUID NOT NULL,
    stream_id       UUID NOT NULL,           -- = training_plans.id
    version         BIGINT NOT NULL,
    event_type      VARCHAR(200) NOT NULL,
    data            JSONB NOT NULL,          -- Event payload
    metadata        JSONB,                   -- {triggered_by, source_layer, confidence}
    timestamp       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Example events in the stream:
-- {event_type: "PlanCreated", data: {athlete_id, methodology, phases, ...}}
-- {event_type: "MesoCycleAdapted", data: {week: 8, changes: {...}, rationale: "..."}}
-- {event_type: "WorkoutModified", data: {date: "2026-04-15", old: {...}, new: {...}, reason: "..."}}
-- {event_type: "LoadConstraintApplied", data: {date: "2026-04-15", constraint: "ACWR_CAP", ...}}
-- {event_type: "PhaseExtended", data: {phase: "base", new_end_week: 8, reason: "illness recovery"}}
-- {event_type: "EscalationTriggered", data: {level: 3, trigger: "ACWR<0.6 for 2 weeks", ...}}

-- ================================================================
-- MONITORING STATE (computed by deterministic layer)
-- ================================================================
CREATE TABLE athlete_monitoring (
    athlete_id      UUID PRIMARY KEY REFERENCES athletes(id),
    acwr            DECIMAL(4,2) NOT NULL,
    ctl             DECIMAL(6,1) NOT NULL,   -- Chronic Training Load (42-day EWMA)
    atl             DECIMAL(6,1) NOT NULL,   -- Acute Training Load (7-day EWMA)
    tsb             DECIMAL(6,1) NOT NULL,   -- Training Stress Balance (CTL - ATL)
    plan_state      VARCHAR(30) NOT NULL DEFAULT 'on_track',
    -- on_track | minor_deviation | needs_adjustment | needs_restructuring | crisis
    streak_missed   INT NOT NULL DEFAULT 0,
    ewma_pace_dev   DECIMAL(5,3),            -- EMA of pace deviation from targets
    ewma_rpe_dev    DECIMAL(5,3),            -- EMA of RPE deviation from expected
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ================================================================
-- SUMMARIZATION LAYERS (pre-computed by background jobs)
-- ================================================================
CREATE TABLE workout_summaries (
    id              UUID PRIMARY KEY,
    athlete_id      UUID NOT NULL,
    date            DATE NOT NULL,
    layer           INT NOT NULL,            -- 1=daily, 2=weekly, 3=phase, 4=trend
    period_start    DATE NOT NULL,
    period_end      DATE NOT NULL,
    summary_text    TEXT NOT NULL,            -- Pre-generated natural language summary
    metrics         JSONB,                   -- Key numeric aggregates
    token_count     INT NOT NULL,            -- Actual token count for budget tracking
    generated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### Context injection in practice

The .NET `ContextAssembler` builds the payload for each LLM call. Here's the concrete structure:

```csharp
public class ContextAssembler
{
    public async Task<CoachingContext> AssembleContext(
        Guid athleteId, 
        string userMessage, 
        InteractionType interactionType)
    {
        // 1. Always-present components (cacheable prefix, ~6.3K tokens)
        var systemPrompt = await GetSystemPrompt(athleteId);        // ~3,000 tokens
        var userProfile = await GetUserProfile(athleteId);           // ~800 tokens
        var planState = await GetCurrentPlanState(athleteId);        // ~2,500 tokens
        
        // 2. Dynamic components (vary per request, ~8-12K tokens)
        var metrics = await GetComputedMetrics(athleteId);           // ~1,500 tokens
        var history = await GetTrainingHistory(                      // ~2,000-4,000 tokens
            athleteId, interactionType);
        var conversation = await GetConversationHistory(             // ~2,000-3,000 tokens
            athleteId, maxTurns: GetMaxTurns(interactionType));
        
        return new CoachingContext
        {
            // Cacheable prefix (position: START for prompt caching + accuracy)
            SystemPrompt = systemPrompt,
            UserProfile = userProfile,
            PlanState = planState,
            
            // Dynamic middle section
            Metrics = metrics,
            TrainingHistory = history,
            
            // Conversation at END (matches "lost in the middle" optimization)
            ConversationHistory = conversation,
            CurrentMessage = userMessage
        };
    }
    
    private async Task<TrainingHistoryContext> GetTrainingHistory(
        Guid athleteId, InteractionType type)
    {
        return type switch
        {
            InteractionType.WorkoutFeedback => new TrainingHistoryContext
            {
                RecentRaw = await GetSummaries(athleteId, layer: 1, days: 3),
                WeeklySummaries = null,  // Not needed for single-workout feedback
                TrendNarrative = null
            },
            InteractionType.PlanAdjustment => new TrainingHistoryContext
            {
                RecentRaw = await GetSummaries(athleteId, layer: 1, days: 7),
                WeeklySummaries = await GetSummaries(athleteId, layer: 2, weeks: 8),
                TrendNarrative = await GetLatestSummary(athleteId, layer: 4)
            },
            InteractionType.RaceReadiness => new TrainingHistoryContext
            {
                RecentRaw = await GetSummaries(athleteId, layer: 1, days: 14),
                WeeklySummaries = await GetSummaries(athleteId, layer: 2, weeks: 16),
                TrendNarrative = await GetLatestSummary(athleteId, layer: 4)
            },
            _ => new TrainingHistoryContext  // General coaching chat
            {
                RecentRaw = await GetSummaries(athleteId, layer: 1, days: 7),
                WeeklySummaries = await GetSummaries(athleteId, layer: 2, weeks: 4),
                TrendNarrative = null
            }
        };
    }
}
```

The assembled payload renders as structured XML:

```xml
<system>
  You are Coach [Name], an expert running coach using [Methodology] principles.
  The athlete's training plan is managed collaboratively: you provide coaching
  judgment, adaptation reasoning, and explanations. A separate computation 
  system handles all pace calculations, load monitoring, and safety validation.
  
  When adapting the plan, output structured JSON matching the plan schema.
  Always explain your reasoning. Never override safety constraints.
  
  [Response format instructions, safety guardrails, persona details]
</system>

<user_profile>
  Name: Sarah Chen | Age: 34 | Experience: 5 years | Goal: Boston Marathon 2026
  Target: 3:25 | Current VDOT: 42.5 | Threshold pace: 7:15/mi
  Injury history: IT band (2024, resolved), left knee (recurring, minor)
  Schedule: Prefers AM runs, track access Wed, long runs Sunday
  Preferences: Enjoys tempo work, dislikes treadmill
</user_profile>

<current_plan>
  Plan: 16-week Boston Marathon | Phase: Build 2 (weeks 7-12) | Current: Week 8
  Weekly targets: 38-42 miles, intensity distribution 78/12/10 (easy/moderate/hard)
  
  This week (Week 8):
    Mon: Rest | Tue: 7mi easy | Wed: 6×800m @ 6:30/mi (w/ 400m jog recovery)
    Thu: 5mi easy | Fri: Rest | Sat: 8mi w/ 4mi @ 7:05 tempo | Sun: 16mi long
  
  Next week (Week 9): Recovery week — volume drops to 32mi, no intervals
  Compliance this plan: 92% overall, 100% key workouts through Week 7
</current_plan>

<computed_metrics>
  ACWR: 1.08 (sweet spot) | CTL: 42.3 | ATL: 45.7 | TSB: -3.4
  Plan state: ON_TRACK | Consecutive missed: 0
  Easy pace trend (8 weeks): 8:55 → 8:40/mi (improving)
  HR at easy pace trend: 148 → 143 bpm (improving)
  EWMA pace deviation: -0.02 (slightly faster than prescribed)
  EWMA RPE deviation: +0.3 (slightly harder than expected)
  Estimated current VDOT: 43.2 (up from 42.5 at plan start)
</computed_metrics>

<training_history>
  [Layer 1 - Last 7 days]
  Mon 3/16: Rest (planned)
  Sun 3/15: Long run 15.2mi, 8:52/mi avg, HR 148, felt "strong through 12, 
    tired last 3", fueling strategy worked well
  Sat 3/14: Tempo 8.1mi total, 4mi @ 7:02/mi (target 7:05), HR 168, 
    felt "controlled", slight left knee awareness post-run
  [... remaining days ...]
  
  [Layer 2 - Weeks 5-8 summaries]
  Week 7: 40.1mi (target 40). 4 easy, 1 interval (5×1000m, all on pace), 
    1 long (15mi). Strong week, all key workouts completed.
  Week 6: 38.5mi (target 38). Recovery week executed well. Easy pace 8:42.
  [... remaining weeks ...]
  
  [Layer 4 - Trend narrative]
  Over 8 weeks, Sarah has demonstrated excellent consistency with 92% overall
  compliance and 100% key workout completion. Aerobic development is strong:
  easy pace improved 15 sec/mi while HR dropped 5bpm at same pace. She tends 
  to run 2-3% faster than prescribed on easy days. Left knee is a recurring 
  minor concern (3 mentions) that resolves with rest. Ready for continued 
  build phase progression.
</training_history>

<conversation_history>
  [Last 5 turns of current conversation]
</conversation_history>
```

### Adaptation routing logic

The signal-processing pipeline runs in the deterministic layer before any LLM invocation:

```csharp
public class AdaptationRouter
{
    public async Task<AdaptationDecision> Route(IncomingSignal signal, Guid athleteId)
    {
        var monitoring = await _monitoringRepo.Get(athleteId);
        var plan = await _planRepo.GetCurrent(athleteId);
        
        // Step 1: Classify signal severity
        var severity = ClassifySignal(signal, monitoring);
        
        // Step 2: Update continuous metrics (EWMA, ACWR, state machine)
        var updatedMonitoring = UpdateMetrics(monitoring, signal);
        
        // Step 3: Check hysteresis thresholds for state transitions
        var newState = EvaluateStateTransition(
            currentState: monitoring.PlanState,
            acwr: updatedMonitoring.Acwr,
            streakMissed: updatedMonitoring.StreakMissed,
            signalType: signal.Type
        );
        
        // Step 4: Determine escalation level
        var level = DetermineEscalationLevel(severity, newState, signal);
        
        // Step 5: Route to appropriate handler
        return level switch
        {
            EscalationLevel.Absorb => new AdaptationDecision 
            { 
                Action = "log_only", 
                RequiresLlm = false 
            },
            EscalationLevel.MicroAdjust => await HandleMicroAdjust(
                signal, plan, updatedMonitoring),  // Deterministic only
            EscalationLevel.WeekRestructure => new AdaptationDecision
            {
                Action = "restructure_week",
                RequiresLlm = true,
                LlmContext = BuildAdaptationContext(plan, updatedMonitoring, signal),
                Constraints = GetSafetyConstraints(updatedMonitoring)
            },
            EscalationLevel.PhaseReconsider => new AdaptationDecision
            {
                Action = "reconsider_phase",
                RequiresLlm = true,
                LlmContext = BuildFullPlanContext(plan, updatedMonitoring, signal),
                Constraints = GetSafetyConstraints(updatedMonitoring),
                RequiresUserConfirmation = false
            },
            EscalationLevel.PlanOverhaul => new AdaptationDecision
            {
                Action = "overhaul_plan",
                RequiresLlm = true,
                LlmContext = BuildFullPlanContext(plan, updatedMonitoring, signal),
                Constraints = GetSafetyConstraints(updatedMonitoring),
                RequiresUserConfirmation = true  // Major changes need user buy-in
            }
        };
    }
    
    private PlanState EvaluateStateTransition(
        PlanState currentState, decimal acwr, int streakMissed, SignalType signalType)
    {
        // Hysteresis: different thresholds for entering vs. exiting states
        return currentState switch
        {
            PlanState.OnTrack when acwr < 0.70m || streakMissed >= 3 
                => PlanState.NeedsAdjustment,
            PlanState.OnTrack when acwr < 0.80m || streakMissed >= 2 
                => PlanState.MinorDeviation,
            PlanState.OnTrack when signalType == SignalType.InjuryReported 
                => PlanState.NeedsRestructuring,
                
            // Higher threshold to EXIT deviation states (hysteresis)
            PlanState.MinorDeviation when acwr > 0.90m && streakMissed == 0 
                => PlanState.OnTrack,
            PlanState.MinorDeviation when acwr < 0.70m || streakMissed >= 3 
                => PlanState.NeedsAdjustment,
                
            PlanState.NeedsAdjustment when acwr > 0.95m && streakMissed == 0 
                => PlanState.MinorDeviation,  // Step down, not jump to OnTrack
            PlanState.NeedsAdjustment when acwr < 0.60m || streakMissed >= 7 
                => PlanState.NeedsRestructuring,
                
            _ => currentState  // No transition
        };
    }
}
```

### Versioning and auditability through Marten events

Every adaptation decision — whether from the deterministic layer or the LLM — is recorded as an event in the Marten stream:

```csharp
public class AdaptationExecutor
{
    public async Task Execute(AdaptationDecision decision, Guid planId)
    {
        var session = _store.LightweightSession();
        
        if (decision.RequiresLlm)
        {
            // Call Claude with structured output enforcement
            var llmResponse = await _claudeClient.Generate<PlanModification>(
                context: decision.LlmContext,
                schema: PlanModificationSchema,
                constraints: decision.Constraints);
            
            // Validate LLM output against deterministic safety constraints
            var validation = _safetyValidator.Validate(llmResponse, decision.Constraints);
            if (!validation.IsValid)
            {
                // Re-prompt with constraint feedback
                llmResponse = await _claudeClient.Generate<PlanModification>(
                    context: decision.LlmContext + validation.Feedback,
                    schema: PlanModificationSchema,
                    constraints: decision.Constraints);
            }
            
            // Emit event with full audit trail
            session.Events.Append(planId, new MesoCycleAdapted(
                WeekNumber: llmResponse.AffectedWeek,
                Changes: llmResponse.Changes,
                Rationale: llmResponse.Rationale,     // LLM-authored explanation
                Trigger: decision.TriggerDescription,  // What caused this
                EscalationLevel: decision.Level,
                MonitoringSnapshot: decision.Monitoring // Metrics at time of decision
            ));
        }
        else
        {
            // Deterministic-only adjustment
            session.Events.Append(planId, new LoadConstraintApplied(
                Date: decision.AffectedDate,
                Constraint: decision.ConstraintType,
                AdjustedLoad: decision.NewLoad,
                Trigger: decision.TriggerDescription
            ));
        }
        
        await session.SaveChangesAsync(); // Atomic: event + projection update
    }
}
```

The event stream provides complete auditability. To understand why a workout changed, query the event stream for that plan and date. Every event includes the trigger (what signal caused it), the escalation level, the monitoring snapshot at the time of decision, and for LLM-driven changes, the full rationale. To undo a change, replay the stream excluding the unwanted event and regenerate the projection — a native capability of event sourcing.

### The complete system flow

```
                    ┌──────────────────────────────────┐
                    │         Incoming Signal           │
                    │  (workout log, feeling report,    │
                    │   life event, health data)        │
                    └──────────────┬───────────────────┘
                                   │
                    ┌──────────────▼───────────────────┐
                    │    DETERMINISTIC LAYER            │
                    │                                    │
                    │  1. Update EWMA metrics            │
                    │  2. Recalculate ACWR/CTL/ATL/TSB   │
                    │  3. Run state machine transitions   │
                    │     (with hysteresis thresholds)    │
                    │  4. Determine escalation level      │
                    │  5. Apply safety constraints        │
                    └──────────┬──────────┬────────────┘
                               │          │
                    Level 0-1  │          │  Level 2-4
                    (no LLM)   │          │  (needs LLM)
                               │          │
                    ┌──────────▼──┐  ┌────▼─────────────┐
                    │ Log + minor  │  │ CONTEXT ASSEMBLER │
                    │ deterministic│  │                    │
                    │ adjustment   │  │ Assemble payload:  │
                    └──────────┬──┘  │ profile + plan +   │
                               │     │ metrics + history + │
                               │     │ conversation        │
                               │     └────────┬────────────┘
                               │              │
                               │     ┌────────▼────────────┐
                               │     │   CLAUDE (200K)      │
                               │     │                      │
                               │     │ Structured output →   │
                               │     │ plan modification     │
                               │     │ + coaching rationale  │
                               │     └────────┬────────────┘
                               │              │
                               │     ┌────────▼────────────┐
                               │     │  SAFETY VALIDATOR    │
                               │     │                      │
                               │     │ ACWR still 0.8-1.3?  │
                               │     │ Mileage ≤ 10% jump?  │
                               │     │ Recovery preserved?   │
                               │     │ Max intensity days?   │
                               │     └────────┬────────────┘
                               │              │
                    ┌──────────▼──────────────▼──────────┐
                    │        MARTEN EVENT STORE           │
                    │                                      │
                    │  Append event to plan stream          │
                    │  Update inline projection (snapshot)  │
                    │  Trigger async projections            │
                    │  (load history, compliance tracking)  │
                    └──────────────────────────────────────┘
```

### Key implementation decisions summarized

**Use Marten (NuGet: `Marten`) as your primary persistence framework.** It gives you event sourcing + document storage on PostgreSQL with C# record types, optimistic concurrency, snapshots, and projections — all native to your .NET + PostgreSQL stack. Setup is approximately ten lines of configuration.

**Use Claude's structured outputs with a JSON Schema matching your plan data model.** This guarantees that every LLM-generated plan modification is schema-valid before it reaches your safety validation layer. Define the schema once in Pydantic-equivalent C# records, enforce it at the API call level.

**Pre-compute all numerical values in the deterministic layer.** The LLM should never calculate pace targets, ACWR values, or mileage progressions. It receives pre-computed metrics and focuses on interpretation, coaching judgment, and natural-language explanation. This mirrors the lesson from Khan Academy's Khanmigo (accuracy improves when the LLM has pre-computed solutions) and Google's PH-LLM (trained on pre-aggregated daily metrics, not raw sensor data).

**Generate micro-level prescriptions on demand rather than storing a full 16-week daily schedule.** Store macro phases as constraints and meso weeks as templates. When the athlete or the system needs tomorrow's workout, generate it from the current meso template + macro constraints + monitoring state. This keeps the plan inherently adaptive and reduces stale data.

**Run background summarization jobs** (post-workout for Layer 1, weekly for Layer 2, at phase transitions for Layer 3, weekly for Layer 4 trend narratives) to pre-compute the compressed training history that feeds into context injection. Store these summaries with their token counts for precise budget management.

**Apply prompt caching to the stable prefix** (~6.3K tokens of system prompt + user profile + plan state) for 90% cost reduction on cached reads. Position this prefix at the start of every request, matching both the caching requirement and the "lost in the middle" optimization that places critical reference data at the beginning.

The overall architecture preserves a clean separation: the deterministic layer owns all numbers and safety, the LLM owns all reasoning and communication, the plan state layer sits between them as an event-sourced, version-tracked, schema-validated artifact that both layers can read and write through clearly defined interfaces. This is not theoretical — every component (Marten, Claude structured outputs, EWMA calculations, prompt caching) is production-ready today and native to the PostgreSQL + .NET + Claude stack.