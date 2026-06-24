<!--
PROVENANCE: R-084 (batch-30c). Prompt: docs/research/prompts/batch-30c-anthropic-sdk-streaming-exceptions.md.
Surfaced in the Slice 4B conversation-core brainstorm (2026-06-24). Integration target:
docs/plans/mvp-0-cycle/slice-4b-conversation-core.md § Unit 1 + research-triggers; research-queue R-084.

VERIFICATION PASS (2026-06-24, against Anthropic 12.29.1 + the shipped PR4 classifier):
- CONFIRMED: the full exception hierarchy is present in the Anthropic 12.29.1 DLL metadata — AnthropicException
  (base), AnthropicApiException, Anthropic4xxException, Anthropic5xxException, the leaf status types
  (BadRequest/Unauthorized/Forbidden/NotFound/UnprocessableEntity/RateLimit/UnexpectedStatusCode), AnthropicIOException,
  AnthropicInvalidDataException, AnthropicServiceException, and **AnthropicSseException** (the mid-stream type).
- CONFIRMED: the shipped PR4 classifier (backend/src/.../Coaching/ClaudeCoachingLlm.cs) already catches
  AnthropicRateLimitException / Anthropic5xxException / AnthropicIOException / AnthropicApiException by status — the
  streaming mapping extends this, consistent with the artifact's "carry over / diverge" split.
