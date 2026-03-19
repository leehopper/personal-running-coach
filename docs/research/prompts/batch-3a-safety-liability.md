# Research Prompt: Safety, Liability, and Legal Guardrails for AI Fitness Coaching (R-003)

## What I Need

I'm building an AI running coach — an LLM-powered adaptive training plan manager. Before I ship this to anyone beyond myself, I need to understand the real legal and safety landscape. I already have a strong technical safety architecture (described below), but I need the legal and regulatory picture.

## What I've Already Decided (Technical Safety)

For context on what the product already does to mitigate risk:

- **Deterministic computation layer** handles all pace calculations, load monitoring (ACWR, CTL, ATL, TSB), and safety guardrail enforcement. The LLM literally cannot prescribe something the code layer blocks.
- **Hard safety guardrails in code:** single-run spike ≤30% above longest in past 30 days, ACWR 0.8-2.0 range, ≥70% easy volume, max 3 quality sessions/week, minimum 1 easy day between hard days.
- **Five-level escalation ladder** with hysteresis thresholds — the system is designed not to overreact.
- **LLM handles coaching conversation only** — explanation, adaptation reasoning, motivation. Never raw plan generation or pace math.
- **Structured outputs** guarantee schema-valid plan modifications.
- **Adversarial safety testing** with 50+ scenarios covering injury signals, medical scope, overtraining, and jailbreak resistance.

The remaining risk surface is: (1) qualitative coaching advice that's contextually inappropriate (e.g., encouraging someone who should see a doctor), (2) users who ignore guardrails or misrepresent their condition, (3) edge cases the deterministic layer doesn't cover.

## What I Need to Understand

**1. Legal liability landscape for AI fitness products**

What is the actual legal exposure for an AI-generated training plan that leads to injury? I want real cases, not hypothetical fears.

- What lawsuits have been filed against fitness apps, personal training software, or AI coaching products? What were the outcomes?
- How do courts currently view AI-generated advice vs. human-generated advice in terms of liability?
- What's the difference between "information" (protected) and "professional advice" (regulated)? Where does an AI running coach fall?
- Is there precedent for the defense that "users can get equivalent advice from ChatGPT directly" — does packaging it as a product increase liability?
- What about the "learned intermediary" doctrine — does the user's own judgment break the liability chain?

**2. Regulatory classification**

- FDA's 2024 guidance on wellness/fitness software — what exactly is excluded vs. regulated? Where does an adaptive training plan fall?
- EU AI Act classification for fitness/coaching AI — what risk tier? What are the requirements?
- FTC enforcement actions against fitness/wellness AI claims — what claims trigger scrutiny?
- HIPAA — does training data (pace, HR, mileage, subjective reports) constitute Protected Health Information? What if we integrate with health platforms like Apple Health?
- State-by-state variations in the US — any states with specific AI fitness/coaching regulations?

**3. What competitors actually do**

Concrete examples of legal/safety approaches from:
- Strava (recently acquired Runna for AI plans)
- Garmin Coach / COROS Coach
- TrainAsONE
- Whoop (recovery recommendations)
- Fitbod (strength training AI)
- Freeletics / Peloton (adaptive training)
- Noom / MyFitnessPal (health-adjacent)
- Any AI coaching startups (Kotcha, etc.)

For each: What disclaimers do they use? Do they require health screening? What's in their Terms of Service re: liability? Have any faced lawsuits or regulatory action?

**4. What disclaimers and legal infrastructure I actually need**

Be specific and practical:
- What disclaimer language is legally meaningful vs. theatrical?
- Should there be a health screening gate (PAR-Q or similar) before onboarding?
- What Terms of Service provisions are standard and which are critical?
- Do I need professional liability insurance? At what stage?
- What's the minimum legal infrastructure for MVP-0 (personal use), MVP-1 (friends/testers), and a public beta?

**5. The honest risk assessment**

I want a candid view:
- What's the realistic worst-case scenario for an AI running coach? Not "someone dies" abstractly — what's the actual chain of events, probability, and legal outcome?
- What risks are overblown by caution (where I might over-engineer safety theater)?
- What risks are underestimated (where I might have a blind spot)?
- What is the incremental risk vs. a human coach, vs. a static plan from a book, vs. asking ChatGPT directly?
- How does my deterministic safety layer change the risk profile compared to a pure LLM approach?

## What I Don't Need

- Generic "consult a lawyer" advice (I will, but I need to know the right questions to ask)
- Hypothetical fear-mongering without evidence
- Coverage of medical device regulation (the product doesn't diagnose or treat)
- Deep dive into GDPR/privacy (that's a separate research topic)

## Output Format

Structure findings as:
1. **Legal liability landscape** — real cases, current legal framework, where AI coaching fits
2. **Regulatory map** — FDA, EU AI Act, FTC, HIPAA applicability (with clear YES/NO/MAYBE for each)
3. **Competitor legal approaches** — what they do, organized by approach type
4. **Practical legal requirements** — staged by MVP-0 → MVP-1 → public beta → scale
5. **Honest risk matrix** — likelihood × impact for the real scenarios, with the incremental risk analysis

Cite real cases, statutes, and guidance documents. Flag where the law is unsettled or evolving. Be specific enough that I can take the findings to a lawyer and have a productive conversation.
