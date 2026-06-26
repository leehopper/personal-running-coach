# Slice 4B Design: Streaming Conversation Core

> **Design + requirements — not an implementation plan.** Captures the "what" and the locked design decisions for the interactive, streaming coaching conversation. The per-piece "how" (endpoint shapes, prompt prose, task breakdown) is written as a spec in a fresh session at build time. Parent: [`./cycle-plan.md`](./cycle-plan.md). Sibling slices: [`./slice-4a-voice-retune.md`](./slice-4a-voice-retune.md) (4A, COMPLETE), 4C (onboarding redesign + km/miles units — not yet written). Supersedes the conversation-core scope sketched in [`./slice-4-conversation.md`](./slice-4-conversation.md).

## Origin: the Slice 4B brainstorm (2026-06-24)

Slice 4 (Open Conversation) was decomposed 2026-06-17 into 4A (voice re-tune, COMPLETE 2026-06-23, DEC-084), **4B (conversation core, this doc)**, and 4C (onboarding redesign + units). With 4A shipped, a `/catchup` brainstorm locked the conversation-core decisions before spec-writing. Four load-bearing decisions were taken by the builder; the rest are recommended defaults to be ratified at the design-review/spec gate.

## Purpose

Turn the read-only Slice 3 "Explain-the-change" panel into an **interactive, streaming coaching conversation**: the user types, the coach answers token-by-token grounded in the user's profile/plan/history, the conversation **persists across plans**, and a conversational message that describes a workout can be **logged through the chat** (after confirmation) so it feeds the existing deterministic adaptation/safety pipeline.

This is LLM *behaviour + plumbing*, not a visual redesign. The pre-MVP visual UI refactor is tracked separately (ROADMAP § Deferred Items).

## Locked context — do not re-open (R-082 + prior DECs)

R-082 (`batch-30a`, integrated 2026-06-16) and the inherited decisions already settle the transport and persistence mechanics. The brainstorm ratifies, it does not re-litigate:

- **Transport = SSE-over-`fetch`** (POST + JSON body + manual `ReadableStream.getReader()` parse, `text/event-stream`). WebSocket / SignalR / native `EventSource` were each evaluated and rejected.
- **Client architecture** = live turn in **local React state** via `fetch`+reader, reconciled into the `getConversationTurns` RTK cache **exactly once on `done`** via `updateQueryData`/`upsertQueryData` (never per-token; `onCacheEntryAdded` rejected for token streams).
- **Persistence ordering** = user turn durable **first**, assistant turn appended **once on completion**; never silently persist a partial as complete. Idempotent via the existing `IIdempotencyStore` (`MartenIdempotencyStore`, co-transactional), keyed on a client-generated GUID.
- **Safety posture** = pre-call deterministic `SafetyGate` on user input + **abort-only** mid-stream (no corrective splicing) + async post-stream judge after persistence. (DEC-079 high-risk subset.)
- **Adapter** = net-new `ICoachingLlm.StreamAsync` over `SanitizationAuditChatClient.GetStreamingResponseAsync`, throwing the DEC-073 `Transient`/`Permanent` hierarchy; `Kind=Error` HTTP-200 envelope; **no second retry layer** (SDK-only retry).
- **Voice/trademark** = streamed output inherits the gruff-direct register: deterministic `VoiceProseGuard` hard gates + Daniels-Gilbert trademark scrubbing before persistence (DEC-084). KEPT-VERBATIM safety/crisis/body-food/anti-toxic invariants apply.
- **Substrate** = event-sourced Marten projections with inline co-transactional projection (DEC-060). The existing `ConversationLogView` is **plan-scoped** (keyed by `PlanId`), holds proactive adaptation/safety turns, and stays as-is.
- **Auth** = `CookieOrBearer` (`__Host-` cookie + antiforgery on the POST); POST not GET, to support the future iOS bearer client.
- **Rendering** = `react-markdown` safe-by-default (no `rehype-raw`/`dangerouslySetInnerHTML`); URL allow-list + CSP backstop.

## Design decisions (brainstorm 2026-06-24)

### D1 — Conversation scope: user-scoped conversation, plan-scoped adaptation (**builder-locked**)

The coach remembers the conversation **across plan regenerations** — RunCoach is "my coach", not "a coach for this plan", honoring the persistent-relationship vision in `CLAUDE.md`.

