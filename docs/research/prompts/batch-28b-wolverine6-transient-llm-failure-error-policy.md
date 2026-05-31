# Research Prompt: Batch 28b — R-078

# Wolverine 6 error policy for transient LLM-API failures + Anthropic .NET SDK retry semantics + the structured error envelope for a synchronous request (Wolverine 6.1 / Anthropic 12.24 / M.E.AI, 2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a Wolverine 6.1.0 application (Marten 9.2.1, JasperFx 2.0) whose HTTP endpoints dispatch work via `IMessageBus.InvokeForTenantAsync<TResponse>(...)` (synchronous request/reply, mediator-style — NOT a durable background queue), where a handler makes an LLM call through `Microsoft.Extensions.AI` over the first-party `Anthropic` 12.24.0 SDK — what is the canonical 2026 policy for handling **transient LLM-API failures** (HTTP 429 rate-limit, 529 overloaded, request timeout, transient network error) versus **terminal failures** (401 bad key, 400 invalid request, model deprecated), and what user-facing error contract should a synchronous endpoint return when the LLM call ultimately fails?

Deliver a recommendation covering three linked decisions: (1) the retry/backoff/dead-letter policy and *where* it lives (Anthropic SDK vs `Microsoft.Extensions.AI` middleware vs Wolverine error-handling vs `Microsoft.Extensions.Http.Resilience`), (2) the wire-level error envelope shape, and (3) the OTel dead-letter signal — all without double-retrying and without paging the solo builder.

### Sub-questions the artifact must answer

