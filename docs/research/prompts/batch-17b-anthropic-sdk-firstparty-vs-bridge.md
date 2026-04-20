# Research Prompt: Batch 17b — R-052

# Anthropic .NET SDK Choice — First-Party `Anthropic` NuGet v12.x vs DEC-037 `AnthropicStructuredOutputClient` Bridge (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For an ASP.NET Core 10 + `Microsoft.Extensions.AI` (M.E.AI) application that calls Anthropic's Messages API and depends on structured-output schemas, prompt caching, and (eventually) tool use + extended thinking — should the project migrate from its current DEC-037 `AnthropicStructuredOutputClient` custom bridge to the first-party `Anthropic` NuGet (v12.x in April 2026, which now implements `Microsoft.Extensions.AI.IChatClient`), keep the custom bridge, or run a hybrid?

## Context

I'm preparing Slice 1 (Onboarding → Plan) of MVP-0 for an AI-powered running coach (RunCoach). The next several slices add many new LLM call sites — the SDK choice affects every one of them.

**The history:** POC 1 surfaced a real bug in 2026-Q1: the `Anthropic.SDK` `IChatClient` bridge (built on top of M.E.AI) silently dropped `ChatResponseFormat.ForJsonSchema()`, returning free-form JSON instead of constrained structured output. The fix was DEC-037's `AnthropicStructuredOutputClient` — a custom `DelegatingChatClient` that intercepts the structured-output schema, calls the raw Anthropic SDK with the schema preserved, and wraps the response in M.E.AI's expected shape. This is documented in `docs/decisions/decision-log.md` § DEC-037 and `docs/research/artifacts/batch-7a-ichatclient-structured-output-bridge.md`.

**The new context (R-048):** R-048 (`docs/research/artifacts/batch-16a-onboarding-conversation-state.md`) reported: *"the first-party `Anthropic` NuGet (v12.x as of April 2026, from `github.com/anthropics/anthropic-sdk-csharp`) is the 2026 default; it implements `Microsoft.Extensions.AI.IChatClient`, so your `ICoachingLlm` adapter can sit over either the raw Anthropic client or MEAI with a config switch."* This is a meaningful change — the first-party SDK now exists (POC 1 used a community SDK called `Anthropic.SDK` from `tghamm/Anthropic.SDK`) and now implements `IChatClient`. The DEC-037 problem may be solved, or it may persist in the new SDK.

**Existing constraints:**

- All LLM calls flow through `ICoachingLlm` (project-internal interface) which today is implemented by `ClaudeCoachingLlm` calling `IChatClient` from M.E.AI.
- `AnthropicStructuredOutputClient` is registered as the `IChatClient` implementation for the structured-output paths.
- Eval cache replay (DEC-039) intercepts at the `IChatClient` layer via M.E.AI.Evaluation's `DiskBasedCachingChatClient`. Whatever SDK we pick must compose with this.
- Prompt YAML files in `Prompts/` (e.g., `coaching-v1.yaml`) are loaded via `YamlPromptStore`. Independent of the SDK choice.
- Slice 0 spec (`docs/specs/12-spec-slice-0-foundation/`) does NOT wire any LLM client — Slice 1 is where the first new LLM call sites land.
- Slice 1 will introduce a multi-turn onboarding flow with structured outputs per turn (`{ extracted: PartialAnswer, reply: string, confidence: number, needs_clarification: bool }` per DEC-047) AND prompt caching enabled day-one (`cache_control: { type: "ephemeral", ttl: "1h" }`).
- Slice 3 (adaptation) will add structured-output schema for `MesoWeekOutput`-style plan modifications.
- Slice 4 (open conversation) may add tool use; future slices may add extended thinking.

**The decision matters now because** every Slice 1 LLM call site is shaped by the SDK choice. Migrating later means rewriting every call site that exists by then.

## Research Question

**Primary:** Should RunCoach migrate from DEC-037's `AnthropicStructuredOutputClient` bridge to the first-party `Anthropic` NuGet v12.x's `IChatClient` implementation, keep the bridge, or adopt a hybrid? What are the concrete capability differences in 2026 across structured outputs, prompt caching, tool use, extended thinking, and the eval-cache integration story?

**Sub-questions (must be actionable):**

1. **First-party `Anthropic` NuGet status.** Confirm the package's current state: NuGet name, version (R-048 cited v12.x), source repo, license, .NET 10 support, IChatClient implementation quality. Does it expose Anthropic's full Messages API surface or a curated subset?

2. **Structured output handling — the original DEC-037 problem.** Does the first-party SDK's `IChatClient` implementation preserve `ChatResponseFormat.ForJsonSchema(schema)` end-to-end, or does it suffer the same drop the community SDK had? Document the actual code path in v12.x. If preserved, the DEC-037 bridge is redundant for structured outputs; if not, it's still load-bearing.

3. **Prompt caching support (`cache_control`).** Does the first-party SDK accept `cache_control` block-level annotations on system prompt and conversation prefix? Is there an `IChatClient` extension surface for it, or do callers drop down to the raw SDK? How does the cache-hit metadata (cache-creation-tokens vs cache-read-tokens) surface back to callers?

4. **Tool use (`tool_use` / `tool_result` blocks).** Slice 4 may use tools; future slices likely. Does v12.x support tool use through `IChatClient` cleanly, or is it raw-SDK-only? What's the round-trip pattern for typed tool-use blocks (`tool_use.input` deserialization, `tool_result` echo with verbatim signatures)?