- A **net-new user-scoped `Conversation` stream/projection** (keyed by `UserId`) holds interactive turns: `UserMessagePosted`, `AssistantMessagePosted` (names indicative; finalized in the spec). It survives plan regeneration.
- The existing **plan-scoped** `ConversationLogView` (proactive adaptation + safety turns) is left untouched — DEC-060 co-transactional atomicity preserved.
- A **composed read endpoint** unions both into one time-ordered timeline: all interactive turns for the user + proactive turns for the **current** plan. The panel renders the merge; PR7's `AdaptationTurn`/`SafetyTurn` components are reused for the proactive turns.
- Rejected: keeping it plan-scoped (conversation evaporates on plan regeneration); re-keying the shipped `ConversationLogView` to `UserId` (an event-sourcing migration of a shipped Slice 3 projection that entangles deterministic safety/adaptation turns with free-form dialogue).

### D2 — Conversation capability: Q&A + conversational logging (**builder-locked**)

The conversation does two things: **answer questions** grounded in plan/profile/recent logs, and **log workouts conversationally** ("did my 5 easy, knee felt tight") that feed the existing adaptation/safety pipeline. (The builder chose this over Q&A-only.) This makes an intent classifier **required**, not optional.

On the deterministic/LLM split (`CLAUDE.md`: "never use LLMs for structured data tasks"): intent classification is genuinely an LLM **judgment** — deciding whether a message should feed data into the deterministic pipeline — which is more than onboarding's bounded NL→structured extraction. What actually preserves determinism here is **D4's confirm-then-commit**, not the onboarding analogy: the LLM's parse is **advisory** until the user confirms via button, so the plan-mutating commit (and everything downstream — `DeviationEngine`/`EscalationClassifier`, pace/zone/ACWR computation) stays deterministic and never fires on an unconfirmed LLM judgment. The LLM proposes; the deterministic pipeline disposes only after an explicit human confirm.

### D3 — Routing: classify-then-route via a structured pre-call (**builder-locked**)

A fast Pattern-B (DEC-058) classification call (Haiku) triages every incoming message into `{Question | WorkoutLog}` and, when it's a log, extracts a `StructuredLogDraft` (distance, duration, pace/RPE, notes, candidate prescribed-workout match).

- **Question** → grounded streaming answer.
- **WorkoutLog** → the confirm-then-commit flow (D4).
- Rejected: tool-use/function-calling (lifts the MVP-0 no-tool-use guard, couples streaming + extraction + the deterministic write-path into one call, harder to eval); an explicit `/log` affordance (loses the conversational-logging magic).
- On classifier failure → `Kind=Error`, user re-sends. **Never guess intent** on a failed classify.

### D4 — Conversational logging is confirm-then-commit (**builder-locked**)

A parsed workout log is **not** auto-committed. The coach echoes the parsed `StructuredLogDraft` as a structured **confirmation card** (Confirm / Edit / Cancel); only on **Confirm** does the log commit and the adaptation pipeline run.