1. **Do Wolverine error policies even apply to `InvokeForTenantAsync`?** This is the load-bearing question. Wolverine's `OnException(...).RetryWithCooldown(...)/ScheduleRetry/MoveToErrorQueue` policies are documented for the durable message-processing pipeline. The RunCoach endpoints call `IMessageBus.InvokeForTenantAsync<TResponse>(...)` for *synchronous request/reply*. Confirm authoritatively whether Wolverine 6.1 applies `OnException` retry/error policies to inline `InvokeAsync`/`InvokeForTenantAsync` invocations, or whether those policies only govern durably-queued messages — and therefore whether retry for a synchronous LLM call must live somewhere else entirely. Cite Wolverine 6 docs/source.
2. **What does the Anthropic 12.24.0 .NET SDK do on its own?** Enumerate the exception types it throws for 429 / 529 / 500 / timeout / network vs 400 / 401 / 404, whether it carries a `retry-after` header through, and its **built-in retry/backoff** (default `MaxRetries`, jitter, which statuses it auto-retries). How are these surfaced after the `Microsoft.Extensions.AI` `AsIChatClient()` bridge (and the repo's `AnthropicStructuredOutputClient` `DelegatingChatClient`) — preserved, wrapped, or flattened to a generic exception?
3. **Avoid double-retry.** If the SDK already retries 429/529 with backoff, what is the correct division of labor so we don't stack a second retry layer (multiplying latency and token spend on a 6-call plan chain)? Should retry live exclusively in the SDK config, in a `Microsoft.Extensions.AI` `ConfigureOptions`/resilience `DelegatingChatClient`, or in `Microsoft.Extensions.Http.Resilience` on the underlying `HttpClient`? Give the single recommended seam and the values (max attempts, base delay, cap, jitter, total timeout budget) appropriate for an interactive request a human is waiting on.
4. **Transient vs terminal classification.** Define the canonical mapping from Anthropic failure → {retry transparently, fail-fast with retryable=true, fail-fast with retryable=false}. Should `ICoachingLlm` translate SDK exceptions into a small domain exception set (e.g., `TransientCoachingLlmException` / `PermanentCoachingLlmException`) so call sites and any Wolverine policy can react uniformly? Where should that translation live?
5. **The wire-level error envelope.** When the LLM call exhausts retries on a synchronous endpoint, compare: (a) raw RFC 9457 `ProblemDetails` (status 503/502) — the current de-facto behavior, errors surface as an unhandled 500 via `ErrorHandlingMiddleware`; (b) a `kind=Error` branch on the success DTO (discriminated-union response carrying a user-facing message + `retryable` flag); (c) HTTP 200 with a partial-success body. Recommend one. It must (i) carry a human-readable message + a machine `retryable` boolean, (ii) be expressible through the Swashbuckle→Orval Zod codegen pipeline (DEC-066) — note Anthropic constrained-decoding Pattern B (DEC-058) rejects `oneOf`, so any discriminated response shape must follow the project's flat nullable-slot + `kind` discriminator convention — and (iii) be consistent across onboarding, plan-regeneration, and future adaptation endpoints.
6. **OTel dead-letter signal without paging.** What span/log/metric shape should a terminal LLM failure emit so it lands in the existing OTel collector + Jaeger dashboard (and a counter the builder can watch) without alerting/paging? If a Wolverine durable path is involved anywhere, what's the dead-letter observability hook?
7. **Idempotency interaction.** Endpoints use `IIdempotencyStore` (Marten-document-backed per DEC-060) keyed by a client `idempotencyKey`. If a user retries after a transient failure, the second attempt must not double-write or double-charge LLM tokens. Define the interaction between client retry, the idempotency marker, and any server-side retry — including whether a failed-then-retried request reuses or invalidates the marker.
8. **Frontend "try again" contract.** Given the envelope from (5), what should the React client do — auto-retry transient failures (with backoff/limit) vs surface a manual "Try again" affordance vs route terminal failures to a "something's wrong" state? Keep it minimal for a single-user MVP-0 but forward-compatible.
9. **Scope check for Slice 2b specifically.** In Slice 2b, the workout-log *write* is pure persistence — no LLM in the request path; the LLM only *consumes* logs at adaptation (Slice 3) and conversation (Slice 4) time, and the 2b "logged notes flow into context" criterion is verified at eval time, not in a live request. Confirm this, and frame the recommended policy as the **general in-handler-LLM-call contract decided now (in the 2b spec) and first exercised live in Slice 3** — i.e., is there *any* synchronous LLM call on a 2b request path, or is this purely forward-provisioning? State it plainly so the spec scopes the work correctly.

## Context

This resolves a Slice 1 carry-forward explicitly deferred into the Slice 2b spec: **"Wolverine LLM-failure error policy."** Today, when an in-handler LLM call throws (transient 429, network blip, timeout under longer prompts), the exception propagates to an unhandled 500 with no actionable user-facing message and no `retryable` signal.

**Verified repo state:**
- `Program.cs` configures Wolverine error policies for Marten concurrency only: `OnException<ExistingStreamIdCollisionException>().MoveToErrorQueue()`, `OnException<ConcurrentUpdateException>().MoveToErrorQueue()`, `OnException<JasperFx.DocumentAlreadyExistsException>().MoveToErrorQueue()`. **No transient-failure / LLM retry policy exists.**
- Endpoints (`OnboardingController`, plan regenerate) dispatch via `bus.InvokeForTenantAsync<TResponse>(userId.ToString(), command, ct)` — synchronous request/reply. `OnboardingTurnHandler` calls `llm.GenerateStructuredAsync<…>(…)` inside a `try/catch`; on failure the transaction aborts and the error surfaces as a 500 `ProblemDetails`.
- `OnboardingTurnResponseDto` is a flat record with a `Kind` discriminator (`Ask` | `Complete`) — **no `Error` branch**. Errors are out-of-band HTTP status + `ProblemDetails`.
- LLM stack: `ICoachingLlm` → `ClaudeCoachingLlm` → `Microsoft.Extensions.AI` `IChatClient` over `Anthropic` 12.24.0; structured output goes through a custom `AnthropicStructuredOutputClient : DelegatingChatClient`. Model: Claude Sonnet 4.x.
- DEC-057: single-handler / single-session / single-transaction. DEC-058: Anthropic constrained decoding rejects `oneOf`/discriminated-union schemas → flat nullable-slot + `Topic`/`kind` discriminator + post-deserialization validator. DEC-066: OpenAPI→Zod codegen drift gate; new endpoints must consume it.
- Prior research `batch-22a` covered `[AggregateHandler]` transaction scope and `ConcurrencyException → 409` (concurrent submissions should not re-run 6 LLM calls) but **explicitly did not** address transient LLM-API failure policy or the error envelope.
- Stack note: Wolverine 6 pulled Roslyn out of core (needs `WolverineFx.RuntimeCompilation` for dev/test `TypeLoadMode.Auto`); JasperFx 2.0 moved several types into the shared `JasperFx` assembly. `Microsoft.Extensions.Http.Resilience` (Polly-based) is available in the .NET 10 ecosystem.

## Why It Matters

Slice 2b's longer prompts (workout-log narrative + pace-zone interpretation) and Slice 3's adaptation chain make transient 429/timeout measurably more likely than in Slice 1. Without a decided policy, the first rate-limit during real use looks identical to a hard bug: opaque 500, no "try again," and — worse — a naive retry could re-run a multi-call LLM chain, doubling latency and token cost, or double-write through the idempotency seam. Deciding the retry seam, the wire contract, and the observability hook once (and applying it uniformly across onboarding, regeneration, adaptation, and conversation) prevents a class of "is it broken or just busy?" failures from reaching the user and the builder's pager.

## Deliverables

- **Retry/backoff decision** — the single seam (SDK config vs M.E.AI middleware vs `Http.Resilience` vs Wolverine), with concrete values tuned for an interactive request, and an explicit "no double-retry" statement.
- **Authoritative answer** on whether Wolverine 6 error policies apply to `InvokeForTenantAsync` (with citation), and the consequent placement of retry logic.
- **Anthropic 12.24 exception taxonomy** and how it survives the M.E.AI bridge, with the transient↔terminal classification table.
- **Recommended `ICoachingLlm` exception contract** (domain exception set + where translation lives), sub-100 LOC sketch.
- **Wire error-envelope recommendation** (one of the three options), codegen-compatible and Pattern-B-compatible, consistent across endpoints.
- **OTel dead-letter signal** shape (span/metric/log) for terminal failures, non-paging.
- **Idempotency-on-retry rule.**
- **Minimal frontend "try again" contract** keyed off the envelope's `retryable` flag.
- **Slice 2b scope statement** — whether any live LLM call sits on a 2b request path, framing the policy as decided-now/exercised-in-Slice-3 if not.

## Out of scope

- The Slice 4 async-plan-gen flip (handler split + cascading message + polling) — separately tracked; assume synchronous request/reply for this prompt.
- Marten concurrency/`ConcurrencyException` handling — already settled by `batch-22a` / DEC-057.
- Choosing a different LLM provider or SDK — Anthropic first-party + M.E.AI is locked (R-052).
- Prompt-injection sanitization of inputs — settled by DEC-059.
- Cost/tiered-model routing — deferred post-MVP-0 (DEC-038).

The artifact lands at `docs/research/artifacts/batch-28b-wolverine6-transient-llm-failure-error-policy.md` and integrates into the Slice 2b spec plus a new DEC entry locking the LLM-failure retry seam and the error-envelope contract.
