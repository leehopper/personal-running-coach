# R-084: Anthropic SDK streaming error/stop-reason surface and its mapping onto the DEC-073 failure hierarchy

## Context

Slice 4B exposes the existing `SanitizationAuditChatClient.GetStreamingResponseAsync` through a net-new `ICoachingLlm.StreamAsync` (R-082 / `batch-30a`), served over SSE. DEC-073 requires **every** `ICoachingLlm` failure to surface as `TransientCoachingLlmException` / `PermanentCoachingLlmException`, classified by HTTP status (incl. non-leaf 408/409), per-attempt timeout, `max_tokens` truncation, and malformed output — with raw `Retry-After` captured through the owned HTTP pipeline, **SDK-only retry** (`MaxRetries = 2`, no second Wolverine/`Http.Resilience` layer), and a flat `Kind=Error` HTTP-200 envelope. That classifier was built and shipped for the **request/response structured** path in Slice 3 PR4 (#167).

Streaming moves the failure surface in ways the PR4 classifier was not designed for, and these are the concrete unknowns to resolve:

- An error can arrive **after** the HTTP 200 + headers, **mid-token-enumeration** — there is no fresh HTTP status to classify on.
- A `Retry-After` for a mid-stream rate-limit / overload **cannot be returned in a response header** once streaming has begun.
- `max_tokens` is a normal stream **stop reason**, not an exception — but for a free-text coaching reply it means a **truncated answer**, and Slice 4B's brainstorm locked "**discard the partial, persist an explicitly errored turn marker, never present a partial as complete**".
- The SSE endpoint must propagate `HttpContext.RequestAborted` into the SDK call to **stop token billing on client disconnect**, and must not mistake a normal client-gone abort for a server error.

## Research Question

**Primary:** How does the Anthropic .NET SDK (current pinned major) surface streaming errors and stop-reasons via its streaming API and through the Microsoft.Extensions.AI `GetStreamingResponseAsync` bridge, and how should `ICoachingLlm.StreamAsync` map each condition onto the **DEC-073 `Transient`/`Permanent` hierarchy** and the **SSE typed error frame** — covering mid-enumeration transport failures, `overloaded_error`/rate-limit during a stream, `max_tokens` truncation, and client abort?

Sub-questions that make the answer actionable:

1. **Mid-stream error delivery.** When the streaming API emits an error (e.g. `overloaded_error`) after the 200 and some token deltas, does the SDK **throw mid-enumeration** of the `IAsyncEnumerable`, surface an error/`message_delta` stop event in-band, or both? Which exception type(s)? How does the M.E.AI bridge propagate it through `ChatResponseUpdate` enumeration?
2. **Transient/permanent classification without a fresh status.** How do we reuse the PR4 status-based classifier when the failure arrives mid-stream and the 200 already went out? Are the streaming error events **typed** (`overloaded_error` → transient; `invalid_request_error` → permanent) so we can classify on the event rather than an HTTP status? Where does `Retry-After` live for a mid-stream rate-limit, and since it can't be a response header, how should the **SSE error frame** carry `Retryable` / `RetryAfterSeconds`?
3. **Stop reasons vs. errors, and the truncation posture.** Enumerate the streaming stop reasons (`end_turn`, `max_tokens`, `stop_sequence`, …). PR4 treats `max_tokens` truncation as a **Permanent malformed-output** failure for structured output; is that right for a **free-text streamed coaching reply** where text has already streamed to the client? Reconcile the answer with the locked Slice 4B posture: **discard the partial, persist an errored marker, re-send with a fresh idempotency GUID** — i.e. confirm whether `max_tokens` mid-stream should map to the errored-turn path rather than a normal completion.
4. **Client abort vs. genuine transport drop.** Confirm `HttpContext.RequestAborted` → cancellation of the SDK streaming call surfaces as `OperationCanceledException` (normal client-gone, **not** an error to envelope), how to distinguish that from a real mid-stream transport drop, and whether aborting **actually stops token billing** upstream.
5. **SDK retry semantics on a streaming call.** Does the SDK's built-in `MaxRetries` apply to a streaming request, and at what point — only **pre-first-byte**, or can it retry mid-stream? Confirm no second retry layer is needed or desirable (DEC-073), and that a retried streaming call neither double-bills nor double-emits tokens.

## Why It Matters

This is the failure-surface contract for **Unit 1 (streaming adapter)** and **Unit 2 (SSE endpoint)**. Getting it wrong yields one of three bad outcomes: an unhandled mid-stream exception that crashes the response with no typed error frame (the client hangs), a **truncated coaching reply silently presented as complete** (the brainstorm's HARD safety concern), or a divergent second retry/error path that violates DEC-073. R-082 covered transport, not the exception taxonomy for streamed failures. This is verifiable largely **against the live SDK** rather than a full external research artifact — but it is a genuine unknown that must be pinned before Unit 1 is built, not guessed at the call site.

## Deliverables

- **A concrete mapping table:** SDK streaming condition → exception/stop-reason → DEC-073 classification (`Transient` / `Permanent` / not-an-error) → SSE frame (`token` / `done` / `error` + `Retryable`/`RetryAfterSeconds`) → persistence outcome (assistant-turn-on-complete vs. errored-marker).
- **The recommended `StreamAsync` error-handling shape** — throw mid-enumeration vs. emit an in-band error frame vs. both — and how the endpoint translates it to the wire.
- **Version pins** — Anthropic SDK major, Microsoft.Extensions.AI — and any minimum versions the streaming error/abort semantics require.
- **Gotchas** — `Retry-After`-with-no-header, `max_tokens`-as-truncation, abort-vs-drop disambiguation, and billing-on-abort. Cross-reference `batch-17b-anthropic-sdk-firstparty-vs-bridge.md`, `batch-28b-wolverine6-transient-llm-failure-error-policy.md`, and the shipped PR4 (#167) classifier.
