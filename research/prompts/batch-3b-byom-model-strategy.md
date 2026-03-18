# Research Prompt: BYOM (Bring Your Own Model) and Model Dependency Strategy

## What I Need

A practical analysis of whether and how to support Bring Your Own Model (BYOM) in an AI-powered running coach application, alongside the broader question of model dependency strategy. I need concrete architectural guidance, not theoretical discussion.

## Context About the Product

I'm building an AI running coach that uses LLMs for coaching conversation, plan explanation, and adaptation reasoning — but NOT for generating raw training plans or pace calculations. The architecture has a hard separation:

- **Deterministic computation layer**: Safety guardrails, pace calculations, load monitoring, plan math. This is code, not LLM.
- **LLM coaching layer**: Natural language conversation, explaining plan changes, reasoning about adaptation, user interaction. This is where model choice matters.

The LLM layer uses a context injection strategy (~15K tokens per call, 7.5% of a 200K window) with a stable prefix (~6.3K tokens) optimized for prompt caching (90% cost reduction). The system uses interaction-specific context assembly — different calls get different context slices.

Current cost estimates from competitive research: $0.04–$1.00/month per user at 2-5 interactions/week. Prompt caching on the stable prefix saves ~$2.70 per million tokens.

The product is a side project moving toward MVP. I want to avoid over-engineering but also avoid painting myself into a corner on model dependency.

## Specific Questions

### 1. BYOM Feasibility and Patterns
- How do existing AI products implement BYOM? Specifically examine: Cursor, TypingMind, OpenRouter, Bolt.new, Cody, Continue.dev, LibreChat, and any other relevant examples.
- What's the actual UX for BYOM? How do users provide API keys? How is key storage handled securely?
- What are the security implications of routing user data through user-provided API keys? (Context: the app sends health-adjacent data — training history, fatigue reports, injury mentions — through the LLM. R-003 research confirmed FTC Health Breach Notification Rule applies.)
- What's the realistic user adoption rate for BYOM? What percentage of users actually bring their own keys vs. using the default?
- Does BYOM actually solve the cost problem, or does it just shift it to user friction?

### 2. Model-Agnostic vs. Model-Optimized Architecture
- What's the real cost of supporting multiple LLM providers? Not just API format differences (OpenAI vs. Anthropic vs. Google) but: prompt behavior differences, safety behavior differences, structured output reliability, context window handling, prompt caching availability.
- How do products that support multiple models handle prompt portability? Do they maintain separate prompt sets per model, use an abstraction layer, or accept degraded quality on non-primary models?
- What abstraction layers exist? Evaluate: LiteLLM, Vercel AI SDK, LangChain, Portkey, and any others. What are the real trade-offs (latency overhead, feature support gaps, maintenance burden)?
- For a coaching product where tone, safety behavior, and domain expertise matter: is model-agnostic design realistic, or does optimizing for one model produce meaningfully better results?
- What's the switching cost if you optimize for one provider and need to migrate later?

### 3. Provider Lock-in Risk Assessment
- How volatile is the LLM provider landscape? What's the realistic risk of a provider: dramatically changing pricing, degrading model quality, changing ToS to be hostile, shutting down API access?
- What happened to products that were tightly coupled to a single provider when that provider made breaking changes? Any case studies?
- What's the minimum viable abstraction to protect against provider lock-in without over-engineering?
- How do pricing models differ across providers for the usage pattern described (2-5 calls/day per user, ~15K tokens per call, heavy prompt caching)?

### 4. Monetization Implications
- Given that LLM costs are $0.04–$1.00/month per user: does a BYOM tier even make economic sense, or is the cost low enough to just include it?
- How do AI products actually monetize around model costs? Examine: flat subscription absorbing API costs, usage-based pricing, BYOM as premium tier, freemium with model quality as the gate.
- What pricing models have worked for AI fitness/coaching products specifically?
- If BYOM is offered: what's the right default model tier for non-BYOM users? (e.g., a cheaper/smaller model with usage limits, vs. the same model with rate limits)

### 5. Practical Architecture Recommendations
- For an MVP side project: what's the simplest architecture that avoids provider lock-in without BYOM complexity?
- If BYOM is worth doing: what's the minimum viable implementation? What can be deferred?
- How should the system handle model-specific features (prompt caching is Anthropic-specific, structured outputs differ by provider, safety behaviors vary)?
- What does the API key management look like? Client-side storage vs. server-side encrypted storage? What are the security requirements?

## What I DON'T Need
- Generic "pros and cons of BYOM" lists
- Theoretical discussion of LLM capabilities
- Comparisons of model quality (I can evaluate that myself)
- Recommendations about which model is "best" — I need architecture guidance, not model reviews

## Output Format

Structure the findings as:
1. **BYOM landscape** — what exists, how it works, adoption rates, security patterns
2. **Model abstraction reality** — real cost of multi-model support, abstraction layer evaluation, prompt portability challenges
3. **Provider risk assessment** — lock-in risk, mitigation strategies, case studies
4. **Monetization models** — what works for AI products at this cost structure
5. **Recommended architecture** — specific recommendation for this product staged by maturity (MVP → scale), with rationale for what to build now vs. defer
