# Research Queue

Topics to hand off to a deep research agent. Each entry captures the question, why it matters, and where findings should feed back into the planning docs. Full research artifacts live in `research/artifacts/` — only outcomes and key takeaways get pulled into the main planning documents.

## How to Use

1. Add a topic below with a clear research question and context on why it matters.
2. Hand off to a deep research agent.
3. Store the full output in `artifacts/` (e.g., `artifacts/competitive-landscape.md`).
4. Summarize key takeaways back into the relevant planning doc and update status here.

---

## Priority Ordering

Research is batched by dependency. Later batches depend on findings from earlier ones.

- **Batch 1 (Kick off now — unblocks everything else):**
  R-008 + R-009 (dev tooling & workflow) — shapes how you run every other research task and POC.
  R-002 (competitive landscape) — validates the problem space before investing in solutions.

- **Batch 2 (Kick off after Batch 1, or in parallel if capacity allows):**
  R-001 + R-004 (training methodologies + planning architectures) — directly feeds POC 1 and POC 3.
  R-007 (testing non-deterministic outputs) — needed before POC work so you can evaluate results.

- **Batch 3 (Important but not blocking POCs):**
  R-003 (safety/liability), R-005 (BYOM + model strategy), R-006 (wearable integrations).

- **Batch 4 (Discovered topics — feeds prompt engineering and safety testing):**
  R-010 (coaching conversation design), R-011 (special populations safety boundaries).

---

## Queue

