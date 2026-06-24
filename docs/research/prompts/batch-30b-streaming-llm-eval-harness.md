# R-083: Recording, replaying, and asserting over streamed LLM output in Microsoft.Extensions.AI.Evaluation

## Context

Slice 4B (the streaming conversation core) of RunCoach adds the project's first **streamed** LLM surface: a net-new `ICoachingLlm.StreamAsync` returning `IAsyncEnumerable<ChatResponseUpdate>`, served over SSE-over-`fetch` (R-082 / `batch-30a`). Every other LLM surface in the repo is request/response — plan-generation and adaptation use Pattern-B **structured** calls (`GenerateStructuredAsync<T>`), onboarding uses plain-text `GenerateAsync`. All of them are eval-tested in **Replay mode** against committed disk fixtures (Microsoft.Extensions.AI.Evaluation + a `DiskBasedChatClient`-style cache), so CI makes **zero live Anthropic calls**.

Concrete eval-harness facts established by codebase exploration (design against these; do not re-derive):

- **Replay discipline.** `EvalTestBase` switches Record/Replay; CI runs Replay-only. Fixtures are committed under `tests/eval-cache/`. Recording uses a funded key via `rerecord-eval-cache.sh` (targeted re-record recipe, not a blanket wipe).
- **DEC-074 prompt-hash sentinel.** A committed `Prompts/.prompt-hashes.sha256` manifest + `check-prompt-hashes.sh` + a lefthook hook + the `EvalTestBase` static-ctor backstop tie every fixture to the exact prompt bytes that produced it. The manifest is regenerated **before** the Record run.
- **Delegating chain.** Calls route through `ICoachingLlm` → `SanitizationAuditChatClient : DelegatingChatClient` (which already implements `GetStreamingResponseAsync`) → the M.E.AI caching/replay layer → the Anthropic SDK. The cache key must stay byte-stable for committed fixtures to replay.
- **Existing voice/safety eval pattern (Slice 4A).** Deterministic `VoiceProseGuard` (em-dash / exclamation / banned-phrase) hard-gates prose fields of cached fixtures; an advisory `VoiceRubrics.Restraint` Haiku judge scores register; `TrademarkProseGuard` and the safety evals run over the same fixtures. Slice 4B's streamed output must inherit this exact discipline.
- **Slice 4B also adds a classifier.** The conversational-logging path (confirm-then-commit) uses a Pattern-B **structured** classify-then-extract call (intent `{Question | WorkoutLog}` + a `StructuredLogDraft`) before any streaming — a normal structured call, but new to the conversation flow.

## Research Question

**Primary:** Within Microsoft.Extensions.AI.Evaluation's disk-based record/replay model, how do we record, replay deterministically, and assert over **streamed** (`IAsyncEnumerable<ChatResponseUpdate>`) coaching output — so the Slice 4B streaming conversation eval is offline, byte-stable, and DEC-074-hash-guarded exactly like the plan-gen / adaptation / onboarding evals?

Sub-questions that make the answer actionable:

1. **Does the caching layer cache streaming at all?** Does the M.E.AI response-caching / disk-based `DelegatingChatClient` that backs Replay intercept `GetStreamingResponseAsync`, and if so does it cache the **assembled** response, the individual `ChatResponseUpdate` chunks, or nothing? If streaming is not cached, what is the supported pattern — **buffer-the-stream-then-assert** (drive the streaming API, concatenate updates into the full text, assert on that) versus exercising the same prompt through the cached non-streaming `GetResponseAsync` for the eval and asserting incremental delivery elsewhere?
2. **Cache-key + manifest stability.** Does the cache key derive byte-stably for a streamed request the way it does for `GenerateStructuredAsync`? What, precisely, must change in `rerecord-eval-cache.sh` and the `EvalTestBase` Record path to capture a streamed fixture, and does the DEC-074 `.prompt-hashes.sha256` flow need any change for a streaming prompt?
3. **Assertion architecture for streamed coaching text.** Is the right approach to **buffer the full stream and run the existing deterministic guards (`VoiceProseGuard`, `TrademarkProseGuard`) + the advisory Haiku restraint/quality judge over the assembled text** — i.e. identical to the Slice 4A voice evals once buffered? Is there any eval-layer value in asserting chunk boundaries / incremental delivery, or is incremental delivery purely an integration/E2E (Playwright) concern?
4. **Evaluating the classifier + answer quality.** Does the Pattern-B classify-then-extract call eval exactly like the existing structured evals, or does its position in the conversation flow add anything? How do we deterministically measure **classifier accuracy** (question vs. workout-log vs. ambiguous) and **answer quality** across the canonical open-conversation question shapes (status / injury / schedule / intensity — the taxonomy locked in the Slice 4B design doc, `../plans/mvp-0-cycle/slice-4b-conversation-core.md` § D5 + Unit 7) in Replay?
5. **First-party support to adopt instead of hand-rolling.** Is there M.E.AI.Evaluation support (scenario runners, reporting) for multi-turn or streamed conversations specifically — or current Microsoft / community guidance — that we should adopt rather than hand-rolling a buffer-then-assert adapter? Cross-reference `batch-17c-multi-turn-llm-eval-pattern.md`, `batch-8a-eval-cache-ttl-ci.md` (TTL), and `batch-6a`/`batch-6b` (eval strategy + .NET tooling).

## Why It Matters

Unit 7 (the streaming conversation eval) is the regression gate for streamed coaching **voice, trademark, safety, and answer quality** — the exact surface most exposed to a prompt or model change silently degrading. If streamed output cannot be recorded and replayed offline, either that gate can't run in CI (a coverage hole on the highest-risk LLM surface) or we bolt on a divergent harness that fights the committed-fixture + hash-sentinel discipline the rest of the suite depends on. R-082 explicitly did **not** resolve eval mechanics for streamed output. This is the gate before Unit 7 can be specced.

## Deliverables

- **Concrete recommendation** a solo dev can implement: the record/replay mechanism for streamed output + the assertion architecture + the exact `rerecord-eval-cache.sh` / `EvalTestBase` changes needed.
- **Alternatives considered and why rejected** — native streaming-cache (if it exists) vs. buffer-then-assert vs. split-streaming-integration-from-eval (eval the prompt via the cached non-streaming path, assert streaming delivery in Playwright only).
- **Version pins** — Microsoft.Extensions.AI + Microsoft.Extensions.AI.Evaluation, the Anthropic SDK, xUnit v3 — including any minimum versions a streaming-cache pattern requires.
- **Gotchas** — cache-key stability for streamed requests, DEC-074 manifest interaction, Replay determinism (chunk ordering/timing must not leak into assertions), and the committed-fixture TTL concern from `batch-8a`.
