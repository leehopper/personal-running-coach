# Research Prompt: Batch 17a — R-051

# LLM Observability for a .NET 10 + Anthropic Stack — Langfuse vs LangSmith vs Custom OTel (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For an ASP.NET Core 10 application that calls Anthropic's Messages API through a thin `ICoachingLlm` adapter and a `ContextAssembler` prompt-composition primitive, what is the current 2026 best-practice approach to LLM-specific observability — per-call traces, token-cost tracking, prompt-cache hit rates, prompt-version diffing, eval-drift dashboards — and which combination of tools fits a same-Postgres single-instance MVP-0 with a public-beta trajectory?

## Context

I'm preparing Slice 1 (Onboarding → Plan) of MVP-0 for an AI-powered running coach (RunCoach). Slice 0 (`docs/specs/12-spec-slice-0-foundation/`) just landed with a Compose-based OpenTelemetry overlay (per R-050 / DEC-045): `docker-compose.otel.yml` runs an OpenTelemetry Collector + Jaeger; Marten's `OpenTelemetry.TrackConnections` and `TrackEventCounters` are wired; `"Marten"` / `"Wolverine"` / `"RunCoach.Llm"` `ActivitySource` and `Meter` sources are registered.

That gives raw infrastructure traces. **It does not give LLM-specific observability** — per-call token cost, prompt-version tracking, prompt-cache hit rate, structured-output extraction success rate, eval-drift over time, "what exactly did Claude see and return for user X's turn 3 onboarding turn?". R-048 (`docs/research/artifacts/batch-16a-onboarding-conversation-state.md`) explicitly named Langfuse / LangSmith as needed *from day one*: *"Run Langfuse (self-hosted, MIT, OTel-based) or LangSmith with `sessionId = userId`, `traceId = turnId` from day one. The event log gives you replay; the trace gives you latency, token costs, and per-turn cache-hit rates. Both are needed for the 'why did the coach give me this plan?' debugging Slice 4+ will require."* — but did not pick one.

Existing constraints:

- **Backend:** ASP.NET Core 10, EF Core 10 + Marten 8.28 + Wolverine 5.28 on Postgres. LLM calls flow through `ICoachingLlm` (existing thin adapter) and `ContextAssembler` (existing prompt-composition primitive). Anthropic structured outputs via DEC-037's `AnthropicStructuredOutputClient` bridge (parallel research R-052 may revise).
- **LLM provider:** Anthropic. Floating model aliases per DEC-037 (`claude-sonnet-4-6` for coaching, `claude-haiku-4-5` for judging).
- **Prompt caching:** enabled day one per DEC-047 with `cache_control: { type: "ephemeral", ttl: "1h" }` and a second breakpoint on the system prompt. Cache-hit-rate is a major cost driver (~70% input-token saving at MVP-0 volume per R-048).
- **Evaluation:** existing M.E.AI.Evaluation infrastructure (DEC-036) with committed replay-cache fixtures (DEC-039). Parallel research R-053 covers the multi-turn extension.
- **Privacy posture:** RunCoach is a PHR vendor under FTC HBNR (R-049 / DEC-046). Sending user-conversation data to a third-party SaaS observability provider (LangSmith, Langfuse cloud) creates a data-flow that needs a DPA before public beta. Self-hosted observability avoids the DPA entirely.
- **Trajectory:** solo-dev now → friends/testers (MVP-1, hosted) → public beta. Single-instance API for MVP-0; could go multi-replica later.
- **Existing OTel infrastructure** (per R-050 / DEC-045): Collector + Jaeger via Compose overlay; `OTEL_EXPORTER_OTLP_ENDPOINT` configurable.

Slice 1 will issue many LLM calls (onboarding turns, plan generation). Slice 3 will issue more (adaptation evaluation). Slice 4 adds open-conversation calls. Wiring the trace shape now means trace-id propagation flows naturally through the existing primitives; wiring it after Slice 1 means retrofitting every call site.

## Research Question