- **Confirm** → the draft flows through the existing `CreateWorkoutLog` path (Slice 2b) → `EvaluateAdaptationHandler` runs synchronously (SafetyGate on the structured log, deviation/escalation, L2 restructure if needed) → proactive adaptation/safety turns append to the **plan** stream (PR7 renders the before/after diff) → **then** the coach streams a short **acknowledgment turn** on the user stream that points to the plan update.
- The ack turn is **LLM-generated free text** (it names the specific outcome — "knee tightness plus pace drift, cutting tomorrow's tempo"), so it inherits the DEC-084 gruff-direct voice lock + `VoiceProseGuard` and is covered by the U7 eval. It streams **after** the synchronous adaptation pipeline has committed its proactive turns, so any Amber safety referral (DEC-081) is already persisted and visible **before** the ack — the ack never preempts or contradicts a safety referral.
- **Edit** → opens the Slice 2b structured form pre-filled with the parsed draft.
- **Cancel** → discards the draft, no commit.
- **Commit is button-driven, not NL "yes"** — keeps the plan-mutating commit deterministic instead of re-classifying a confirmation.
- Rationale: a misparsed log must never silently mutate the plan; confirm-then-commit sidesteps event-sourced undo entirely. Rejected: auto-commit-with-undo (undoing an adaptation means reversing event-sourced plan events — genuinely hard and risky); confidence-gated auto/confirm (needs a calibrated threshold we don't have without live data).

### D5 — Recommended defaults (ratify at the design-review/spec gate)

| Item | Default | Rationale |
|---|---|---|
| Q&A context assembly | Single fixed ~15K assembly (Layer-1 one-liners + recent logs + recent turns); **no per-shape routing** | The classifier already exists for intent; a per-shape context router is an **eval-gated fast-follow** only if a single assembly underperforms across the canonical shapes (status/injury/schedule/intensity) |
| SSE framing | Hand-roll the ~15-line reader + **typed `token`/`done`/`error` frames** + heartbeat comments | `done` is load-bearing for reconcile-once; `error` for the mid-stream affordance; one fewer dependency for a solo dev |
| Mid-stream death | **Discard partial** + persist an explicitly **errored turn marker** (no partial text rendered) + re-send with a **fresh** idempotency GUID | Never present truncated coaching advice as complete (HARD safety); avoids double-billing ambiguity |
| Async post-stream judge | **Advisory/telemetry only** for MVP-0 (no redaction write-path) | A redaction write-path is a large new safety surface, deferred under the DEC-079 high-risk-subset steer ("build the high-risk safety subset now, defer the rest"); DEC-080's zero-re-prompt posture is consistent with this but is not the gating reason |
| Confirmation UX | Structured card, Confirm/Edit/Cancel; Edit pre-fills the Slice 2b form | Deterministic commit; reuses the existing form |
| Conversation ack vs proactive turn | The **plan-stream adaptation turn (PR7) owns the before/after diff**; the conversation ack turn is a short free-text pointer | No duplicated adaptation rendering; the deterministic adaptation turn stays the source of truth |
| Input caps / rate limiting | **Deferred** (personal use), logged as a known gap | Consistent with the existing `WorkoutLog` Notes/Metrics caps backlog |

## Architecture & data flow

**Two streams, one panel.** Plan-scoped `ConversationLogView` (proactive, unchanged) + a new user-scoped `Conversation` stream (interactive), unioned by a composed read endpoint into one ordered timeline.

**Q&A path:**
```
POST /conversation/messages (msg + client GUID)
 → append UserMessagePosted (durable-first, idempotency marker on GUID)
 → SafetyGate(msg): Red→scripted crisis turn (no LLM), done | Amber→referral attached | Green→go
 → classify (Haiku, Pattern-B) → Question
 → ContextAssembler.ComposeForConversationAsync (single ~15K assembly)
 → StreamAsync(coaching-system.v1 conversation register) → SSE token frames (voice+trademark scrubbed)
 → on complete: append AssistantMessagePosted once → done frame {turnId}
 → async post-stream judge (advisory)
 → client reconciles completed turn into RTK cache on `done`
```

**Conversational-logging path (confirm-then-commit):**
```
POST /conversation/messages → durable user turn → SafetyGate → classify → WorkoutLog + StructuredLogDraft
 → coach returns a CONFIRMATION CARD (parsed fields, Confirm/Edit/Cancel) — NOT committed
 → [Confirm] POST /conversation/logs/confirm (draft + GUID)
      → existing CreateWorkoutLog path (Slice 2b)
      → EvaluateAdaptationHandler runs synchronously (SafetyGate on structured log, deviation/escalation, L2 restructure)
      → proactive adaptation/safety turns appended to the PLAN stream (PR7 renders before/after diff)
      → coach streams a short ack turn on the user stream pointing to the plan update
 → [Edit] opens the Slice 2b structured form pre-filled with the draft
 → [Cancel] discards the draft, no commit
```

## Unit decomposition (candidate — ~7 demoable units, Slice-3-sized)

- **U1 — Streaming adapter.** `ICoachingLlm.StreamAsync` over the Anthropic streaming API via `SanitizationAuditChatClient`, DEC-073 typed-exception translation, `RequestAborted` propagation, voice/trademark scrubbing on the stream. *(R-084 resolved — throw-based mapping in § Research findings.)*
- **U2 — SSE endpoint (Q&A).** `POST /conversation/messages`, `text/event-stream`, buffering/compression off, `FlushAsync` per frame, typed `token`/`done`/`error` frames + heartbeat, CookieOrBearer + antiforgery.
- **U3 — User-scoped conversation stream + union read.** New `Conversation` events/projection keyed by `UserId`, user-turn-first + assistant-on-complete persistence via `IIdempotencyStore`, errored-turn marker, composed union read endpoint.
- **U4 — Intent classifier + context assembly.** Pattern-B Haiku classify → `{Question | WorkoutLog + StructuredLogDraft}`, `ContextAssembler.ComposeForConversationAsync` (single-assembly baseline).
- **U5 — Conversational logging (confirm-then-commit).** Confirmation-card draft → `/confirm` → existing `CreateWorkoutLog` + `EvaluateAdaptationHandler` → ack turn; Edit/Cancel.
- **U6 — Frontend streaming UX.** `useCoachStream` hook, interactive composer, live local-state render, reconcile-once on `done`, safe `react-markdown`, confirmation card, mid-stream error + re-send affordance.
- **U7 — Streaming conversation eval.** Replay-mode fixtures across the canonical question shapes + classifier accuracy + voice (`VoiceProseGuard`) / trademark / safety assertions on streamed output. *(R-083 resolved — buffer-then-assert over coalesced fixtures, in § Research findings.)*

PR sequencing is written in the implementation plan; expect a stacked, dependency-ordered set like Slice 3.

## Error handling

- **Mid-stream death** → discard partial, persist an errored turn marker (never a partial-as-complete), re-send with a fresh GUID; the original GUID resolves to the errored marker.
- **Classifier failure** (`Transient`/`Permanent`) → `Kind=Error`, user re-sends; never guess intent.
- **Adaptation failure during commit** → inherits the DEC-080/081 posture (terminal `Kind=Error`; the scripted Amber referral commits per DEC-081).
- **SafetyGate Red mid-conversation** → scripted crisis turn, no LLM.

## Research findings (landed + integrated 2026-06-24 — verified against the installed packages)

Both unknowns are resolved; artifacts at `docs/research/artifacts/batch-30b-*.md` (R-083) and `batch-30c-*.md` (R-084), each carrying a 2026-06-24 verification header. The full detail (R-084's mapping table, R-083's code snippets) lives in the artifacts; the load-bearing takeaways:

**R-084 — streaming exception mapping (informs U1/U2).** Verified against `Anthropic` 12.29.1 + the shipped PR4 classifier.
- **`StreamAsync` is throw-based.** Drive the enumerator manually (`MoveNextAsync` in try/catch, `yield return` outside the try) and rethrow a DEC-073 type on a caught native exception. No in-band sentinel.
- **Mid-stream errors → `AnthropicSseException`** (post-200, no HTTP status). Classify on the SSE error `type` string: `overloaded_error` / `rate_limit_error` / `api_error` / `timeout_error` → **Transient**; `invalid_request_error` / `authentication_error` / `permission_error` → **Permanent**.
- **Pre-first-byte `AnthropicApiException` → reuse PR4's status classifier unchanged** (408/409/429/5xx → Transient; other 4xx → Permanent). `AnthropicIOException` / premature stream-end (with `RequestAborted` not signaled) → Transient.
- **`max_tokens` / `model_context_window_exceeded` / `refusal` are NOT exceptions** — they end the enumeration cleanly (`FinishReason == ChatFinishReason.Length` / a non-null `stop_reason`). This is the one place "throw for all errors" needs an explicit detector → route to the **errored-turn path** (discard partial, errored marker), matching D5's locked posture. PR4 treats `max_tokens` as Permanent malformed-output for structured JSON; streamed free text diverges to errored-turn (a truncated reply is incomplete, not unparseable) — same destination, different route.
- **Client abort** (`RequestAborted` → `OperationCanceledException`, `IsCancellationRequested == true`) → **not-an-error**: no frame, persist nothing / an aborted marker, do not 500 or log as a fault.
- **Retry-After:** pre-stream 429 → real header via the owned pipeline (PR4); mid-stream has no header → carry `Retryable`/`RetryAfterSeconds` (configured default) in the SSE `error` frame.
- **SDK retry is pre-first-byte only** (`MaxRetries=2`); no mid-stream double-bill, and **no second retry layer** (DEC-073 carries over).
- **Divergence to flag:** **abort does NOT stop Anthropic billing** — propagate `RequestAborted` for resource hygiene, not cost. If abandoned-stream cost matters, cap `MaxTokens` rather than relying on disconnects.
- **Open for U1 (test targets):** confirm `AnthropicSseException` exposes the structured error `type` (message-text fallback otherwise — verified the type exists in 12.29.1, but its `type` accessor fidelity is the one unverified point); make the terminal-frame logic idempotent against duplicate `FinishReason` updates; tolerate `ping`/unknown events.

**R-083 — streaming eval mechanics (informs U7).** Verified the mechanism exists in the installed M.E.AI / Evaluation 10.7.0.
- **Streaming is cached byte-stably with zero new infra.** The eval `ResponseCachingChatClient` (a `DistributedCachingChatClient`) coalesces the stream into one `ChatResponse` on record (`CoalesceStreamingUpdates = true`) and re-splits it on replay. A streamed fixture is a **single coalesced `ChatResponse`**, identical in form to the existing plan-gen/adaptation fixtures, replaying deterministically offline.
- **Buffer-then-assert** is the architecture: drive `StreamAsync`, `updates.ToChatResponse().Text`, run `VoiceProseGuard` + `TrademarkProseGuard` (hard) + the advisory `VoiceRubrics.Restraint` Haiku judge over the assembled text — identical discipline to the existing voice evals.
- **The streamed cache key differs from the non-streamed key** (boxed streaming discriminator) → record the fixture **via the streaming surface** (`StreamAsync`), not the non-streaming path; keep `ChatOptions` byte-identical record↔replay.
- **Never assert chunk count / boundaries / timing in the eval layer** (replay chunking is synthetic) — incremental-delivery UX is Playwright-only.
- **DEC-074 manifest:** no flow change — add the streaming system prompt + the Pattern-B classifier prompt to `.prompt-hashes.sha256`, regenerate before Record.
- **Classifier eval** = existing structured-eval discipline: a labelled ground-truth set (the four canonical shapes + workout-log + deliberately ambiguous), a confusion-matrix / zero-regression gate, stable under constrained decoding (NOT `Temperature 0` — the SDK deprecates `Temperature`/`TopP`/`TopK` and the API 400s any non-default value on current models, so the classifier has no sampling control; see DEC-085 § PR3a correction). Treat **"ambiguous" as its own expected class** so the eval asserts the classifier asks for confirmation (aligns with confirm-then-commit). Optionally layer M.E.AI NLP (BLEU/F1, no LLM) and/or Quality LLM-judge evaluators run through the **same cached client**, advisory until baselined.
- **TTL is already solved** — the repo's re-record workflow + `EvalTestBase` already extend the 14-day fixture TTL (batch-8a / DEC-039); streaming fixtures inherit it (run the existing TTL-extension on the new fixtures), so R-083's "big gotcha" is not new work here.

**Verified version facts** (corrects R-083's pin section): SDK = first-party **`Anthropic` 12.29.1** (NuGet id `Anthropic`, Stainless — *not* the community `Anthropic.SDK`); **`Microsoft.Extensions.AI.Evaluation` (+ .Quality/.Reporting) 10.7.0** (CPM-pinned); core `Microsoft.Extensions.AI[.Abstractions]` resolves transitively (10.7.0 available). Both confirmed in `backend/Directory.Packages.props` + the NuGet cache.

## Open items for the spec-writing session

- Exact event names + role-enum members for the user-scoped stream, and whether the composed union read extends the existing `GET /api/v1/conversation/turns` or adds a sibling endpoint.
- The composed-timeline **ordering rule** (the existing GET returns newest-first; a chat composer wants oldest-first conversational flow) and the visual distinction between interactive turns and proactive adaptation/safety turns.
- Async post-stream **judge semantics** — confirm advisory-only for MVP-0 (no follow-up redaction event).
- Whether the `StructuredLogDraft` reuses the Slice 2b create-request contract directly (so Edit→form is a pre-fill, not a re-map) and how prescribed-workout matching is surfaced in the confirmation card.
- Whether the carry-forward `JsonDocument`-in-DTO antipattern / server-driven `SuggestedInputType` items ride 4B or 4C (lean 4C — they are onboarding-flavored; the conversation here is a fresh surface).
- A new decision-log entry (DEC-08x) recording the user-scoped-conversation + conversational-logging + confirm-then-commit decisions, or whether this design doc + the cycle-plan lock suffice (lean: a short DEC, since D1/D2 set product identity).
- Whether 4B needs a `coaching-system.v1` conversation-register prompt change beyond what Slice 4A shipped (4A re-tuned `coaching-system.v1`, which already drives conversation; confirm it needs no further edit, only a context-assembly path).

## How this feeds the spec

1. ~~Land R-083 + R-084 artifacts; integrate findings into this doc.~~ **Done 2026-06-24** — both artifacts landed, verified, and folded into § Research findings. Spec-writing is unblocked.
2. Read this doc + the cycle plan § Captured During Cycle + R-082/R-083/R-084 + the existing conversation code (PR3 `ConversationLogView`/endpoint, PR7 panel) + the LLM adapter + `SafetyGate` + `EvaluateAdaptationHandler` + `ContextAssembler`.
3. Write the spec + PR strategy (house format, working-tree-only under `docs/specs/`).
4. Builder reviews before implementation.
5. Implement the stacked PRs in dependency order.

## Relationship to the cycle plan

The cycle plan's Status block and Slice 4 § Key risks carry the R-082 resolution and the locked conversation-core decisions. This doc elaborates the design without crossing into implementation. If they conflict, the cycle plan wins — update this doc to match. On 4B completion, the cycle plan and ROADMAP Status blocks get updated and a Cycle History row added.