5. **Extended thinking (`thinking` / `redacted_thinking` blocks).** Anthropic's extended-thinking mode is now stable. Does v12.x surface these blocks through `IChatClient`, and does it preserve the `signature` fields verbatim per Anthropic's verbatim-echo requirement? DEC-047 already requires the event log to carry these blocks verbatim; the SDK choice affects how cleanly we get them.

6. **Eval cache integration (DEC-039).** Existing eval replays go through `DiskBasedCachingChatClient` (M.E.AI.Evaluation primitive). The cache fingerprint hash includes the request shape. If we change SDK, does the cache fingerprint change in a way that invalidates committed fixtures? What's the migration story for existing eval cache entries?

7. **`Microsoft.Extensions.AI` (M.E.AI) bridge in 2026.** Has Microsoft (or the SDK author) fixed the bridge issue that motivated DEC-037? Search M.E.AI release notes 2025–2026 for structured-output fixes. If fixed, the bridge is unnecessary; if not, the bridge stays even with a first-party SDK switch.

8. **Migration cost from `AnthropicStructuredOutputClient` to first-party.** Concretely: which files change? Is `ClaudeCoachingLlm` rewritten or just rewired? Does `ICoachingLlm` change shape? Does the `ContextAssembler` need adaptation? What's the test impact (eval cache, integration tests)?

9. **Hybrid pattern feasibility.** Can the project register `IChatClient` to use the first-party SDK for normal calls and the bridge for structured-output calls, switched per-call site or per-endpoint? What's the wiring shape, and is it more or less complex than either pure path?

10. **Streaming, retries, and error handling.** Slice 4 introduces streaming responses (open conversation). Does v12.x's `IChatClient` implementation support streaming via `IAsyncEnumerable<StreamingChatCompletionUpdate>` or equivalent? Are retries on rate limit baked in, configured by `IHttpClientFactory`, or hand-rolled?

11. **R-051 (LLM observability) interaction.** Whichever SDK is chosen, the trace-id propagation (per parallel R-051) flows through it. Does v12.x emit OTel spans natively, or do we wrap it in a `DelegatingChatClient` for tracing?

12. **Future Anthropic features.** Anthropic ships features quickly (vision, computer use, message batches, agent SDK). Which features ship first to the first-party SDK vs land late in `Anthropic.SDK` community packages? Is the cadence-asymmetry decisive on its own?

13. **License and supply-chain.** Confirm the first-party SDK's license, signed-package status, dependency tree, and Anthropic's commitment trajectory. Does it have any dependencies that conflict with M.E.AI / Marten / Wolverine pins?

14. **Real-world adoption.** GitHub-search evidence — are .NET projects that previously used `Anthropic.SDK` migrating to first-party in 2026? Any blog posts / Microsoft samples / Anthropic samples that show the migration?

## Why It Matters

- **Slice 1 is the next slice and adds many LLM call sites.** Every site shaped by the SDK choice. Picking now beats refactoring later.
- **Structured outputs are load-bearing for Slice 1.** Every onboarding turn returns a structured `{ extracted, reply, confidence, needs_clarification }` schema. If the first-party SDK handles this cleanly, the DEC-037 bridge becomes maintenance debt.
- **Prompt caching is a major cost lever** (~70% input-token saving per R-048). The SDK must surface cache-control natively or the savings are unrealizable.
- **The DEC-037 bridge has been working** — there's no urgency for migration unless the new SDK genuinely improves the contract. The "ruthlessly honest" answer might be "keep the bridge; defer the migration." We need that answer explicitly.
- **R-051 (LLM observability) and R-052 are interlinked.** Whichever observability tool wins must work with the chosen SDK. Settling SDK first means R-051's recommendation isn't conditional.

## Deliverables

- **A concrete recommendation** — migrate / keep bridge / hybrid — with explicit rationale.
- **A capability comparison matrix** — first-party `Anthropic` v12.x `IChatClient` vs `AnthropicStructuredOutputClient` bridge vs hybrid — across structured outputs, prompt caching, tool use, extended thinking, streaming, eval-cache compatibility, observability hooks, license, future-feature lead time, real-world adoption.
- **An explicit verdict on the original DEC-037 problem** — has the underlying bug been fixed in 2026-vintage `Anthropic.SDK`/M.E.AI, or does it persist?
- **A migration scope estimate** if migration is recommended — file-by-file changes, eval-cache impact, test impact, hours.
- **A wiring sketch** for the recommended path — `Program.cs` registration of `IChatClient`, plus the `ClaudeCoachingLlm` shape and any `DelegatingChatClient` wrappers (eval cache, observability, retry).
- **A keep-bridge-but-prepare-for-future-migration plan** if the recommendation is to defer — what to monitor, what triggers re-evaluation.
- **An interaction note with R-051** — confirm the recommended SDK plays nicely with the recommended observability stack.
- **An interaction note with R-053** — confirm the recommended SDK composes with the multi-turn eval pattern.
- **Citations** — current `Anthropic` first-party docs, GitHub repo for the package, M.E.AI release notes 2025–2026, real adoption signal from .NET projects.

## Out of Scope

- Choice of LLM provider — locked in DEC-022 / DEC-038 (Anthropic with future tiered model routing).
- Choice of prompt-storage format — `YamlPromptStore` is locked.
- Tool-use UX / agentic patterns — orthogonal; researched separately when tool use is on a real slice.
- Multi-provider abstraction (one `ICoachingLlm` over Anthropic + OpenAI + Azure OpenAI) — out of scope; DEC-022 picked single-provider with thin abstraction.
- M.E.AI's broader feature surface — only the Anthropic-relevant subset matters here.
