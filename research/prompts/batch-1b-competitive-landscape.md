# Research Prompt: Batch 1b — R-002
# Competitive Landscape & Market Opportunity for AI Running Coaches

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

I'm in the early planning stages of building an AI-powered running coach — an app that builds, maintains, and continuously optimizes personalized training plans through natural conversation and real-time adaptation. Before I invest in building, I need to deeply understand what already exists in this space and whether there's a real gap.

### What I think my differentiator is

Most running apps and coaching tools fall into two camps: (1) static plan generators that slot you into a template and never adjust, or (2) expensive human coaches. My thesis is that no product currently provides a persistent, adaptive AI coaching relationship out of the box — one that maintains ongoing context about a specific runner, reasons about their training history, and proactively adjusts plans based on real-world feedback without the user needing to re-explain their situation each time.

My app would be a **planning intelligence layer**, not a workout tracker. It doesn't do live GPS tracking or compete with Strava/Garmin — it sits on top of those tools, consumes workout results, and manages the training plan. Web app first, native later.

**I need you to pressure-test this thesis.** Tell me if I'm wrong. Tell me if something like this already exists and I just haven't found it.

### What I need researched

**1. Existing AI running coaches and adaptive training tools**

For each significant product in this space, I need to understand:
- What does it actually do? (Not marketing claims — what is the real user experience?)
- How "adaptive" is it really? Does it adjust based on individual performance data, or is it just re-running a template algorithm?
- What's the AI/ML approach? Is it a real LLM-based conversational agent, a rule-based system, or a statistical model?
- What do users actually say about it? (Look at App Store reviews, Reddit threads, running forums like r/running, LetsRun, Strava community posts, etc.)
- What are the most common complaints?
- What's the pricing model?

Products to investigate (at minimum — add others you find):
- **Runna** — recently raised significant funding, claims AI coaching
- **TrainAsONE** — supposedly uses ML for adaptive plans
- **COROS Training Hub / EvoLab** — built into COROS watches
- **Garmin Coach** — free with Garmin watches
- **Nike Run Club guided plans** — now part of Nike app
- **Humango** — AI endurance coaching
- **Final Surge** — coaching platform with some AI features
- **ChatGPT/Claude used directly** — power users manually prompting AI for coaching (this is the "unbundled" version of what I'm building)

**2. The "why doesn't this exist" question**

If a truly adaptive, LLM-powered running coach doesn't already exist as a product, why not? Possible explanations to investigate:
- Is the market too small? What's the addressable market for running-specific coaching tools?
- Is it a technology timing issue? (LLMs only recently became capable enough)
- Is it a unit economics problem? (API costs per user too high for the price runners will pay)
- Is there a liability/safety barrier? (AI giving training advice that could cause injury)
- Is it a data moat problem? (Garmin/Apple/Strava own the data and a startup can't access it easily)
- Is it a "good enough" problem? (Template plans work fine for most runners)
- Something else I'm not seeing?

**3. User pain points and unmet needs**

Beyond what specific products do wrong, what are runners actually frustrated about in their training tool experience? Look at:
- Reddit threads (r/running, r/AdvancedRunning, r/C25K, r/marathontraining)
- Running forum discussions about training plans and coaching
- App store reviews for the products above
- Blog posts or articles about AI in running/fitness

I'm specifically looking for signals that validate (or invalidate) the problem I'm trying to solve: that runners want something more adaptive and personalized than what exists, but don't want to pay for a human coach.

**4. Competitive positioning map**

Based on the research, create a positioning map along two axes:
- X axis: Static/Template ← → Truly Adaptive
- Y axis: Tool/Utility ← → Coaching Relationship

Place each product on this map and identify where the whitespace is.

### What "good" looks like

Be ruthlessly honest. I'd rather learn that this product already exists and I should do something else than get a cheerful "there's a huge gap!" that's wrong. Cite specific sources — user reviews, forum posts, articles — don't just summarize marketing pages. When you find a product that seems close to what I'm describing, dig deep into what it actually does vs. what it claims.

End with a **"So What"** section: given everything you found, is there a real opportunity here? What would you build differently (or not build) based on this research? What's the strongest argument *against* building this?

### Output format

Structure as:
1. Product-by-product analysis (the landscape)
2. Why this doesn't exist yet (the barriers)
3. User pain points (the demand signal)
4. Positioning map (the whitespace)
5. So What (the honest assessment)