| # | Topic | Research Question | Why It Matters | Status | Artifact |
|---|-------|-------------------|----------------|--------|----------|
| R-001 | Training methodologies as configurable frameworks | What are the major running training methodologies (Daniels, Hanson, Pfitzinger, 80/20, etc.), how do they differ in structure and philosophy, and how could an AI select and blend between them based on user profile and feedback? | Core to the product — the AI needs a principled basis for plan generation, and users benefit from methodology choice. Also need to understand where LLMs are strong vs. weak on training science. | Integrated | batch-2a-training-methodologies.md |
| R-002 | Competitive landscape & market opportunity | What do existing AI running coaches and adaptive training tools (TrainAsONE, COROS coach, Garmin Coach, Runna, etc.) actually do? Where do they fall short? What do users complain about? Does a truly adaptive AI coaching product already exist? If not, why — is it a market gap or is there a structural reason it hasn't been built? | Validates the problem space, identifies differentiation opportunities, and answers the fundamental "why doesn't this exist yet" question. Could reveal barriers we haven't considered. | Integrated | batch-1b-competitive-landscape.md |
| R-003 | Safety, liability, and legal guardrails | What are the real risks of AI-generated training plans (overtraining, injury, cardiac events, etc.)? What is the legal liability if a user is harmed following AI-generated advice? How do existing fitness apps handle this (disclaimers, waivers, limits on advice)? Given that users can already get similar advice from raw AI chat, what is the incremental liability of packaging it as a product? | Critical for understanding worst-case exposure. Directly informs what guardrails are legally necessary vs. nice-to-have, and whether the product needs medical disclaimers, age restrictions, or health screening gates. | Integrated | batch-3a-safety-liability.md |
| R-004 | AI-driven planning architectures and tiered plan models | What design patterns exist for AI systems that manage multi-layered, evolving plans? How do other AI-first products (not just running) handle the tension between structured plan state and flexible AI output? Is macro/meso/micro the right decomposition, or are there better models? | The three-tier model is a core assumption. Need to validate it against real-world patterns before it becomes load-bearing in the data model. Directly feeds into POC 3. | Integrated | batch-2b-planning-architecture.md |
| R-005 | BYOM (Bring Your Own Model) and model dependency strategy | Is it feasible to let users bring their own API key (OpenAI, Anthropic, etc.) for unlimited querying, alongside a default in-house model with usage limits? What are the UX, security, and architectural implications? How do other AI-first products handle this (e.g., Cursor, Typingmind, OpenRouter)? Additionally: should the product be model-agnostic or optimized for one provider? What's the cost of supporting multiple models (prompt portability, behavior differences, testing overhead)? | Directly shapes the monetization model and architecture. BYOM could eliminate the cost barrier for power users, but adds complexity. Model-agnostic design provides resilience against provider changes but costs more upfront. These questions are intertwined and should be researched together. | Integrated | batch-3b-byom-model-strategy.md |
| R-006 | Wearable & platform integration feasibility | What does integration with Garmin Connect, Apple Health, Strava, and similar platforms actually require? What APIs are available, what are the approval processes, rate limits, and data access restrictions? Which platforms are easiest to integrate first? | The app's value increases dramatically with automatic data ingestion. Need to understand the real effort and constraints for each platform before committing to an integration roadmap. | Integrated | batch-3c-wearable-integrations.md |
| R-007 | Testing non-deterministic AI outputs | How do you evaluate the quality of AI-generated training plans when outputs are non-deterministic? What scoring criteria, rubrics, or automated evaluation frameworks exist for assessing plan quality? How can you set thresholds for "good enough" coaching output? | Essential for both development confidence and ongoing quality assurance. Without a way to measure output quality, you can't tell if prompt changes or model updates are making things better or worse. Feeds directly into POC work. | Integrated | batch-2c-testing-nondeterministic.md |
| R-008 | Claude Code best practices, plugins, and agent frameworks | What are the current best practices for building a full product with Claude Code? Investigate "everything claude code," "awesome-claude-code," and similar community resources. What skills, plugins, agent team patterns, and frameworks exist? How to maximize AI-assisted development — multi-agent workflows, custom skills, MCP servers, etc. Focus on what's available for personal/open-source use (not proprietary). | This is the development methodology for the entire project. Getting the tooling right early compounds across every phase of building. Want to leverage the cutting edge of what's possible with Claude Code rather than using it as a basic coding assistant. | Integrated | batch-1-claude-code-workflow.md |
| R-009 | AI-assisted side project workflow and context management | How do solo builders / side project developers organize AI-assisted development across project phases? Specifically: (1) How to maintain a clean separation between planning/ideation docs and active codebase so ongoing ideas don't pollute the build. (2) How to hand off rich context from one phase to the next (e.g., planning → POC → MVP → post-MVP) so each new agent session doesn't start cold. (3) What repo structures, CLAUDE.md patterns, multi-workspace setups, or project scaffolding approaches exist for this? (4) How to run autonomous agents effectively on a side project — what guardrails and structures produce good outcomes without constant supervision? Look at how the Claude Code community handles this, what conventions are emerging, and whether existing frameworks (agent-stack, claude-flow, etc.) solve parts of this. | This is the operational problem for the entire project. Building is a side project with limited time — need to maximize what autonomous agents can accomplish per session. The planning docs we've built are the first layer, but need a clear bridge from "organized planning docs" to "agent can pick this up and build the next phase." Without this, each session starts from scratch and planning/ideation work gets tangled with implementation. | Integrated | batch-1-claude-code-workflow.md |

| R-010 | Coaching conversation design & behavioral psychology | How do effective human running coaches communicate plan changes, setbacks, and corrections? What does sports psychology research say about motivation, adherence, and tone in coaching relationships? How should an AI coach handle common behavioral challenges: users who run too hard on easy days, users who are demoralized by a downgrade, users who ignore advice? What conversational patterns build trust vs. erode it? | R-001 and R-004 defined what the AI adapts — but not how it talks. The coaching persona prompt is the user-facing product. Getting the tone wrong (too clinical, too cheerful under injury, too scolding on over-performance) directly undermines trust and retention. This feeds prompt engineering for the coaching layer. | Integrated | batch-4a-coaching-conversation-design.md |
| R-011 | Special populations & safety edge cases | What are the safety boundaries and coaching considerations for populations the AI will encounter but isn't specifically designed for? Specifically: pregnancy/postpartum running, menstrual cycle effects on training, masters runners (age-related adaptation), juvenile runners, runners with chronic conditions (asthma, type 1/2 diabetes, cardiac arrhythmias), and return-to-run protocols after common injuries (plantar fasciitis, IT band, stress fractures, Achilles tendinopathy). For each: what should the AI know, what should it refuse to advise on, and what should trigger a "see a professional" referral? | The adversarial test library (DEC-016) needs comprehensive safety scenarios. Without researching these populations, the AI might give dangerous advice to someone who mentions they're pregnant, diabetic, or recovering from a stress fracture. This isn't about building features for these groups — it's about knowing the boundaries so the AI stays safe. Directly feeds the safety scenario library and system prompt guardrails. | Integrated | batch-4b-special-populations-safety.md |

