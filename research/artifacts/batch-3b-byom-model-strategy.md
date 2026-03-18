# BYOM is a distraction — absorb the cost and ship

**At $0.04–$1.00/month per user in LLM costs, Bring Your Own Model solves a problem your running coach doesn't have.** The engineering complexity, regulatory risk, and user friction of BYOM far exceed the marginal cost savings. Every AI fitness product on the market — Runna ($20/month), WHOOP ($24–30/month), TrainAsONE ($12/month) — absorbs AI costs into subscription pricing with gross margins above 90% on the inference component. The practical path forward is a thin abstraction layer optimized for one provider, with a subscription model that treats LLM costs as a rounding error.

This analysis draws from implementation patterns across Cursor, TypingMind, OpenRouter, Continue.dev, Cody, and LibreChat; evaluations of LiteLLM, Vercel AI SDK, Portkey, and LangChain; pricing data from seven providers; and case studies from companies that learned provider dependency the hard way.

---

## 1. The BYOM landscape: who does it, how, and why it doesn't apply here

### Implementation patterns across the industry

BYOM implementations cluster into three architectural models. **Client-side direct** (TypingMind, BetterChatGPT, Brave Leo) stores API keys in browser localStorage or IndexedDB and routes requests directly from the client to the provider — maximum privacy, but vulnerable to browser extension key extraction and XSS attacks. **Server-side proxy** (Cursor, OpenRouter, LibreChat) transmits the user's key to the product's server with each request for prompt building and analytics — enables advanced features but creates a trust surface. **OS-level secure storage** (BoltAI using Apple Keychain) provides hardware-backed security but is platform-specific.