- OPEN (carry to U1 implementation, per the artifact's own caveat): verify AnthropicSseException exposes the structured
  error `type` string reliably; if not, fall back to message-text matching ("overloaded"/"rate limit"). Test target.
-->

# R-084 — Anthropic SDK Streaming Error / Stop-Reason Surface → DEC-073 Failure Hierarchy

**Artifact:** `docs/research/artifacts/batch-30c-anthropic-sdk-streaming-exceptions.md`
**Lineage / cross-refs:** batch-30a (R-082, streaming transport) · batch-17b (first-party SDK vs M.E.AI bridge decision) · batch-28b (Wolverine 6 transient-LLM-failure error policy / why no second retry layer) · PR #167 (DEC-073 classifier, request/response path)
**Status:** implementation-ready. Centerpiece is the mapping table plus the StreamAsync shape; everything else pins from the SDK code surface and three targeted cross-checks.

---

## TL;DR
- The first-party Anthropic SDK **throws mid-enumeration** for streamed errors: an `error` SSE event arriving after the HTTP 200 (e.g. `overloaded_error`) surfaces as **`AnthropicSseException`** ("thrown for errors encountered during SSE streaming after a successful initial HTTP response"), which propagates through the M.E.AI `ChatResponseUpdate` enumeration (rethrown from `MoveNextAsync`, **no** final/sentinel update yielded). `max_tokens` is the exception to "throw" — it is a normal stop reason, not an exception, so StreamAsync must detect it explicitly.
- Recommended shape: **StreamAsync throws** DEC-073 types mid-enumeration; the SSE endpoint catches and translates to a terminal SSE `error` frame; `max_tokens` truncation is converted to the locked errored-turn path. Mid-stream errors are classified on the **SSE error-event `type` string**, not an HTTP status (there is none after the 200).
- **Aborting does NOT stop Anthropic billing.** Per Anthropic's Help Center "How will I be billed for Claude API use?": *"you will be charged if your client disconnects or times out in the middle of an API call that was on track to be successful."* Propagating `RequestAborted` is for UX/resource hygiene, not cost savings. SDK retry (`MaxRetries=2`) is **pre-first-byte only**, so there is no mid-stream double-billing — and no second retry layer should sit on top.

---

## Key Findings

### Version pins
- **Anthropic .NET SDK 12.29.1** — first-party, Stainless-generated, NuGet id **`Anthropic`** (source repo `github.com/anthropics/anthropic-sdk-csharp`). The package README banner reads: *"As of version 10+, the `Anthropic` package is now the official Anthropic SDK for C#."* Latest published version observed is **12.29.0 (last updated 9 June 2026)**; **12.x is the current major**, so the pinned 12.29.1 carries **no delta away from the current major** — the streaming error/stop-reason surface is stable across 12.x. Do **not** conflate with the community **`Anthropic.SDK` (latest 5.10.0, by tghamm)**, self-described as *"an unofficial C# client… not affiliated with, endorsed by, or sponsored by Anthropic"*, which uses a totally different surface (`MessageResponse`, `StreamClaudeMessageAsync`, `GetClaudeMessageAsync`).
- **Microsoft.Extensions.AI.Abstractions: resolves to 10.7.0** transitively. Anthropic 12.x declares a floor of `Microsoft.Extensions.AI.Abstractions (>= 10.2.0)`; M.E.AI.Evaluation 10.7.0 pulls the 10.7.0 line, and NuGet highest-wins resolution lands on **10.7.0**. The `IChatClient` / `ChatResponseUpdate` / `ChatFinishReason` surface used here is whatever 10.7.0 ships. (Note: the streaming `ChatResponseUpdate.ContinuationToken` resume API is marked `[Experimental]` in this line — do not depend on mid-stream resume.)
- **Microsoft.Extensions.AI.Evaluation 10.7.0** (+ `.Quality` / `.Reporting` siblings) — not itself part of the streaming failure surface, but it is the version driver that floats Abstractions to 10.7.0.
- **Minimum-version behaviors:** `model_context_window_exceeded` stop reason is returned natively only on Sonnet 4.5+ (earlier models require the `model-context-window-exceeded-2025-08-26` beta header, else they return a validation error pre-stream); `ChatFinishReason.Length` truncation mapping is present across the 10.x Abstractions.

### SDK exception hierarchy (from the SDK error-handling surface)
Per the official C# SDK docs (`platform.claude.com/docs/en/api/sdks/csharp`) and the package README:
- **`AnthropicException`** — base class for all exceptions.
- **`AnthropicApiException`** — base for API errors (HTTP-status-bearing). Status → leaf type:
  - 400 → `AnthropicBadRequestException`
  - 401 → `AnthropicUnauthorizedException`
  - 403 → `AnthropicForbiddenException`
  - 404 → `AnthropicNotFoundException`
  - 422 → `AnthropicUnprocessableEntityException`
  - 429 → `AnthropicRateLimitException`
  - 5xx → `Anthropic5xxException`
  - other → `AnthropicUnexpectedStatusCodeException`
  - all 4xx also inherit from `Anthropic4xxException`
- **`AnthropicSseException`** — *"thrown for errors encountered during SSE streaming after a successful initial HTTP response."* This is the **mid-stream** type. It does **not** carry a fresh HTTP status.
- **`AnthropicIOException`** — I/O / networking errors (transport drops, resets).
- **`AnthropicInvalidDataException`** — successfully parsed but semantically invalid data (e.g. a required property the API unexpectedly omitted). Thrown lazily on property access unless `ResponseValidation = true` or `.Validate()`.

### SDK retry policy (built-in)
Per the official C# SDK docs: *"The SDK automatically retries 2 times by default, with a short exponential backoff between requests. Only the following error types are retried: Connection errors… 408 Request Timeout · 409 Conflict · 429 Rate Limit · 5xx Internal."* The API may also explicitly instruct the SDK to retry / not retry. Default request timeout: *"Requests time out after 10 minutes by default."* Configurable via `new AnthropicClient { MaxRetries = n }` or `WithOptions(o => o with { MaxRetries = n })`.

These retries fire on the **initial request only (pre-first-byte)**. Once the 200 + first SSE event arrive, errors surface as `AnthropicSseException` and are **not** retried by the SDK.

### Stop reasons (string values, from the API/SDK)
`end_turn`, `max_tokens`, `stop_sequence`, `tool_use`, `pause_turn`, `refusal`, `model_context_window_exceeded`. In streaming these arrive on the `message_delta` event's `delta.stop_reason` (it is `null` in `message_start` and non-null thereafter). For free-text coaching, only `end_turn` and `stop_sequence` are clean completions; `max_tokens` and `model_context_window_exceeded` are truncations; `refusal` is a content-classifier block (note: refusal returns HTTP 200 and **you are billed for output tokens up until the refusal**); `tool_use`/`pause_turn` are not expected on this read-only coaching surface.

### SSE event taxonomy (HTTP streaming API)
`message_start` → `content_block_start` → `content_block_delta` (free text via `text_delta`) → `content_block_stop` → `message_delta` (carries `stop_reason` + final `usage`) → `message_stop`, with `ping` events interspersed. Per Anthropic's Errors docs: *"When receiving a streaming response over SSE, it's possible that an error can occur after returning a 200 response, in which case error handling wouldn't follow these standard mechanisms."* The error event body is e.g. `{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}`. Error `type` values (from the Errors docs status table): `invalid_request_error` (400), `authentication_error` (401), `permission_error` (403), `rate_limit_error` (429), `api_error` (500 — *"An unexpected error… internal to Anthropic's systems"*), `timeout_error` (504 — *"The request timed out while processing"*), `overloaded_error` (529 — *"The API is temporarily overloaded"*).

### M.E.AI bridge mapping
- `client.AsIChatClient("model").AsBuilder()…Build()` yields an `IChatClient` whose `GetStreamingResponseAsync(IEnumerable<ChatMessage>, ChatOptions?, CancellationToken)` returns `IAsyncEnumerable<ChatResponseUpdate>`.
- `ChatResponseUpdate.FinishReason` is a `ChatFinishReason` struct with canonical values **`Stop`, `Length`, `ToolCalls`, `ContentFilter`, `FunctionCall`**. `max_tokens` maps to `ChatFinishReason.Length`; `end_turn`/`stop_sequence` map to `Stop`.
- **Exceptions propagate through enumeration**: the bridge does **not** yield a sentinel/error update — `AnthropicSseException` / `AnthropicIOException` rethrow from `MoveNextAsync`. (This is why instrumenting wrappers call `MoveNextAsync` inside a try/catch and `yield return` outside the try — C# forbids `yield return` inside a try that has a catch.)

### Sub-Q1 — mid-enumeration throw vs in-band event (RESOLVED)
The wire carries an in-band `error` event, but the **SDK converts it to a thrown `AnthropicSseException`** mid-enumeration; it does **not** hand you a yielded error update. The M.E.AI bridge **rethrows** that exception through `ChatResponseUpdate` enumeration rather than yielding a final error update. Because `AnthropicSseException` is post-200, classification must key off the **SSE error `type` string**, not an HTTP status.

### Sub-Q4 — abort, billing, and disambiguation (RESOLVED)
- Propagating `HttpContext.RequestAborted` into the SDK call surfaces as `OperationCanceledException` / `TaskCanceledException` (`TaskCanceledException` derives from `OperationCanceledException`; one `catch (OperationCanceledException)` covers both).
- **Aborting does NOT reliably stop upstream token billing.** Anthropic's Help Center ("How will I be billed for Claude API use?", `support.anthropic.com/en/articles/8114526`) states: *"you will be charged if your client disconnects or times out in the middle of an API call that was on track to be successful."* Cancellation is **best-effort**: it stops local enumeration immediately and closes the HTTP connection, but there is no separate "stop generating" control frame — the server only stops when it notices the closed connection, and you are billed for tokens generated up to that point. So `RequestAborted` propagation is for **resource hygiene and stopping local work**, not cost control. (Anthropic does not publish exact server-side stop timing; the "billed up until the stop point" principle is confirmed by the analogous refusal rule and corroborated by community reports such as anthropics/claude-code #43295 / #38905.)
- **Disambiguation:** a **normal client-gone abort** is `OperationCanceledException` with `HttpContext.RequestAborted.IsCancellationRequested == true` → **not-an-error** (do not log as a server fault; do not return 500). A **genuine mid-stream transport drop** surfaces as `AnthropicIOException` (or a "stream ended before message_stop"-class failure) with `RequestAborted` **not** signaled → **Transient**.

### Sub-Q5 — retry scope and double-billing (RESOLVED)
- SDK retry applies **pre-first-byte only**. A mid-stream `AnthropicSseException` is **not** retried by the SDK. Therefore the SDK cannot double-bill or double-emit tokens mid-stream — a retried streaming call only re-issues before any SSE event has been emitted.
- A **second retry layer is undesirable.** Wolverine retry / `Microsoft.Extensions.Http.Resilience` / M.E.AI middleware wrapping the streamed call would re-issue the whole request *after* partial tokens already streamed to the client — causing double-billing and re-emitting a fresh response over a wire that already showed partial output. DEC-073's "SDK-only retry (`MaxRetries=2`), no second layer" posture (see batch-28b) **carries over unchanged** to streaming.

---

## Details — THE MAPPING TABLE (centerpiece)

| SDK streaming condition | Exception type and/or stop reason | DEC-073 classification | SSE frame to the wire | Persistence outcome |
|---|---|---|---|---|
| **Clean completion** | `message_delta` `stop_reason="end_turn"` → `ChatFinishReason.Stop` | not-an-error | token frames, then `done` | append **normal assistant turn** |
| **Stop sequence hit** | `stop_reason="stop_sequence"` → `Stop` | not-an-error (complete) | token frames, then `done` | append **normal assistant turn** |
| **max_tokens truncation mid-stream** | `stop_reason="max_tokens"` → `ChatFinishReason.Length` (**NOT an exception**) | **errored-turn** (diverges from PR4) | `error` frame, `Retryable=true`, no `RetryAfterSeconds` | **persist errored marker**, discard partial, re-send with fresh idempotency GUID |
| **model_context_window_exceeded mid-stream** | `stop_reason="model_context_window_exceeded"` (Sonnet 4.5+) → treated like Length | **errored-turn** | `error` frame, `Retryable=false` (won't fit; reduce context) | persist errored marker, discard partial |
| **overloaded_error mid-stream** | `error` event `overloaded_error` → `AnthropicSseException` | **Transient** (529-equiv) | `error` frame, `Retryable=true`, `RetryAfterSeconds`=configured default | persist errored marker |
| **Rate-limit (429-equiv) mid-stream** | `error` event `rate_limit_error` → `AnthropicSseException` | **Transient** | `error` frame, `Retryable=true`, `RetryAfterSeconds`=configured default (**no header available mid-stream**) | persist errored marker |
| **invalid_request / authentication / permission mid-stream** | `error` event (`invalid_request_error`/`authentication_error`/`permission_error`) → `AnthropicSseException` | **Permanent** | `error` frame, `Retryable=false` | persist errored marker |
| **Refusal mid-stream** | `stop_reason="refusal"` (HTTP 200; billed up to refusal) | **errored-turn** (handled by post-stream judge/safety, not retried) | `error` frame, `Retryable=false` | persist errored marker, discard partial |
| **Genuine transport drop / connection reset** | `AnthropicIOException` / stream ends before `message_stop`; `RequestAborted` NOT signaled | **Transient** | `error` frame, `Retryable=true` | persist errored marker |
| **Client abort via RequestAborted** | `OperationCanceledException`/`TaskCanceledException`, `RequestAborted.IsCancellationRequested==true` | **not-an-error** | none (connection already gone) | persist nothing (or aborted marker); **never a normal assistant turn** |
| **Pre-first-byte 4xx (≠408/409/429)** | `AnthropicBadRequest`/`Unauthorized`/`Forbidden`/`NotFound`/`UnprocessableEntity` (after SDK retries) | **Permanent** (PR4 rule carries over) | `error` frame (no token streamed yet), `Retryable=false` | persist errored marker (no partial to discard) |
| **Pre-first-byte 408/409/429/5xx** | `AnthropicRateLimitException` / `Anthropic5xxException` (after 2 SDK retries exhausted) | **Transient** | `error` frame; for 429, `RetryAfterSeconds` from **Retry-After header** via owned pipeline | persist errored marker |
| **Per-attempt timeout** | SDK timeout → `TaskCanceledException`/`OperationCanceledException` (token NOT from `RequestAborted`) | **Transient** | `error` frame, `Retryable=true` | persist errored marker |

---

## Details — Recommended StreamAsync error-handling shape

**Throw mid-enumeration; do not invent an in-band sentinel.** This matches the SDK's native throw-on-error behavior and the bridge's rethrow-through-`MoveNextAsync`, and keeps `Transient`/`Permanent` as the single DEC-073 contract.

1. **`ICoachingLlm.StreamAsync`** wraps `SanitizationAuditChatClient.GetStreamingResponseAsync`. Drive the enumerator manually (`MoveNextAsync` inside try/catch, `yield return` outside the try) and, on a caught native exception, rethrow a DEC-073 type:
   - `AnthropicSseException` → classify on the SSE error `type` string: `overloaded_error` / `rate_limit_error` / `api_error` / `timeout_error` → **`TransientCoachingLlmException`**; `invalid_request_error` / `authentication_error` / `permission_error` → **`PermanentCoachingLlmException`**.
   - `AnthropicApiException` (pre-first-byte) → **reuse PR4's status classifier unchanged**: 408/409/429/5xx → Transient; other 4xx → Permanent.
   - `AnthropicIOException` / premature stream end (and `RequestAborted` not signaled) → Transient.
   - `OperationCanceledException` with `RequestAborted.IsCancellationRequested` → **rethrow as-is (not-an-error)**; do NOT wrap as Transient/Permanent.
2. **`max_tokens` is special** — it is **not** an exception. StreamAsync inspects the terminal update's `FinishReason == ChatFinishReason.Length` (equivalently `stop_reason=="max_tokens"`, and likewise `model_context_window_exceeded`/`refusal`) and, per the locked Slice-4B posture, raises the **errored-turn path** (a dedicated truncation/blocked signal) rather than completing normally. This is the one place the "throw for all errors" rule needs an explicit detector, because the SDK reports truncation as a clean enumeration end.
3. **The SSE endpoint** (controller piping `IAsyncEnumerable<ChatResponseUpdate>` with response buffering/compression disabled and `RequestAborted` propagated) is the single translation point from exceptions to the flat `Kind=Error` envelope over HTTP 200:
   - emit a `token` frame per text delta;
   - on normal completion → `done` frame, then Marten `FetchForWriting` appends the **normal assistant turn** (only after success);
   - on a thrown DEC-073 exception → terminal `error` frame carrying `Kind=Error`, `Retryable`, `RetryAfterSeconds`, then persist an **errored marker** (never a normal assistant turn);
   - on `OperationCanceledException` + `RequestAborted` → **no frame** (client gone), persist nothing / an aborted marker, do **not** log as a server error;
   - on `max_tokens`/context/refusal truncation → `error` frame, persist errored marker, **discard the partial**.

This respects the abort-only safety posture (no mid-stream corrective splicing, no in-band rewrites; the pre-call SafetyGate and async post-stream judge remain out of scope for this ticket).

### Which PR4 (#167) rules carry over vs diverge
- **Carry over unchanged:**
  - HTTP-status classification for **pre-first-byte** failures (4xx ≠ 408/409/429 → Permanent; 408/409/429/5xx → Transient).
  - Retry-After captured from the response header through the **owned HTTP pipeline** for the **pre-stream** 429.
  - SDK-only retry (`MaxRetries=2`), no second Wolverine / Http.Resilience layer (batch-28b).
- **Diverge for streaming:**
  1. Mid-stream errors have **no HTTP status** → classify on the **SSE error event `type` string**, not status.
  2. `max_tokens` for free-text streamed coaching is the **errored-turn path** (discard partial, persist errored marker) — **not** PR4's "Permanent malformed-output" reasoning (a truncated free-text reply is incomplete, not unparseable like a truncated JSON object) and **not** a normal completion.
  3. **Retry-After cannot be returned as an HTTP header once headers are flushed** → it must be carried in the SSE `error` frame's `Retryable`/`RetryAfterSeconds` fields; mid-stream `rate_limit_error`/`overloaded_error` event bodies carry no retry-after, so use a configured default backoff.
  4. **Abort does not stop billing** — diverges from any naive "cancel = free" assumption.

---

## Recommendations
1. **Implement StreamAsync as throw-based** using the manual-enumerator pattern and the classifier above; add the explicit `ChatFinishReason.Length` / `max_tokens` (+ `model_context_window_exceeded`, `refusal`) detector for the errored-turn path. **Acceptance benchmark:** every streamed failure surfaces as exactly one of `TransientCoachingLlmException` / `PermanentCoachingLlmException` — or the not-an-error abort — matching DEC-073. Truncation never persists a normal assistant turn.
2. **Pin Retry-After handling per-path:** pre-stream 429 → real header via the owned pipeline; mid-stream → configured default in the SSE `error` frame. **Threshold to revisit:** if Anthropic begins emitting a retry-after value inside the SSE `error` event body, switch mid-stream frames to use it.
3. **Do not add a second retry layer.** Keep SDK `MaxRetries=2`; verify no Wolverine / Http.Resilience / M.E.AI retry wraps the streamed call. Re-evaluate only if long coaching replies move to the Batch API.
4. **Treat abort as resource hygiene, not cost control.** Propagate `RequestAborted`; do not assume it caps billing. If cost on abandoned streams becomes material, cap `MaxTokens` and/or shorten coaching turns rather than relying on disconnects.
5. **Persistence discipline (Marten `FetchForWriting` + `IIdempotencyStore`):** append the user turn before stream start; append the normal assistant turn **only** on a clean `done`; on any error / truncation / transport-drop persist the **errored marker**; re-sends use a **fresh idempotency GUID**.
6. **Test targets:** (a) `AnthropicSseException` exposes the structured error `type` reliably (see caveat); (b) abort vs transport-drop disambiguation via `RequestAborted.IsCancellationRequested`; (c) duplicate `FinishReason` updates are idempotent in the terminal-frame logic.

---

## Caveats / Gotchas
- **Retry-After-with-no-header (mid-stream):** once SSE headers are flushed, no HTTP `Retry-After` can be sent, and the `rate_limit_error`/`overloaded_error` SSE event bodies do not include a retry-after. Carry `Retryable` + `RetryAfterSeconds` (configured default) **in the SSE error frame** instead. Only the **pre-stream** 429 path has a true header.
- **max_tokens-as-truncation:** it is `ChatFinishReason.Length` / `stop_reason="max_tokens"`, a *normal* stop reason — easy to mistreat as a clean `done`. For free-text coaching it means a truncated answer → errored-turn path. `model_context_window_exceeded` is a sibling truncation (Sonnet 4.5+ native; earlier models need the `2025-08-26` beta header) and must not be conflated with `max_tokens`.
- **Abort-vs-genuine-drop:** distinguish by `HttpContext.RequestAborted.IsCancellationRequested`. True client abort → not-an-error (suppress; do not 500). Transport drop with the token unsignaled → Transient.
- **Billing-on-abort:** cancellation does **not** stop the meter — *"you will be charged if your client disconnects or times out in the middle of an API call that was on track to be successful"* (Anthropic Help Center). Server-side stop is best-effort with no published exact timing.
- **`AnthropicSseException` error-type opacity:** the structured error type can be lossy when surfaced through SSE — the TypeScript SDK historically rendered the cause as `"[object Object]"` (anthropics/anthropic-sdk-typescript #346). **Verify** the C# `AnthropicSseException` exposes the structured `type`; if not reliable, fall back to matching the message text (`"overloaded"`, `"rate limit"`) and flag this as a test target. The same class of bug bit Go (`apiErr.Response == nil` on streaming errors → empty `errMsg`).
- **Duplicate `FinishReason` updates:** the bridge can yield the finish reason on more than one `ChatResponseUpdate` (observed in microsoft/agent-framework #2740); the classifier / terminal-frame logic must be idempotent.
- **`ping` and unknown events:** tolerate/ignore `ping` and any unknown event types — do not treat them as errors. `ping` can also feed an idle-timeout watchdog to distinguish "server still thinking" from "stream silently stalled."
- **Foundry-only SSE concatenation bug:** SSE event-boundary concatenation parse errors have been reported specifically on the **Azure AI Foundry** Anthropic endpoint (`*.services.ai.azure.com/anthropic`), not the first-party `api.anthropic.com` path this slice targets. If a Foundry route is ever added, expect malformed-frame parse failures and budget defensive splitting.
- **Premature stream end:** a stream that ends before `message_stop` (no terminal `stop_reason`) is a transport failure, not a clean completion — treat as Transient and never persist a normal assistant turn (cf. anthropics/claude-code #38905, where a silently-swallowed abort made the loop look complete).