| R-012 | AI-powered PR review and code quality tooling for AI-assisted development | What are the current AI-powered PR review tools (CodeRabbit, Anthropic's Claude Code Review GitHub Action, Codacy, Qodo, etc.)? How do they compare for a solo developer whose code is primarily AI-generated? What pre-commit hook ecosystems, static analysis tools, and CI quality gates best complement AI code generation? How should the full quality pipeline be designed when the "author" is an AI agent? | The project uses Claude Code as the primary development tool. All code is AI-generated and needs deterministic quality enforcement at multiple layers (edit-time hooks, pre-commit, PR review, CI). AI-reviewing-AI is a different use case than AI-reviewing-human — the failure modes and value proposition differ. This directly feeds the quality gate pipeline design (DEC-034) and Claude Code hook configuration. | Integrated | batch-5-ai-pr-review-quality-tool.md |

| R-013 | LLM eval test strategies for non-deterministic output assertion | How do teams structure eval suites for non-deterministic LLM outputs? What are the best practices for structured output enforcement, LLM-as-judge patterns, tiered assertion strategies (deterministic → NLI → judge), and statistical approaches to flaky eval tests? | POC 1 eval tests use brittle string matching and JSON key guessing. Need a principled testing architecture before building out the full eval suite. Directly informs the LLM testing refactor on PR #17. | Integrated | batch-6a-llm-eval-strategies.md |
| R-014 | .NET libraries for LLM response caching, HTTP recording/replay, and eval tooling | What .NET libraries exist for HTTP recording/replay (VCR pattern), LLM-specific evaluation (Microsoft.Extensions.AI.Evaluation), structured output schema generation, snapshot testing, and test parallelization with rate limits? | Need concrete library choices for the .NET eval test infrastructure. Discovered Microsoft.Extensions.AI.Evaluation as the primary .NET eval framework. Directly informs DEC-036. | Integrated | batch-6b-dotnet-llm-testing-tooling.md |
| R-015 | IChatClient bridge gap for Anthropic structured outputs | The Anthropic SDK's IChatClient bridge silently drops ChatResponseFormat.ForJsonSchema(). How to bridge structured output through the M.E.AI pipeline? What's the DelegatingChatClient pattern? | Discovered during POC 1 eval implementation. Without the bridge, structured output calls return free-form JSON. Feeds DEC-037. | Integrated | batch-7a-ichatclient-structured-output-bridge.md |
| R-016 | Anthropic model IDs, versioning strategy, and structured output compatibility | Which models support structured output? How do floating alias IDs work? What's the recommended config pattern to avoid hardcoded model IDs? | Model ID confusion blocked eval tests. Old Sonnet 4 doesn't support structured output. Feeds DEC-037 and DEC-038. | Integrated | batch-7b-anthropic-model-ids-versioning.md |

### Batch 5 (Development infrastructure — feeds repo setup and CI/CD)

- **Batch 5:** R-012 (AI PR review and code quality tooling) — needed before repo scaffolding so the quality pipeline is designed into the project from the start.

### Batch 6 (LLM testing architecture — feeds POC 1 eval refactor)

- **Batch 6:** R-013 + R-014 (LLM eval strategies and .NET tooling) — directly informs POC 1 eval suite refactor. R-013 covers eval strategies and assertion patterns. R-014 covers .NET library choices. Together they feed DEC-036.

### Batch 7 (Implementation discoveries — feeds POC 1 eval debugging and model strategy)

- **Batch 7:** R-015 + R-016 (IChatClient bridge gap and model ID versioning) — discovered during POC 1 eval implementation. R-015 identified that the Anthropic SDK's IChatClient bridge silently drops structured output schemas. R-016 resolved model ID confusion and established versioning strategy. Together they feed DEC-037 and DEC-038.

### Status Key

- **Queued** — Ready to hand off
- **In Progress** — Research agent working on it
- **Done** — Artifact complete, needs integration
- **Integrated** — Takeaways pulled into planning docs