The UX follows a predictable pattern: a settings page with per-provider API key fields, a "Verify" button for real-time validation, and a model selector dropdown. Cursor routes all requests through its backend even with user-provided keys (despite claiming keys aren't "stored"), while TypingMind sends OpenAI requests directly from the browser but proxies Anthropic requests due to CORS restrictions. Continue.dev uses YAML config files with environment variable references — a developer-native approach that would be alien to runners.

### Adoption data is thin, but the signal is clear

**No product has published specific BYOM adoption percentages** — this is closely guarded business data. What's observable: products where BYOM is the *only* mode (TypingMind, BetterChatGPT, Continue.dev, Jan.ai) serve exclusively developer audiences. Products serving broader users (Cursor, JetBrains, Warp) offer BYOM as an optional power-user feature alongside their subscription. The market has converged on **"subscription-first with optional BYOK"** as the standard pattern.

TypingMind — the poster child of BYOK monetization — charges a $39 one-time license for the UI and requires users to bring their own API keys. It reportedly exceeded $1M ARR. But its audience is developers comfortable managing API accounts and understanding token billing. Community complaints center on unpredictable costs, key loss when browser cache clears, and API key management fatigue across providers.

### FTC Health Breach Notification Rule creates real exposure

The updated FTC Health Breach Notification Rule (effective July 29, 2024) applies to vendors of personal health records not covered by HIPAA — which almost certainly includes an AI coaching product collecting training history, fatigue data, and injury mentions. **BYOM does not create a safe harbor.** The rule focuses on whether the entity "maintains" PHR identifiable health information, not on who holds the API key. The product company designed the data flow and collected the information.

BYOM may actually *increase* regulatory complexity: with user-provided keys, the product company has less visibility into and control over downstream data handling. If a user's OpenAI account retains training data, that creates a data handling chain the company may have exposure for. The FTC's enforcement actions against GoodRx ($1.5M settlement) and Easy Healthcare demonstrate aggressive expansion of the rule's scope, with penalties up to **$50,120 per violation**. Clear, affirmative disclosure that health data will flow to an LLM provider is essential regardless of architecture — but BYOM makes the compliance story harder, not easier.

---

## 2. Multi-model support costs more than the API format difference suggests

### Abstraction layers: what actually works

Four abstraction layers merit serious evaluation, and one doesn't.

**LiteLLM** is the most comprehensive option: 100+ providers behind a unified OpenAI-compatible interface, with **~500µs mean latency overhead** — trivial for coaching calls measured in seconds. It handles Anthropic prompt caching (passing through `cache_control` directives and auto-injecting cache breakpoints), OpenAI structured outputs, and Google context caching. The catch: production deployment requires PostgreSQL and Redis, memory can reach 8GB+ under load, and multiple teams report degradation above 300 RPS. For a coaching app at <100 RPS, these limitations are irrelevant. Best used as an SDK (Python import), not as a standalone proxy.

**Vercel AI SDK** offers the best developer experience for TypeScript/Next.js apps: first-class streaming via React hooks, type-safe tool calling, and a provider-agnostic model specification. **2M+ weekly downloads** and production use at Perplexity validate its maturity. Hard constraint: TypeScript/JavaScript only. It lacks built-in cost tracking and observability, requiring third-party tools.

**Portkey** takes a gateway approach with **<1ms latency overhead**, built-in observability, guardrails (PII redaction, jailbreak detection), and cost tracking. The open-source gateway (10.2K GitHub stars) can be self-hosted on Cloudflare Workers. Production tier starts at ~$49/month — worth it at scale, overkill for MVP.

**LangChain is overkill** for LLM abstraction. The developer community is vocal: Octomind found "developers were spending as much time deciphering and debugging framework code as building features." Multiple sources describe "5 layers of abstraction just to change a minute detail." For a conversational coaching app that needs a simple `complete(messages) → response` interface, LangChain's agent frameworks, chains, and memory systems add unnecessary complexity.

### The real cost isn't API format — it's prompt behavior

The meaningful differences between providers for a coaching product aren't syntactic (adapter patterns solve that in hours) but behavioral. **Claude prompts achieve results in ~1,100 lines that GPT prompts accomplish in ~300 lines** — Claude needs enforcement mechanisms and explicit checklists where GPT responds to stated principles. Structured output reliability varies: GPT-4o follows JSON schemas exactly, while Claude occasionally prepends prose ("Here's the data you requested:") that breaks parsers. Safety behaviors diverge on health/exercise advice, injury discussions, and mental health topics — each requiring model-specific testing.

Prompt caching economics differ substantially. **Anthropic offers 90% read discounts** but charges 25% extra for cache writes (5-minute TTL) or 100% extra (1-hour TTL). **OpenAI's caching is automatic with 50% read discounts** and no write premium. Google's context caching offers 75% read discounts but adds per-hour storage charges. For your stable 6.3K-token prefix with 2–5 daily calls, Anthropic's 1-hour TTL is the clear winner — the 90% discount on reads vastly outweighs write costs at your call frequency.

**An estimated 70–80% of prompt engineering investment transfers across models.** The remaining 20–30% is model-specific formatting, safety calibration, and output parsing. For a coaching product where tone, empathy, and domain expertise drive user experience, Claude's conversational quality represents a meaningful advantage — it "reads more like a person wrote it" versus GPT which "reads like copy."

### Switching costs are real but manageable

Migrating between providers with an abstraction layer takes **1–2 weeks for basic functionality, 4–8 weeks for production quality** including prompt re-optimization, eval suite updates, and gradual rollout. For coaching-specific concerns (tone, safety around health topics), add 2–4 weeks for persona validation. Enterprise migration costs average $315K per project — but for a side project with a single prompt set, the cost is engineering time, not dollars.

Architectural decisions that minimize switching costs: prompts in config files (not code), structured output validation via Pydantic/Zod independent of provider, an eval suite testing behavior across models, and provider-specific features isolated behind interfaces. Decisions that increase switching costs: fine-tuning, deep use of provider-specific features (Assistants API, extended thinking), and hardcoded prompt formatting.

---

## 3. Provider risk is real but the mitigation is simple

### The landscape is volatile — prices drop, models regress, APIs break

The LLM provider landscape has demonstrated concrete instability across every dimension. **Pricing dropped ~80% industry-wide from early 2025 to early 2026**, with DeepSeek's entry undercutting established providers by 90%. OpenAI's GPT-4o launched at 50% below GPT-4 Turbo; Anthropic's Opus 4.5 dropped 67% from Opus 4.1. This is good news for costs but makes long-term pricing assumptions unreliable.

Model quality regressions are documented and significant. A Stanford/UC Berkeley study found **GPT-4's prime number identification accuracy dropped from 97.6% to 2.4%** between March and June 2023. Code generation executability fell from 52% to 10%. Users widely reported GPT-5 series losing GPT-4o's "conversational warmth," with creative writing benchmarks dropping from 97.3% to 36.8%.

API deprecations move fast. **OpenAI deprecated GPT-4.5 after just 4 months**, forcing migration to GPT-4.1. The entire Assistants API was deprecated in August 2025 with a one-year sunset. GPT-4o was retired from ChatGPT in February 2026 with only two weeks' notice. Google deprecated Gemini 2.0 Flash with migration to 2.5 series. Anthropic has generally provided longer support windows but isn't immune.

### Case studies: what happens when you're tightly coupled

**Jasper AI** built entirely on GPT-3. When ChatGPT launched offering similar capabilities for $20/month versus Jasper's $59+, the company's business model was directly threatened. Survival required pivoting to multi-model support and adding proprietary value (brand voice tools, SEO, analytics). The lesson: being a "UI wrapper on a single provider" is existentially risky when that provider ships a consumer product.

**Builder.ai** — valued at $1.3B, backed by Microsoft — collapsed entirely. NexGen Manufacturing spent **$315,000 migrating 40 AI workflows**, consuming three months of engineering time. Their CTO's response: "Complete architectural overhaul, with every new AI integration now routed through a provider-agnostic gateway."

### Updated cost reality for your usage pattern

Your original $0.04–$1.00/month estimate corresponds to **Claude Haiku 3, which is now a legacy model**. Current-generation costs for your usage pattern (3.5 calls/day average, 12K input + 3K output tokens per call, 6.3K cached prefix):

| Model | Monthly/user (with caching) | Quality tier |
|-------|---------------------------|-------------|
| GPT-4o-mini | ~$0.33 | Budget |
| Gemini 2.5 Flash-Lite | ~$0.25 | Cheapest |
| Mistral Small 3.1 | ~$0.22 | Budget |
| Groq Llama 4 Scout | ~$0.25 | Good |
| Claude Haiku 4.5 | ~$2.46 | Strong |
| GPT-4.1 mini | ~$0.87 | Budget+ |
| Gemini 2.5 Flash | ~$0.99 | Good |
| Claude Sonnet 4.5 | ~$7.60 | Frontier |

**The sweet spot for coaching quality at reasonable cost is Claude Haiku 4.5 at ~$2.50/month/user or Gemini 2.5 Flash at ~$1.00/month/user.** Both are well below the threshold where BYOM becomes economically rational.

### Minimum viable abstraction: the adapter pattern

The 80/20 approach requires exactly one architectural decision: **route all LLM calls through a single interface.** Define `complete(messages, config) → response`, implement one adapter per provider, and select models via configuration. LiteLLM does this out of the box — replacing an OpenAI import with LiteLLM and changing one model string takes about two hours. This gets you 80% of lock-in protection with near-zero engineering cost.

Level 2 (90% protection): store prompts in separate template files, version them per model if needed, and build 10–20 behavioral test cases that validate coaching responses across providers. Level 3 (production-grade): deploy an LLM gateway for cost tracking, rate limiting, automatic failover, and A/B testing models — defer this until you have thousands of users.

---

## 4. Monetization: subscription absorbing costs wins at this price point

### BYOM economics don't work for a running coach

At your cost structure, BYOM is economically counterproductive for three reasons. First, **users would pay *more* with their own keys** — they lose your prompt caching optimizations (saving ~$2.70 per million tokens) and pay retail API rates. A Claude Sonnet call that costs you $0.07 with caching costs a BYOK user $0.10+ without it. Second, the friction of requiring runners to create API accounts, manage keys, and understand token billing is a massive adoption barrier — TypingMind works because its users are developers. Third, at sub-$3/month LLM costs, the engineering time for BYOM implementation (key management, security, multi-provider testing, compliance documentation) far exceeds years of per-user API costs.

**No AI fitness product offers BYOM.** WHOOP absorbs GPT-4 costs in its $24–30/month subscription. Enduco uses OpenAI for its Coach Squad chat feature and includes it free (with limits). The running coach market simply doesn't have users who expect or want to bring their own model.

### What works: flat subscription with LLM costs as a rounding error

AI fitness products cluster in the **$10–30/month range**: Runna at $20/month, TrainAsONE at $12/month, TrainerRoad at $25/month, PKRS.AI at $30/month (AI-only), WHOOP at $24–30/month. Annual discounts of 40–50% are universal. Free trials of 7–21 days are standard.

At a $10–15/month subscription with Claude Haiku 4.5 costs of ~$2.50/user/month, **gross margin on the AI component is 75–83%**. With a budget model like Gemini 2.5 Flash at ~$1.00/user/month, margin exceeds 90%. These are traditional SaaS-level margins. The product's value — and what users pay for — is coaching intelligence, plan adaptation, and convenience, not raw LLM access.

The most effective model gating strategy for an MVP: a **free tier with limited AI coaching messages** (5–10/month using a cheap model like GPT-4o-mini at $0.33/user/month) to demonstrate value, then a paid tier ($10–15/month) with unlimited coaching conversations using a higher-quality model. This mirrors what ChatGPT, Cursor, and Perplexity do. A "reverse trial" — full access for 14 days, then downgrade — produces better conversion than a permanent free tier.

---

## 5. Recommended architecture staged by maturity

### MVP (now): optimize for one, abstract for safety

**Primary model**: Claude Haiku 4.5. Best cost-quality tradeoff for coaching conversation at ~$2.50/user/month. Claude's strength in empathetic, conversational tone is a meaningful advantage for a coaching product.

**Abstraction layer**: If TypeScript/Next.js, use Vercel AI SDK. If Python, use LiteLLM as an SDK import (not proxy). Either adds near-zero latency and provides the adapter pattern for future provider switching. Total integration time: ~2 hours.

**Prompt architecture**: Store the 6.3K-token stable coaching prefix in a versioned config file, not in code. Use Anthropic's explicit prompt caching with 1-hour TTL — at 2–5 calls/day, the 90% read discount far outweighs the 2x write cost. Structure prompts using Claude's preferred XML tags for maximum compliance.

**What to skip entirely**: BYOM, multi-model prompt optimization, LLM gateways, usage-based pricing, credits systems. None of these serve a side-project MVP with sub-$3/user/month costs.

**Monetization**: Free tier (5–10 AI messages/month on a budget model) + paid tier ($12–15/month, unlimited coaching on Haiku 4.5). Annual plan at $99/year. 14-day reverse trial.

### Growth (hundreds of users): add fallback and eval

**Fallback model**: Test GPT-4.1 mini or Gemini 2.5 Flash with your existing prompts. Accept minor quality differences. Configure automatic failover in your abstraction layer for Anthropic outages.

**Eval suite**: Build 20–30 behavioral test cases covering coaching scenarios (injury discussion safety, pace adjustment reasoning, motivational tone). Run these against primary and fallback models monthly and after model updates. This is your early warning system for quality regression — the GPT-4 degradation was caught by exactly this kind of testing.

**Cost monitoring**: Add basic per-user cost tracking. At scale, identify heavy users who might need rate limiting or routing to a cheaper model for simple queries.

### Scale (thousands of users): gateway and model routing

**LLM gateway**: Deploy Portkey's open-source gateway or LiteLLM proxy for centralized cost tracking, rate limiting, observability, and A/B testing. This is when the operational overhead justifies itself.

**Model routing**: Route simple queries (greetings, FAQ-type questions) to a budget model (GPT-4o-mini, Gemini Flash-Lite). Route complex coaching interactions (adaptation reasoning, injury response) to the primary model. This can reduce average per-user costs by 30–50%.

**Revisit BYOM only if**: your user base turns out to be unusually technical (developers who run), or LLM costs increase dramatically due to model upgrades, or a significant user segment explicitly requests it. Even then, implement it as a power-user option, not a core architecture.

## Conclusion

The core insight is economic: **when LLM costs are $1–3/month per user and subscriptions are $10–15/month, architectural decisions should optimize for coaching quality and development speed, not cost avoidance.** BYOM solves a cost problem that doesn't exist at this scale while creating security, compliance, and UX problems that do. The minimum viable abstraction — a thin adapter layer with prompts in config — provides sufficient protection against provider lock-in at near-zero engineering cost. The real risks are model quality regression and API deprecation, both mitigated by an eval suite and a tested fallback model, not by BYOM infrastructure. Ship the coaching product. Defer the platform engineering.