**Primary:** What is the current 2026 best-practice combination of tools/SDK/conventions for LLM-specific observability in a .NET 10 + Anthropic stack like RunCoach, and which specific provider — Langfuse (self-hosted), Langfuse Cloud, LangSmith, Helicone, Arize Phoenix, Opik, custom OTel + Grafana, or some combination — should land in Slice 1?

**Sub-questions (must be actionable):**

1. **Tool survey (with concrete .NET-relevant detail).** For each candidate, document:
   - Self-host vs SaaS, OSS license, hosting cost at MVP-0 / MVP-1 / public-beta volumes.
   - .NET SDK quality in 2026 — is there an idiomatic SDK, an OTel-native ingestion path, or only an HTTP API?
   - Native dashboards offered: per-call trace, token-cost rollup, prompt-version diff, cache-hit-rate breakdown, structured-output extraction success rate, eval-drift over time.
   - Anthropic-specific support: does it understand `cache_control`, cache-creation vs cache-read pricing, the `messages[]` shape with content blocks (`thinking`, `tool_use`, `tool_result`), Sonnet/Haiku model-version tagging?
   - Prompt versioning: does it diff `coaching-v1.yaml` vs `coaching-v2.yaml` runs side-by-side?

   Candidates to compare: **Langfuse self-hosted**, **Langfuse Cloud**, **LangSmith** (LangChain), **Helicone**, **Arize Phoenix** (Arize AI), **Opik** (Comet), **custom OTel + Grafana / Tempo**, **PostHog AI features** (if real in 2026).

2. **OTel-native vs proprietary wire format.** Several candidates claim "OTel-native" — is that true end-to-end (SDK emits standard OTel spans → tool ingests directly), or do they require a proprietary SDK that *also* emits OTel as a side-effect? For RunCoach's existing OTel Collector + Jaeger overlay: which candidates plug in by reconfiguring `OTEL_EXPORTER_OTLP_ENDPOINT` vs which require a parallel SDK?

3. **Trace-id propagation from HTTP through `ContextAssembler` through `ICoachingLlm`.** What's the recommended span-hierarchy shape for a multi-turn onboarding flow per DEC-047 (one HTTP request → one Wolverine `[AggregateHandler]` → one Anthropic call → typed extraction)? Does the recommended tool let us tag spans with `sessionId = userId`, `turnId`, prompt-version, model-id, cache-hit-bool natively, or do we hand-roll attributes? How does the trace-id shape interact with Wolverine's outbox-deferred LLM calls in Slice 3?

4. **Privacy / DPA posture for SaaS providers.** RunCoach is a PHR vendor under FTC HBNR. For each SaaS candidate, document: where data is processed, what data is sent (full prompt body? metadata only? configurable?), DPA terms in 2026, BAA availability if applicable, EU data-residency options. For each self-hosted candidate, document the operational cost of running it (Docker resource cost, dependencies on Postgres / ClickHouse / Redis, backup story).

5. **Recommendation matrix by phase.** What's the minimum-viable wiring for MVP-0 (just enough to debug "why did the coach give me this plan?"), and what additions land at MVP-1 (friends/testers — adds dashboards, cost rollups) and pre-public-beta (compliance posture, SLA, alerting)? Each phase: which provider, which features turned on, which trace attributes, which dashboards are load-bearing.

6. **Integration with the existing eval suite (DEC-036, DEC-039) and parallel R-053 multi-turn eval.** Several LLM observability tools double as eval frameworks (LangSmith, Phoenix, Opik). Does the recommended provider extend or replace the existing M.E.AI.Evaluation + committed-cache pattern? If extends: what's the integration point? If replaces: what's the migration scope?

7. **Cost-tracking primitives.** Anthropic emits cache-creation-tokens vs cache-read-tokens vs base input/output tokens with different prices. Does the recommended provider compute correct cost per call (Sonnet vs Haiku × cache-status × tier) or do we maintain our own cost model? Does it surface eval-suite costs separately from production-coaching costs?

8. **Prompt-version diffing and A/B.** Onboarding-v1 → onboarding-v2 prompt evolution is on the horizon. Does the tool support side-by-side diffs of "same scenario, prompt-v1 vs prompt-v2, here's the structured-output difference and the cost difference"? Critical for prompt iteration without manual log-grepping.

