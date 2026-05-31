> **Research artifact — Batch 28b · R-078.** Commissioned via the RunCoach research protocol; prompt at `docs/research/prompts/batch-28b-wolverine6-transient-llm-failure-error-policy.md`. Deep-web-research output landed & integrated 2026-05-31 (queue → Integrated). Locks **DEC-073** (synchronous in-handler LLM-failure policy). Decided now in the Slice 2b spec; first exercised live in Slice 3. Verbatim research output follows.

---

# ADR: Transient vs. Terminal LLM-API Failure Policy for Synchronous Request/Reply Handlers

**Status:** Proposed · **Date:** 2026-05-31 · **Scope:** In-handler LLM calls on synchronous `InvokeForTenantAsync<TResponse>` request/reply endpoints (onboarding, plan-regeneration, future adaptation/conversation). Decided now in the Slice 2b spec; first exercised live in Slice 3.

---

## Executive summary — the three linked decisions

1. **Retry seam → the Anthropic SDK's own retry config, and ONLY there.** Per Anthropic's official C# SDK docs (platform.claude.com, "Retries"): *"The SDK automatically retries 2 times by default, with a short exponential backoff between requests. Only the following error types are retried: Connection errors … 408 Request Timeout · 409 Conflict · 429 Rate Limit · 5xx Internal."* Keep that as the single retry layer. Do **not** add Wolverine `RetryWithCooldown`, do **not** add a Microsoft.Extensions.AI resilience `DelegatingChatClient`, and do **not** add `Microsoft.Extensions.Http.Resilience` — the latter is in any case not attachable because the official SDK does not document a custom-`HttpClient` injection seam. Tune the SDK for interactivity: `MaxRetries = 2`, per-attempt `Timeout ≈ 30s` (the default is 10 minutes), total wall-clock bounded by the inbound request `CancellationToken`. 🔴

2. **Wire envelope → a `Kind = Error` branch on the response DTO (flat nullable-slot + discriminator), returned with HTTP 200.** Add an `Error` variant to `OnboardingTurnResponseDto.Kind` carrying `errorMessage` (human-readable), `retryable` (bool), and `retryAfterSeconds` (nullable int). This is Pattern-B-safe (no `oneOf`/`anyOf`), flows cleanly through Swashbuckle→Orval→Zod (DEC-066), and is reused verbatim across endpoints. 🔴

3. **OTel dead-letter signal → an ERROR-status GenAI span plus a non-paging failure counter.** Reuse Microsoft.Extensions.AI's `UseOpenTelemetry()` GenAI instrumentation (sets span status ERROR and `error.type`), and emit a custom `Counter` (`coaching.llm.failures`) tagged `error.type` and `outcome={transient_exhausted|terminal}`. Counter feeds a Jaeger/Grafana panel only — **no alert route**. 🟠

---

## 1. Do Wolverine error policies apply to `InvokeForTenantAsync`? 🔴 (load-bearing)

**Answer: only the `Retry` and `Retry With Cooldown` policies apply to inline `InvokeAsync`/`InvokeForTenantAsync`. `MoveToErrorQueue` / dead-lettering does NOT.** This is documented explicitly in the Wolverine error-handling guide: *"When using `IMessageBus.InvokeAsync()` to execute a message inline, only the 'Retry' and 'Retry With Cooldown' error policies are applied to the execution automatically."* Custom continuation actions can be opted into for inline invocation only by returning an `InvokeResult` of `Stop` or `TryAgain` from a custom action. The dead-letter-queue continuation is part of the durable message-processing pipeline (durable inbox/outbox), which inline request/reply does not traverse.

