# Research Prompt: Coaching Conversation Design & Behavioral Psychology

## What I Need

Research into how effective running coaches communicate — specifically in difficult moments (plan changes, setbacks, corrections, injury) — and what sports psychology says about motivation, adherence, and trust in coaching relationships. The goal is to inform the AI coaching persona's conversational behavior. I need patterns I can translate into prompt engineering, not abstract psychology theory.

## Context About the Product

I'm building an AI running coach where the LLM handles coaching conversation, plan explanation, and adaptation reasoning. The deterministic layer handles the "what" (pace math, load monitoring, safety guardrails). The LLM layer handles the "how" — this research is about the "how."

### What the AI already does (architecture context)

The system has a **five-level escalation ladder** for adapting plans:
- Level 0: Absorb — log the deviation, say nothing, continue as planned
- Level 1: Micro-adjust — deterministic swap of next 1-2 workouts (e.g., "I shifted tomorrow to an easy run based on your fatigue report")
- Level 2: Week restructure — LLM reasoning about how to reorganize the current week
- Level 3: Phase reconsideration — LLM re-evaluates periodization and multi-week structure
- Level 4: Plan overhaul — major change, requires user confirmation before executing

The system uses a **traffic-light daily adaptation** model (Green/Amber/Red) and **EWMA trend detection** — single bad data points are absorbed, only sustained patterns trigger escalation.

Safety guardrails are deterministic: single-run spike ≤30%, ACWR 0.8-2.0, ≥70% easy volume, max 3 quality sessions/week. The LLM never generates raw plans or paces.

### Specific behavioral challenges the AI will face

From our research so far, these are the most common and difficult coaching conversations:

1. **Easy day over-performance** — the most common coaching challenge. Users consistently run faster than prescribed on easy days, undermining aerobic development and increasing injury risk. A coaching conversation is warranted, but how do you correct this without sounding scolding?

2. **Plan downgrades after illness/injury** — the system reduces volume (50-60% after 3-7 days illness, full rehaul after 2+ weeks). Users are often demoralized. How do you communicate "we're pulling back" without it feeling like punishment?

3. **Goal recalibration** — the AI determines a user's goal (e.g., sub-4:00 marathon) is unrealistic based on training data. How do you surface this honestly without crushing motivation?

4. **Missed workouts / inconsistency** — users miss multiple workouts. The system never redistributes missed mileage (moves forward), but how does the coach acknowledge missed work without guilt-tripping?

5. **Injury discussion scope boundaries** — users will describe pain, ask about injuries, push for diagnosis. The system has hard keyword triggers that auto-generate "see a professional" responses for medical scope topics (DEC-019). But the conversational framing around these triggers matters — "I can't help with that" feels dismissive.

6. **Rest day resistance** — ~8.6% of amateur runners meet exercise addiction criteria (R-003). Users who push back on rest days or express distress about not running need a specific conversational approach.

7. **Returning after a long break** — the coach's first message back should feel like a friend catching up, not a system re-activating.

8. **The "coach training phase"** — first ~2 weeks where the AI is learning the user. How does the coach set expectations that the plan will improve, ask questions naturally, and manage the user's patience with early-stage generic plans?

## Specific Questions

### 1. How Human Running Coaches Communicate
- What communication patterns do effective human running coaches use for difficult conversations (plan changes, corrections, bad news about goals)?
- How do coaches handle the easy-day-too-fast problem specifically? This is the #1 coaching challenge — what actually works?
- What's the difference between coaching communication that builds trust vs. erodes it?
- How do coaches frame setbacks (illness, injury, missed training) as part of the journey rather than failures?
- What does "earning trust through conservative defaults" look like in practice? How do new coach-athlete relationships establish credibility?
- How do coaches balance honesty (your goal is unrealistic) with motivation (but here's what you CAN achieve)?

### 2. Sports Psychology of Running Adherence
- What does research say about motivation types in recreational runners? (Intrinsic vs. extrinsic, mastery vs. performance orientation, self-determination theory applied to running)
- What communication approaches increase vs. decrease training plan adherence?
- How does autonomy support (giving the runner control/choice) affect adherence compared to directive coaching?
- What role does perceived competence play? How does feedback that threatens competence perception affect behavior?
- What does the research say about how runners respond to AI vs. human coaching? Any studies on trust, adherence, or satisfaction?
- What behavioral patterns predict dropout from coaching relationships? What conversational patterns prevent it?

### 3. Conversational Patterns for Specific Scenarios
For each of the 8 behavioral challenges listed above, I need:
- What sports psychology or coaching research says about this specific scenario
- Concrete conversational pattern recommendations (not just "be empathetic" — actual structural approaches)
- What to avoid (anti-patterns that coaches learn the hard way)
- Example framing that translates to prompt engineering (e.g., "acknowledge → validate → reframe → forward action")

### 4. AI-Specific Coaching Considerations
- How should an AI coach handle the "uncanny valley" of coaching — being conversational enough to feel like coaching but not pretending to be human?
- What transparency is needed about the AI's reasoning? (R-004 established that the architecture supports "showing its work" — but how much should it show?)
- How should the AI handle moments where a human coach would use physical observation ("you look tired today")?
- What does the research say about users' emotional responses to AI feedback vs. human feedback on sensitive topics (body, performance, ability)?
- How should the AI adapt its communication style based on user personality? Is this feasible, or should it have one consistent persona?

### 5. Practical Prompt Engineering Implications
- What conversational scaffolding or templates emerge from this research that can be encoded in a system prompt?
- Are there specific phrases, framings, or structural patterns that consistently produce better coaching outcomes?
- What are the "never say this" rules for a running coach (AI or human)?
- How should the coaching persona handle humor, encouragement, and tough love? What's the default balance?

## What I DON'T Need
- General advice about "being empathetic" or "using positive language"
- AI ethics discussions unrelated to coaching communication
- Chatbot design patterns for customer service (different domain, different psychology)
- Technical prompt engineering syntax (I'll handle the implementation)

## Output Format

Structure the findings as:
1. **Coaching communication fundamentals** — what research and practice say about effective coaching communication in running
2. **Scenario-by-scenario playbook** — specific conversational approaches for each of the 8 behavioral challenges, with concrete patterns and anti-patterns
3. **Sports psychology foundations** — key findings on motivation, adherence, autonomy, and trust that inform the coaching persona
4. **AI-specific considerations** — how AI coaching differs from human coaching and what adjustments are needed
5. **Prompt engineering implications** — concrete conversational scaffolding, "always do" / "never do" rules, persona guidelines that can be encoded in the system prompt