9. **Trace retention and storage cost.** At ~50 users × 30 turns × Sonnet 4.5 × roughly 8k input tokens/turn (R-048's MVP-0 envelope), what's the trace volume, and what's the per-month storage cost on each candidate?

10. **2026 maturity check.** This space evolved fast in 2024–2025. Confirm the current stable versions of the named tools, .NET SDK status, Anthropic-specific feature parity, OSS-vs-commercial dynamics. Any tool that was hot in 2024 but is now abandoned should be flagged.

11. **Wolverine outbox interaction.** Slice 3 adaptation flow: HTTP commits a Marten event + enqueues a Wolverine outbox message → background handler invokes the LLM. The trace must propagate from the originating HTTP request through the outbox dequeue. What's the standard pattern for cross-process trace continuation, and do the candidate tools support it natively?

12. **CritterWatch interaction.** JasperFx ships CritterWatch (Marten/Wolverine commercial observability). Does it overlap with the LLM-observability candidates, or is it strictly Marten/Wolverine and the LLM layer is separate?

## Why It Matters

- **Trace-id shape sets the LLM-call contract from Slice 1 onward.** Wired wrong now: every LLM call site retrofitted later. Wired right now: free trace continuity through Slice 4's open conversation.
- **Cost is a measurable axis from day one.** R-048's prompt-cache argument was ~70% saving — that's only verifiable with per-call cache-hit-rate tracking. Without observability, "is the cache working?" is a vibes question.
- **Prompt iteration is the primary user-visible quality lever.** Slice 1 will produce `onboarding-v1.yaml`; Slice 3 will produce `adaptation-v1.yaml`. Iterating these without a diff dashboard is grep + spreadsheet.
- **FTC HBNR pre-public-release escalation requires a clear data-flow.** A self-hosted choice removes a third-party DPA from the compliance scope. A SaaS choice adds one. Picking now lets us bake the choice into the privacy-policy work that lands pre-public-release.
- **The R-048 recommendation was "from day one."** Wiring this in Slice 0 alongside the OTel Collector overlay is materially cheaper than wiring it after Slice 1 implementation has shipped.

## Deliverables

- **A concrete recommendation** with the chosen tool, the rationale, and the explicit alternatives rejected.
- **A capability matrix** comparing all named candidates across the eight axes in sub-question 1 (self-host/SaaS, .NET SDK, OTel-native, dashboards, Anthropic-aware, prompt versioning, privacy posture, cost).
- **A phased adoption plan** — minimum-viable for MVP-0, additions at MVP-1, additions pre-public-beta — with the explicit dashboards and trace attributes for each phase.
- **A trace-shape sketch** — the recommended `ActivitySource` / `Activity` hierarchy for one onboarding turn and one Slice 3 adaptation flow, including the cross-Wolverine-outbox trace continuation.
- **A `Program.cs` wiring snippet** showing the registration alongside the existing OTel Collector setup.
- **An interaction note with R-052 (Anthropic SDK choice)** — does the recommended tool work cleanly over the first-party Anthropic SDK, the M.E.AI bridge, or both?
- **An interaction note with R-053 (multi-turn eval pattern)** — does the recommended tool replace or extend the existing M.E.AI.Evaluation pattern?
- **A cost projection** at MVP-0 / MVP-1 / public-beta volumes for the recommended choice.
- **A privacy / DPA note** appropriate for the recommended choice given the FTC HBNR escalation in DEC-046.
- **Citations** — current docs from each named provider, .NET community sources, real 2025–2026 case studies of Anthropic + .NET observability stacks.

## Out of Scope

- The training-science domain content of the prompts being observed — covered by `batch-2a-training-methodologies.md`.
- The choice of Anthropic vs another LLM provider — locked in DEC-022.
- General-purpose APM (Datadog, New Relic, AppInsights for non-LLM signals) — covered by R-050's OTel Collector + Jaeger setup.
- Test-frame observability — covered by R-053 (multi-turn eval).
- Multi-modal (audio/image) observability — RunCoach is text-only.
- Voice / mid-run logging — explicitly out of MVP-0 scope per cycle plan.