**Consequence:** the three `OnException<…>().MoveToErrorQueue()` policies in `Program.cs` (for `ExistingStreamIdCollisionException`, `ConcurrentUpdateException`, `JasperFx.DocumentAlreadyExistsException`) are **inert for the synchronous LLM path** — they only matter if those exceptions ever flow through a durable queue. An unhandled LLM exception inside `OnboardingTurnHandler` therefore propagates straight back out of `InvokeForTenantAsync` to the controller and surfaces as the current HTTP 500. **Retry for a synchronous LLM call cannot rely on the durable error pipeline and must live elsewhere** (see §3). We could technically attach `OnException<TransientCoachingLlmException>().RetryWithCooldown(...)` since Retry policies *do* apply inline — but we deliberately reject that to avoid stacking a second retry layer on top of the SDK's (see §3).

Sources: Wolverine "Error Handling," "Sending Messages with IMessageBus," "Wolverine as Mediator," and "Runtime Architecture" docs (wolverinefx.net); Jeremy Miller, "Wolverine for MediatR Users."

## 2. What does Anthropic 12.24.0 do on its own, and how does it survive the M.E.AI bridge? 🔴

**Exception taxonomy (official C# SDK docs, platform.claude.com):**

| Failure | HTTP | Exception type | Base |
|---|---|---|---|
| Bad request / invalid schema | 400 | `AnthropicBadRequestException` | `Anthropic4xxException` |
| Bad/expired key | 401 | `AnthropicUnauthorizedException` | `Anthropic4xxException` |
| Forbidden | 403 | `AnthropicForbiddenException` | `Anthropic4xxException` |
| Model deprecated / not found | 404 | `AnthropicNotFoundException` | `Anthropic4xxException` |
| Unprocessable | 422 | `AnthropicUnprocessableEntityException` | `Anthropic4xxException` |
| Rate limit | 429 | `AnthropicRateLimitException` | `AnthropicApiException` |
| Internal / overloaded | 5xx (incl. 529) | `Anthropic5xxException` | `AnthropicApiException` |
| Other non-2xx | – | `AnthropicUnexpectedStatusCodeException` | `AnthropicApiException` |
| SSE stream error after 200 | – | `AnthropicSseException` | `AnthropicException` |
| Network / I/O | – | `AnthropicIOException` | `AnthropicException` |
| Malformed response data | – | `AnthropicInvalidDataException` | `AnthropicException` |

All API errors derive from `AnthropicApiException`; everything derives from `AnthropicException`.

**Built-in retry (official docs "Retries"):** *"The SDK automatically retries 2 times by default, with a short exponential backoff between requests."* Retried types: connection errors, 408, 409, 429, 5xx. *"The API may also explicitly instruct the SDK to retry or not retry a request"* (i.e., the server-specified delay / Retry-After is honored). Configurable via the `MaxRetries` property or per-call `WithOptions(o => o with { MaxRetries = n })`. Per Anthropic's C# SDK docs, *"Requests time out after 10 minutes by default"* (configurable via `Timeout`, e.g. `new() { Timeout = TimeSpan.FromSeconds(42) }`) — far too long for an interactive turn.

**Anthropic error-code semantics (docs.anthropic.com):** 429 `rate_limit_error` is per-account and **carries a `retry-after` header**; 529 `overloaded_error` is provider-wide capacity saturation and **has no trustable `retry-after`** — corroborated verbatim by Respan ("Anthropic API Rate Limits + 429/529 Handling Guide," 2026): *"HTTP 529 overloaded_error means Anthropic itself is over capacity … The error has nothing to do with your usage. There is no retry-after value you can trust."* 529 responses are not billed and are a pure retry/backoff target.

**Survival across the bridge:** The official SDK exposes `client.AsIChatClient("model")`, its Microsoft.Extensions.AI `IChatClient` implementation. Provider exceptions propagate **unwrapped** through the M.E.AI pipeline: `DelegatingChatClient`'s *"default implementation simply passes each call to the inner client instance"* (Microsoft Learn) — no try/catch, no exception transformation. Real-world corroboration: dotnet/extensions issue #6753 shows an original provider exception bubbling straight through the M.E.AI chat-client adapter with its type intact. The repo's `AnthropicStructuredOutputClient : DelegatingChatClient` inherits that pass-through unless it explicitly catches. Therefore `catch (AnthropicRateLimitException)` / `catch (Anthropic5xxException)` at the `ClaudeCoachingLlm` layer will see the original typed exception, provided no intermediate `DelegatingChatClient` swallows it. **Caveat:** this rests on the documented M.E.AI pass-through contract plus the documented SDK exception model; a line-by-line read of the SDK's adapter `catch` logic was not retrievable from GitHub source in this round, so **confirm with one integration test that throws a 429 through the full chain** (`ICoachingLlm → ClaudeCoachingLlm → AnthropicStructuredOutputClient → AsIChatClient → SDK`).

## 3. Avoid double-retry — the single seam and concrete values 🔴

The SDK and `Microsoft.Extensions.Http.Resilience` both operate on the same underlying `HttpClient`; stacking them multiplies attempts (e.g. 3 SDK × 3 Polly = 9 calls) and latency/token spend on a 6-call plan chain. **Decision: retry lives exclusively in the SDK config.** Two reinforcing reasons:

- The SDK's retry already does the right thing (correct status classification + honors server delay).
- The official `Anthropic` SDK does **not document any custom-`HttpClient`/`HttpMessageHandler` injection seam** (only `ApiKey`, `AuthToken`, `BaseUrl`, `Timeout`, `MaxRetries`, `ResponseValidation` are documented). So `Http.Resilience` is not even cleanly attachable to the official client. (The "pass a custom `HttpClient`" pattern circulating online belongs to the *unofficial* `tghamm/Anthropic.SDK`, a different package — do not conflate.)

**Recommended interactive tuning (the only retry layer):**

| Knob | Value | Why |
|---|---|---|
| `MaxRetries` | `2` (keep default) | 1 + 2 = 3 attempts max; enough to ride out a brief 429/529 blip a human will tolerate |
| per-attempt `Timeout` | `≈ 30s` | bound a single hung attempt (default 10 min is unacceptable interactively) |
| total wall-clock budget | inbound request `CancellationToken` (~45–60s ceiling) | flows through `InvokeForTenantAsync(..., ct)`; the human is waiting |
| backoff | SDK default exponential + server-delay honoring | no custom curve needed |
| Wolverine `RetryWithCooldown` | **none** on the command | would double-retry |
| M.E.AI resilience client | **none** | would double-retry |
| `Http.Resilience` | **none** | would double-retry; also not attachable to official SDK |

**Explicit no-double-retry statement:** there is exactly one retry layer in the system — the Anthropic SDK. Every other layer (Wolverine, M.E.AI middleware, HttpClient) must remain retry-free for in-handler LLM calls.

*(For reference, the `Http.Resilience` `AddStandardResilienceHandler` defaults are `Retry.MaxRetryAttempts = 3`, `Retry.Delay` "Default is 2 seconds" exponential w/ jitter, `TotalRequestTimeout.Timeout` "Default is 30 seconds," per-attempt timeout `10s`, handling 5xx/429/408 and honoring Retry-After — a fine general-purpose default per Microsoft's option docs and Milan Jovanović's "Overriding Default HTTP Resilience Handlers in .NET," but redundant here.)*

## 4. Transient vs. terminal classification + the `ICoachingLlm` exception contract 🟠

**Canonical mapping:**

| Anthropic failure | Class | Action |
|---|---|---|
| 429 `AnthropicRateLimitException` | transient | SDK retries transparently; if exhausted → `TransientCoachingLlmException(retryAfter)` |
| 5xx / 529 `Anthropic5xxException` | transient | SDK retries; if exhausted → `TransientCoachingLlmException` |
| 408 / connection / `AnthropicIOException` / timeout | transient | SDK retries; if exhausted → `TransientCoachingLlmException` |
| 400 `AnthropicBadRequestException` | terminal | fail fast → `PermanentCoachingLlmException` |
| 401 `AnthropicUnauthorizedException` | terminal | fail fast → `PermanentCoachingLlmException` |
| 403 / 404 (model deprecated) | terminal | fail fast → `PermanentCoachingLlmException` |
| `AnthropicInvalidDataException` (malformed) | terminal | fail fast → `PermanentCoachingLlmException` |

**Where translation lives:** in `ClaudeCoachingLlm` — the adapter that owns the SDK call and is the lowest layer that still understands Anthropic types. Translating here (not in a `DelegatingChatClient`, not at the call site) keeps `ICoachingLlm`'s contract provider-agnostic: handlers and any policy react to two domain exceptions only.

```csharp
// ICoachingLlm contract — provider-agnostic
public abstract class CoachingLlmException(string message, Exception? inner = null)
    : Exception(message, inner);

public sealed class TransientCoachingLlmException(string message, TimeSpan? retryAfter = null, Exception? inner = null)
    : CoachingLlmException(message, inner)
{ public TimeSpan? RetryAfter { get; } = retryAfter; }

public sealed class PermanentCoachingLlmException(string message, Exception? inner = null)
    : CoachingLlmException(message, inner);

// ClaudeCoachingLlm — owns the SDK call + translation (the ONLY place that knows Anthropic types)
internal sealed class ClaudeCoachingLlm(ICoachingLlmInner llm) : ICoachingLlm
{
    public async Task<T> GenerateStructuredAsync<T>(CoachingPrompt p, CancellationToken ct)
    {
        try
        {
            // SDK has already exhausted its 2 internal retries by the time these throw
            return await llm.GenerateStructuredAsync<T>(p, ct);
        }
        catch (AnthropicRateLimitException ex)               // 429
        {
            throw new TransientCoachingLlmException(
                "The coaching service is busy.", ex.RetryAfter, ex);
        }
        catch (Anthropic5xxException ex)                     // 5xx incl. 529
        {
            throw new TransientCoachingLlmException(
                "The coaching service is temporarily unavailable.", inner: ex);
        }
        catch (AnthropicIOException ex)                      // network / timeout
        {
            throw new TransientCoachingLlmException(
                "The coaching service could not be reached.", inner: ex);
        }
        catch (OperationCanceledException) { throw; }        // honor the request budget
        catch (AnthropicApiException ex)                     // 400/401/403/404/422 → terminal
        {
            throw new PermanentCoachingLlmException(
                "The coaching request could not be processed.", ex);
        }
    }
}
```
*(`ex.RetryAfter` shown illustratively; if the 12.24.0 surface exposes the header differently, read it from the raw response headers via `WithRawResponse`.)*

## 5. The wire-level error envelope 🔴

**Recommendation: option (b) — a `Kind = Error` branch on the response DTO, returned with HTTP 200.** Rejected: (a) RFC 9457 ProblemDetails 503 — breaks the uniform discriminated-response contract and makes RTK Query treat the turn as a thrown error; (c) bare 200 partial-success — no machine-readable retryable signal.

```csharp
public enum OnboardingTurnKind { Ask, Complete, Error }

// Flat nullable-slot + kind discriminator — Pattern-B safe (no oneOf/anyOf), Zod/Orval-friendly
public sealed record OnboardingTurnResponseDto
{
    public required OnboardingTurnKind Kind { get; init; }
    // Ask slots
    public string? Question { get; init; }
    // Complete slots
    public PlanDto? Plan { get; init; }
    // Error slots
    public string? ErrorMessage { get; init; }     // human-readable
    public bool? Retryable { get; init; }           // machine signal
    public int? RetryAfterSeconds { get; init; }    // optional hint
}
```

This satisfies all three constraints: (i) carries a human message + machine `retryable` boolean (+ optional `retryAfterSeconds`); (ii) flat nullable slots + discriminator → no `oneOf`/`anyOf`, so it survives Anthropic constrained-decoding Pattern B (DEC-058) **and** the Swashbuckle→Orval→Zod pipeline (DEC-066); (iii) identical shape reused across onboarding, plan-regeneration, and future adaptation/conversation endpoints. Anthropic's structured-output docs confirm the engine rejects `oneOf`/`anyOf`/`allOf` and complex schema composition (`tools…input_schema does not support oneOf, allOf, or anyOf`), and recommend flattening — exactly what the nullable-slot convention provides.

**Why HTTP 200 and not 503:** the LLM failure is a *domain outcome of an interactive coaching turn*, not a transport fault; returning it in-band keeps the typed success channel uniform, keeps the codegen single-schema, and lets the React client branch on `kind` rather than catch a thrown error. The cost — losing HTTP-level error semantics — is repaid by §6: the span is still marked ERROR and the failure counter still increments server-side, so observability is not sacrificed. `ErrorHandlingMiddleware` remains the catch-all only for truly unexpected exceptions; the handler now maps `TransientCoachingLlmException`/`PermanentCoachingLlmException` to the `Error` branch before they reach it.

RFC 9457 (Problem Details, obsoletes RFC 7807) supports custom extension members such as `retryable`, and the `Retry-After` header (RFC 9110) is the standard transient-retry signal — we mirror those semantics inside the body (`retryable`, `retryAfterSeconds`) rather than at the transport layer, for contract uniformity.

## 6. OTel dead-letter signal — non-paging 🟠

- **Span:** reuse Microsoft.Extensions.AI `UseOpenTelemetry()` (`OpenTelemetryChatClient`, which "provides an implementation of the Semantic Conventions for Generative AI systems v1.37"). On failure set span **status = ERROR** and `error.type` = the Anthropic exception's canonical name or status code (per OTel GenAI semconv: *"`error.type` SHOULD match the error code returned by the Generative AI provider or the client library, the canonical name of exception that occurred, or another low-cardinality error identifier"*). GenAI attributes `gen_ai.provider.name`, `gen_ai.request.model`, `gen_ai.operation.name` are emitted by the instrumentation. OTel guidance: keep exception details in the normal exception fields/events and set span status ERROR when the operation fails.
- **Metric:** emit a custom `System.Diagnostics.Metrics.Counter<long>` named `coaching.llm.failures`, tagged `error.type` and `outcome` (`transient_exhausted` | `terminal`), plus `endpoint`. A monotonic OTel `Counter` (an accumulating, only-goes-up instrument) is the correct shape for terminal-failure accounting. Wire it to a Jaeger/Grafana panel the solo builder watches — **no alert/pager route**.
- **Wolverine DLQ hook:** not applicable to the inline path (§1 — inline invocation never dead-letters). If a future durable path is introduced (Slice 4, out of scope), Wolverine emits the `wolverine.error.queued` activity event and failure/dead-letter counters via the `Wolverine:{App}` meter (`opts.InvokeTracing = InvokeTracingMode.Full` is also available to get full structured logs for inline invocations); hook those then.

## 7. Idempotency on retry 🟠

`IIdempotencyStore` (Marten-document-backed, DEC-060) is keyed by client `idempotencyKey`. **Rule: write the idempotency marker in the SAME Marten transaction as the side effects.** Then:

- **Transient failure (transaction aborts):** the marker is rolled back with the side effects → **the same `idempotencyKey` is safely reusable** on the client's "try again." This matches Stripe's guidance (docs.stripe.com, "Idempotent requests"): *"if a request to create an object doesn't respond because of a network connection error, a client can retry the request with the same idempotency key to guarantee that no more than one object is created."* Nothing was committed, so there is no double-charge of tokens.
- **Terminal failure:** also rolls back; the client receives `retryable=false` and is steered away from retrying (§8). Optionally persist a short-lived `failed-permanent` marker to short-circuit accidental replays, but it is not required since the client contract forbids retry.
- **Success:** marker + side effects commit atomically; a replay with the same key returns the stored result without re-invoking the LLM — no double-write, no double token spend.

This co-transaction approach is cleaner than Stripe's v1 "cache even errors" model because Marten gives us ACID rollback for free; we never persist a half-finished LLM turn. (It aligns with the DEC-057 concurrency handling already settled.) Note Stripe's broader caution: idempotency keys are a quick-retry safety mechanism, not a perpetual guarantee — for very-delayed retries, a fresh key plus existence checks are appropriate, but that is outside MVP-0 scope.

## 8. Frontend "try again" contract (React 19 / RTK Query) 🟡

Because the envelope is returned as HTTP 200 `data` (not an HTTP error), the RTK Query `retry()` baseQuery wrapper — which only fires on thrown/HTTP-error results — is **not** the right tool here and should not wrap this endpoint. Branch in the component on the typed response instead:

- `kind === 'Ask' | 'Complete'` → normal flow.
- `kind === 'Error' && retryable === true` → render a **manual "Try again"** affordance (re-dispatch the same mutation with the same `idempotencyKey`); if `retryAfterSeconds` is present, disable the button for that countdown. **No auto-retry for MVP-0** — the SDK already retried and a human is present.
- `kind === 'Error' && retryable === false` → route to a **terminal "something went wrong"** state; do not offer retry.

Forward-compatible: a bounded auto-retry can be layered later for non-interactive/background calls without changing the envelope. Per Redux Toolkit docs ("Customizing Queries"), the `retry` utility *"defaults to 5 attempts with a basic exponential backoff"* (intervals `~600ms · 1.2s · 2.4s · 4.8s · 9.6s`, each ×`random(0.4,1.4)`), with per-endpoint opt-out via `maxRetries: 0` — suitable for future background calls but explicitly not wired onto interactive coaching turns now.

## 9. Slice 2b scope check 🔵

**Confirmed internally consistent: there is NO synchronous LLM call on any Slice 2b request path.** The workout-log WRITE is pure persistence; the LLM only *consumes* logs at adaptation (Slice 3) and conversation (Slice 4) time, and the 2b "logged notes flow into context" criterion is verified at eval time, not in a live request. This policy is therefore **decided now (in the 2b spec) as the general in-handler-LLM-call contract and first exercised live in Slice 3** — pure forward-provisioning, not 2b runtime behavior. The 2b spec should state this explicitly so the slice is scoped correctly and no resilience/envelope work is mistakenly pulled into 2b implementation.

---

## Recommendations — staged, with the benchmarks that would change them

**Stage 0 (decide now, in the 2b spec — no code):** Record the five DEC entries below. Add the explicit "no live LLM call on a 2b request path" sentence (§9).

**Stage 1 (Slice 3, first live exercise):**
1. Configure the Anthropic SDK once: `MaxRetries = 2`, per-attempt `Timeout ≈ 30s`; pass the request `CancellationToken` all the way through `InvokeForTenantAsync`. Confirm no other layer retries.
2. Add `TransientCoachingLlmException`/`PermanentCoachingLlmException` and the translation `try/catch` in `ClaudeCoachingLlm` (sub-100-LOC sketch in §4).
3. Add the `Error` branch to the response DTO(s) and map both domain exceptions to it in the handler (§5).
4. Write **one integration test** that forces a 429 (and a 400) through the full chain to verify exceptions arrive **unwrapped** at `ClaudeCoachingLlm` — this closes the one residual uncertainty in §2.
5. Wire `UseOpenTelemetry()` on the chat-client pipeline and add the `coaching.llm.failures` counter (§6); add a Grafana/Jaeger panel, **no alert**.

**Stage 2 (post-MVP-0, if signals warrant):**
- If the `coaching.llm.failures{outcome=transient_exhausted}` rate exceeds a chosen threshold (e.g. >2% of turns over a rolling window — Respan flags >2% sustained 529s as architecturally significant), revisit: raise `MaxRetries`, add model/provider fallback (Sonnet→Haiku, or Bedrock/Vertex), or move plan-gen to the async Slice-4 path.
- If background/non-interactive LLM calls appear, layer RTK `retry` (or a dedicated resilience `DelegatingChatClient`) **only on those**, never on interactive turns.

**Thresholds that would flip a decision:**
- *Custom `HttpClient` becomes documented on the official SDK* → `Http.Resilience` becomes a viable alternative seam, but the no-double-retry rule still forces choosing exactly one (prefer SDK config for simplicity).
- *The integration test shows exceptions are wrapped/flattened* → move the `catch` translation to inspect `InnerException`/status code, or add a thin classifying `DelegatingChatClient` above `AnthropicStructuredOutputClient`.

## Decisions to record (paste into the decision log)

- **DEC-0xx (Retry seam):** All retry for in-handler synchronous LLM calls lives in the Anthropic SDK config only (`MaxRetries = 2`, per-attempt `Timeout ≈ 30s`, total budget = request `CancellationToken`). No Wolverine `RetryWithCooldown`, no M.E.AI resilience client, no `Http.Resilience` for the LLM path. Wolverine error policies do not retry/dead-letter inline `InvokeForTenantAsync` beyond Retry/RetryWithCooldown, which we decline. Single retry layer; no double-retry.
- **DEC-0xx (Exception contract):** `ICoachingLlm` throws `TransientCoachingLlmException` / `PermanentCoachingLlmException`; translation from Anthropic exception types lives in `ClaudeCoachingLlm`.
- **DEC-0xx (Wire envelope):** Synchronous LLM-backed endpoints return a `Kind = Error` branch (flat nullable slots: `errorMessage`, `retryable`, `retryAfterSeconds`) with HTTP 200 — Pattern-B (DEC-058) and Orval/Zod (DEC-066) compatible, uniform across endpoints.
- **DEC-0xx (OTel dead-letter signal):** Terminal LLM failures set the GenAI span status to ERROR with `error.type`, and increment a non-paging `coaching.llm.failures` counter tagged `error.type`/`outcome`. Dashboard only, no alerting.
- **DEC-0xx (Idempotency on retry):** The idempotency marker is written in the same Marten transaction as side effects; failed attempts roll back the marker so the same `idempotencyKey` is reusable, and successful turns are never re-invoked on replay.

---

## Caveats

- **Anthropic `Anthropic` SDK is in beta** ("Although this package is versioned as 10+, it's currently in beta … breaking changes may occur in minor or patch releases"). Pin 12.24.0 and re-verify the exception taxonomy and `MaxRetries`/`Timeout` surface on upgrade.
- **Exception-unwrapping through `AsIChatClient` + `AnthropicStructuredOutputClient` is a well-supported inference, not a source-confirmed fact** — it rests on the documented M.E.AI `DelegatingChatClient` pass-through and the documented SDK exception model. The Stage-1 integration test (Recommendation #4) is the gate that turns this from inference into verified behavior; treat it as required, not optional.
- **`RetryAfter` accessor on `AnthropicRateLimitException`** is shown illustratively; the exact property/surface in 12.24.0 was not source-confirmed. If absent, read `retry-after` from raw response headers via `WithRawResponse`.
- **The OTel GenAI semantic conventions are still "Development"/experimental** (M.E.AI implements v1.37; opentelemetry.io surfaces v1.40.0). Attribute/metric names may shift; the `error.type` + span-status-ERROR + custom counter approach is stable enough, but pin the M.E.AI version and re-check on upgrade.
- **Some operational figures (529 resolution times, "<2%" health thresholds, tier limits) come from vendor/community 2026 blogs (TokenMix, Respan, AI Free API), not Anthropic primary docs** — treat them as directional tuning guidance, not contractual SLAs. The retry/exception/timeout mechanics in this ADR all trace to Anthropic, Microsoft, Wolverine, OTel, IETF, and Stripe primary sources.
- **Scope boundaries respected:** the Slice-4 async flip, Marten concurrency handling (DEC-057), provider/SDK choice (R-052), prompt-injection sanitization (DEC-059), and tiered-model routing (DEC-038) are treated as settled and out of scope.