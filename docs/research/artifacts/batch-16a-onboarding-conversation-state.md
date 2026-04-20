# Event-source onboarding in Marten, project to EF

**Recommendation: pattern (d) — a dedicated Marten event stream per user for onboarding, with a Marten inline `SingleStreamProjection` for the onboarding read model and a separate EF projection that materializes user-facing fields into `UserProfile`.** Onboarding events go in their own stream (`onboarding-{deterministicGuid(userId)}`), NOT in the existing Plan stream. On completion, a command handler starts a fresh Plan stream and appends `PlanGenerated` there. This is the only pattern that simultaneously satisfies Anthropic's prompt-caching preference for byte-stable prefixes, survives refresh/cross-device resume, gives a literal "what did the AI know" audit, stays idiomatic for both Marten and ASP.NET Identity, and leaves a clean seam for the Slice 4 `ConversationTurn` pattern. The other five patterns each lose on at least one dimension you've listed as a hard requirement.

The decision is not close. It becomes close only if you decide to defer Marten for MVP-0 entirely, in which case pattern (b) is the second-best fallback — but since the Plan aggregate is already event-sourced in Marten, that retreat costs more than it saves.

## Why the other five patterns lose

**(a) EF column on `UserProfile`** is the simplest option and loses the most. Mutating a JSONB `Answers` column in place destroys the audit trail ("what exact context produced this plan?") the moment the user edits an answer. It also fights Anthropic's prompt-prefix caching: if you reconstruct the Claude `messages[]` from a mutated snapshot, the prefix hash changes every time a prior answer is corrected, invalidating the cache and forcing a full re-write at your current breakpoint. Retry safety is via optimistic-concurrency tokens, which handle double-submit but not "LLM succeeded, DB commit failed" cleanly.

**(b) Dedicated `OnboardingSession` EF table** is pattern (a) with better isolation and lifecycle. It solves multi-session archival but shares (a)'s core weaknesses: no replay, no audit, and it fights caching unless you explicitly store a history rows table next to it — at which point you've reinvented pattern (d) as EF rather than Marten, and lost all the Marten tooling (tenancy, projections, stream archival, data masking) that you're already paying for.

**(c) Marten event stream per user-onboarding, with `UserProfile` purely derived** wins on audit and replay but forces you to push ASP.NET Identity's `UserProfile` fields through a Marten projection only. That collides with the fact that `UserProfile` is already an EF entity owned by Identity migrations and is read by code paths (settings screens, authorization policies) that have no reason to know Marten exists. It also complicates GDPR: `UserProfile` is the row you delete on account-close, and having it derived from events rather than authoritative makes every Identity-touching query go through a projection.

**(e) Client-side accumulation** is excluded by DEC-044 and the cycle plan's acceptance criteria. The SPA is explicitly untrusted and refresh-prone; shipping the accumulating state through the client would break "reload page mid-onboarding and it still works" the moment a user hard-refreshes and the browser drops the in-memory array. This is also ruled out by multi-device resume.

**(f) Hybrid picked per sub-property** is the shape of the final recommendation, but framed wrong. (d) IS a hybrid: events in Marten, derived state in EF. There's no coherent case for splitting *which answers* go event-sourced versus column-mutated — all onboarding answers share the same audit and replay requirements.

## Capability matrix

| Criterion | (a) EF column | (b) EF session table | (c) Pure Marten | **(d) Marten + EF projection** | (e) Client-side | (f) Per-property hybrid |
|---|---|---|---|---|---|---|
| Resumability on refresh | ✅ | ✅ | ✅ | **✅** | ❌ | ✅ |
| Multi-tab (optimistic concurrency) | ⚠ row-locking | ⚠ row-locking | ✅ stream version | **✅ stream version** | ❌ | ⚠ |
| Cross-device handoff | ✅ | ✅ | ✅ | **✅** | ❌ | ✅ |
| Idempotency on LLM retry | ⚠ ETag only | ⚠ ETag only | ✅ stream version + event headers | **✅ stream version + event headers** | ❌ | ⚠ |
| Replay "what the AI knew" | ❌ | ❌ | ✅ | **✅** | ❌ | ⚠ partial |
| Re-derive `UserProfile` from history | ❌ | ❌ | ✅ but forced | **✅ and optional** | ❌ | ⚠ |
| Per-turn handler complexity | ✅ trivial | ✅ trivial | ⚠ new concept | **⚠ new concept** | ✅ | ❌ most complex |
| GDPR-erasability | ✅ row delete | ✅ row delete | ⚠ needs archival/masking | **✅ `DeleteAllTenantDataAsync(userId)` + EF delete** | n/a | ⚠ |
| `ContextAssembler` integration | ⚠ snapshot only | ⚠ snapshot only | ✅ both snapshot + event log | **✅ both** | ❌ | ⚠ |
| Prompt-cache friendliness (byte-stable prefix) | ❌ mutates | ❌ mutates | ✅ deterministic | **✅ deterministic** | n/a | ❌ |
| Alignment with existing Plan aggregate | ❌ | ❌ | ✅ | **✅** | ❌ | ⚠ |
| Seam for Slice 4 `ConversationTurn` | ❌ | ⚠ | ✅ | **✅ cleanest** | ❌ | ⚠ |

The critical asymmetry: pattern (d) is the only row with no ❌ and no ⚠ on any dimension that Slice 1's acceptance criteria or RunCoach's stated constraints care about. Per-turn handler complexity is the one mild cost, and it's a one-time learning cost you've already paid for the Plan aggregate.

## Why Anthropic's stateless API actively rewards event sourcing

This is the strongest single argument for (d). Anthropic's Messages API is stateless — every call resends the full `messages[]`. Prompt caching is a **pure prefix-hash mechanism**: the docs state "cache hits require 100% identical prompt segments, including all text and images up to and including the block marked with cache control." Cache reads cost 0.1× base input; cache writes cost 1.25× (5-min TTL) or 2× (1-h TTL). On a realistic MVP-0 workload (50 users × 30 turns × Sonnet 4.5, ~8k input tokens/turn), caching drops total cost from roughly $43 to **~$13** — a ~70% saving for essentially one line of configuration.

The catch: caching only helps if your `messages[]` reconstructs **byte-identically** turn after turn. An append-only event log with a deterministic projection function guarantees this. A mutable snapshot does not — any summarization, field edit, or JSON key reorder invalidates the prefix and forces a re-write. Additionally, when you eventually turn on tool use or extended thinking, Anthropic requires you to echo `tool_use`, `tool_result`, and (with tools) `thinking` blocks back **verbatim including their cryptographic `signature` fields**. The event log where each event carries the typed content-block JSON is the storage model that matches this requirement without special cases. A column-per-field row does not.

The practical consequence: **always enable automatic caching** (`cache_control: { type: "ephemeral", ttl: "1h" }` at the top of the request body) from day one; the code cost is trivial and the model cost savings are immediate. Keep a second explicit breakpoint on the system prompt for longer-lived independent caching. Serialize typed content blocks with `System.Text.Json` configured for stable property ordering.

For the .NET layer specifically, the **first-party `Anthropic` NuGet** (v12.x as of April 2026, from `github.com/anthropics/anthropic-sdk-csharp`) is the 2026 default; it implements `Microsoft.Extensions.AI.IChatClient`, so your `ICoachingLlm` adapter can sit over either the raw Anthropic client or MEAI with a config switch.

## Marten-specific: separate streams, deterministic Guid for onboarding

**Onboarding events go in a SEPARATE stream from Plan events.** Marten's documented idiom is one-stream-per-aggregate-instance, and `SingleStreamProjection<TDoc, TId>` assumes this. Commingling `Plan` and `Onboarding` events on a single per-user stream would force both projections to event-filter on the same stream id, block future use of stream-type snapshots (which Marten's docs call out as a planned direction), and confuse archival — you'd want to archive a completed onboarding independently of the user's plans. Since `TenancyStyle.Conjoined` with `tenant_id = userId` already scopes everything per user, there is zero isolation benefit to merging streams.

**Stream id derivation** given `StreamIdentity.AsGuid` is the one subtlety worth getting right:

- **Onboarding** is 1:1 with the user, so derive the stream id deterministically: `onboardingStreamId = DeterministicGuid(userId, "onboarding")` via UUID-v5 shape (SHA-1 of `userId + ":onboarding"` truncated to 16 bytes). This makes `StartStream<Onboarding>(deterministicId, ...)` naturally idempotent — a retry hits a primary-key violation and you handle it as "already started."
- **Plan** is potentially 1:many per user, so use `CombGuidIdGeneration.NewGuid()` per plan (matching what Wolverine's `MartenOps.StartStream` does internally) and store `CurrentPlanId` on the EF `UserProfile` row.

**Projection wiring** uses the Marten 8 `Marten.EntityFrameworkCore` package, which is the 2025-2026 first-class path for projecting events into EF entities. Register `EfCoreSingleStreamProjection<UserProfile, UserDbContext>` for onboarding, inline lifecycle at MVP-0 (no async daemon required at tens of users). Your `UserProfile` entity implements `ITenanted` so the tenant_id is auto-populated. For cross-cutting side effects (sending a welcome email, triggering the plan-generation pipeline), use a Wolverine event subscription via `.IntegrateWithWolverine().PublishEventsToWolverine().PublishEvent<OnboardingCompleted>(...)`. Event Subscriptions (not Event Forwarding) give you strict-order at-least-once delivery with the outbox.

**GDPR** is simpler than it looks thanks to conjoined tenancy: `store.Advanced.DeleteAllTenantDataAsync(userId.ToString(), ct)` wipes every stream, every event, and every Marten-owned projection doc for that tenant in one call. Pair with an EF `UserProfile` delete and you're done. Keep PII out of event payloads where feasible (emit `AnswerCaptured { Topic, NormalizedValue }` rather than echoing free-text user messages into event bodies where practical); if you later must embed PII, use Marten's `AddMaskingRuleForProtectedInformation<T>` and `ApplyEventDataMasking()` API.

## "Next question" ownership: hybrid, deterministic-led

**A deterministic controller picks the topic; the LLM handles phrasing, follow-ups, and structured extraction.** This is the 2026 consensus for fixed-schema intake.

Anthropic's own *Building effective agents* makes this explicit: "workflows offer predictability and consistency for well-defined tasks, whereas agents are the better option when flexibility and model-driven decision-making are needed at scale." RunCoach's six topics are a fixed schema with a known terminal action — textbook workflow territory. LangGraph's `StateGraph`-with-LLM-nodes is the reference implementation of this shape; Vercel AI SDK 6's `ToolLoopAgent` shipped the same idea in TypeScript. Every production intake bot examined converges on the same pattern: code picks the slot, the model generates a question for that slot, structured output extracts the answer, code advances.

Concretely: maintain a static `topics = [PrimaryGoal, TargetEvent, CurrentFitness, WeeklySchedule, InjuryHistory, Preferences]`. The controller picks the first topic whose corresponding field on the projected `OnboardingView` is null or marked `needs_clarification`. Call Claude with a system prompt that describes the topic and the target schema; request structured output via Anthropic tool use (or the `structured-outputs-2025-11-13` beta) so each turn returns `{ extracted: PartialAnswer, reply: string, confidence: number, needs_clarification: bool }`. The controller writes events based on `extracted`, then decides the next topic or re-asks. This keeps the LLM on the job it's good at (natural phrasing, extraction from ambiguous user text) and off the job it's bad at (remembering what it's supposed to cover).

## Completion criteria: deterministic gate, with LLM ambiguity pre-check

**"Onboarding complete" must be deterministic: all required fields present, all validate, no outstanding `needs_clarification` flag.** Because completion triggers full plan generation (a significant, user-visible action), LLM-judged completion is too uncertain. Use the LLM only to surface ambiguity *before* the gate: a per-turn `needs_clarification: bool` plus a final `ready_for_plan` structured-output check that reads the filled slots and returns `{ ready: bool, ambiguities: [{topic, reason}] }`. If the deterministic check passes AND `ready` is true, emit `OnboardingCompleted` and start the Plan stream. If either fails, route back to the controller. This mirrors the evaluator-optimizer pattern but wires the evaluator as a precondition to irreversible action rather than a loop.

## Wiring sketch for Slice 1

**Marten event types on the onboarding stream** (records with stable property order):

```csharp
public record OnboardingStarted(Guid UserId, DateTimeOffset At);
public record TopicAsked(OnboardingTopic Topic, Guid TurnId);
public record UserTurnRecorded(Guid TurnId, JsonDocument ContentBlocks); // typed Anthropic blocks
public record AssistantTurnRecorded(Guid TurnId, JsonDocument ContentBlocks, string ModelId, string? CacheReadTokens);
public record AnswerCaptured(OnboardingTopic Topic, JsonDocument NormalizedValue, double Confidence);
public record ClarificationRequested(OnboardingTopic Topic, string Reason);
public record OnboardingCompleted(Guid UserId, DateTimeOffset At, Guid PlanRequestId);
```

The key move is that `UserTurnRecorded` and `AssistantTurnRecorded` carry the **full typed content blocks** as sent to/returned from Claude. The `ContextAssembler` then has two options per call: (i) snapshot mode — read the `OnboardingView` projection and compose a fresh prompt from structured blocks, or (ii) replay mode — order the turn events and emit the exact `messages[]` for Anthropic. Use (ii) for the primary Claude call (preserves byte-stable prefix → cache hits), use (i) for summary views, re-prompts, and downstream plan generation.

**Marten read model (inline projection):**

```csharp
public class OnboardingView : ITenanted {
    public Guid Id { get; set; }          // = stream id = DeterministicGuid(userId, "onboarding")
    public string TenantId { get; set; }  // = userId, auto-populated
    public OnboardingStatus Status { get; set; }
    public PrimaryGoal? Goal { get; set; }
    public TargetEvent? TargetEvent { get; set; }
    public FitnessSnapshot? Fitness { get; set; }
    public WeeklySchedule? Schedule { get; set; }
    public InjuryHistory? Injuries { get; set; }
    public Preferences? Preferences { get; set; }
    public OnboardingTopic? CurrentTopic { get; set; }
    public int Version { get; set; }       // for optimistic concurrency
}

public class OnboardingProjection : SingleStreamProjection<OnboardingView, Guid> {
    public OnboardingView Create(OnboardingStarted e) => new() { Status = OnboardingStatus.InProgress };
    public void Apply(AnswerCaptured e, OnboardingView v) { /* pattern-match topic → field */ }
    public void Apply(OnboardingCompleted e, OnboardingView v) => v.Status = OnboardingStatus.Completed;
}
```

Register inline: `opts.Projections.Add<OnboardingProjection>(ProjectionLifecycle.Inline);`

**EF projection to `UserProfile`** (via `Marten.EntityFrameworkCore`):

```csharp
public class UserProfileFromOnboardingProjection : EfCoreSingleStreamProjection<UserProfile, AppDbContext> {
    public void Apply(AnswerCaptured e, UserProfile p) { /* map topic to UserProfile column */ }
    public void Apply(OnboardingCompleted e, UserProfile p) => p.OnboardingCompletedAt = e.At;
}
```

This runs in the same transaction as the event append; no async daemon needed at MVP-0.

**Per-turn handler flow** (Wolverine aggregate-handler workflow):

```csharp
[AggregateHandler]
public static async Task<(OnboardingEvents, OutgoingMessages)> Handle(
    SubmitUserTurn cmd,
    OnboardingView view,                 // loaded via FetchForWriting by aggregate id
    ICoachingLlm llm,
    ContextAssembler assembler,
    IIdempotencyStore idempotency)
{
    if (await idempotency.Seen(cmd.IdempotencyKey)) return await idempotency.Replay(cmd.IdempotencyKey);

    var userBlocks = Blocks.FromUserText(cmd.Text);
    var messages  = await assembler.ComposeForClaude(view, appendUser: userBlocks); // event-replay projection
    var response  = await llm.Call(messages, cacheControl: Ephemeral1h);
    await idempotency.Record(cmd.IdempotencyKey, response);

    var events = new List<object> {
        new UserTurnRecorded(cmd.TurnId, userBlocks.AsJson()),
        new AssistantTurnRecorded(cmd.TurnId, response.ContentBlocks, response.ModelId, response.Usage.CacheReadTokens?.ToString())
    };
    if (response.Extracted is { } answer)   events.Add(new AnswerCaptured(answer.Topic, answer.NormalizedValue, answer.Confidence));
    if (response.NeedsClarification)         events.Add(new ClarificationRequested(response.Topic, response.Reason));

    if (Completion.IsSatisfied(view.ApplyAll(events)) && response.ReadyForPlan)
        events.Add(new OnboardingCompleted(view.TenantId.ToGuid(), DateTimeOffset.UtcNow, Guid.NewGuid()));

    return (events, OutgoingMessages.Empty);
}
```

Wolverine/Marten handle `FetchForWriting`, optimistic concurrency on the stream version, and transactional append + projection update. The idempotency table is a plain EF row keyed by `IdempotencyKey` (client-supplied UUID per user action, retained 24-48h).

**On completion, emit `PlanGenerated` to a new Plan stream:**

```csharp
public static async Task Handle(OnboardingCompleted e, IDocumentSession session, ICoachingLlm llm, ContextAssembler assembler) {
    var planId = e.PlanRequestId;  // deterministic from this onboarding run
    var planContext = await assembler.ComposeForPlanGeneration(e.UserId); // reads OnboardingView, not replay
    var plan = await llm.GeneratePlan(planContext);
    session.Events.StartStream<Plan>(planId, new PlanGenerated(planId, e.UserId, plan, DateTimeOffset.UtcNow));
    await session.SaveChangesAsync();
}
```

Wire as a Wolverine event subscription. `PlanGenerated` idempotency is guaranteed by the unique constraint on `mt_streams.id` — retrying with the same `planId` fails cleanly.

## Re-triggering plan generation from settings

The settings action is **option (ii) from the question: regenerate using the stable `UserProfile` + optional new intent**. The recommended pattern supports this trivially because the `OnboardingView` projection and the `UserProfile` EF row are both authoritative after completion. The settings handler:

1. Loads `UserProfile` (or `OnboardingView`) — no replay needed.
2. Optionally accepts a `RegenerationIntent` (e.g., "I got injured, please regenerate" or "I want to target a different race").
3. Calls `ContextAssembler.ComposeForPlanGeneration(userId, intent)`.
4. Starts a new Plan stream with a fresh `planId`.

No onboarding re-run is required, and no rework of the onboarding data model is needed. If the user wants to *edit* specific answers, that's a separate command (`ReviseAnswer(Topic, NewValue)`) that appends `AnswerCaptured` to the existing onboarding stream — preserving audit — and then proceeds to plan regeneration as above. The event log makes the "what changed between these two plans" question directly answerable.

## Seam with Slice 4 `ConversationTurn`

**Onboarding and open-conversation coaching should share infrastructure but keep separate schemas and streams.** They are different data types with different guarantees:

| | Onboarding (Slice 1) | Open coaching (Slice 4) |
|---|---|---|
| Schema | Fixed six-topic typed slots | Free-form message log |
| Completion | Terminal, triggers plan gen | Open-ended |
| Authoritative store | Marten event stream + EF projection | EF `ConversationTurn` append-only rows |
| Caching posture | Replay events → byte-stable prefix | Direct message rows → byte-stable prefix |

The seam is that **both sit behind the same `ContextAssembler` interface**. Slice 4's `ConversationTurn` EF table is the equivalent of the `UserTurnRecorded`/`AssistantTurnRecorded` events, just in EF because open-conversation has no "completion" and no structural projection requirement. The industry survey confirms this is the mainstream pattern: Vercel AI SDK's `Message_v2` table is EF-shaped, append-only, typed-parts-per-row — exactly what Slice 4 wants. Onboarding is more structured because its output feeds plan generation, so it gets the event-sourced treatment; Slice 4 chat is less structured and gets the simpler EF append-only table.

The choice in Slice 1 does not constrain Slice 4. If later you want Slice 4 conversations to also participate in Marten (e.g., for cross-stream projections like "sessions summary"), the `ConversationTurn` EF table can be backed by a Marten projection over a `chat-{userId}-{sessionId}` stream. Start with the simpler EF-only pattern and upgrade if needed — the `ContextAssembler` abstraction absorbs the difference.

## Loose ends worth naming

**Content-block determinism in .NET.** Use `System.Text.Json` with declared property-order records (not `Dictionary<string,object>`) for anything that ends up inside a `tool_use.input` or cached block. Swift/Go had caching failures from randomized key order; .NET is safer but only if you avoid dictionary-based block construction.

**Extended thinking and tool use in the event shape.** Store typed content blocks as JSON, not flat strings. The `AssistantTurnRecorded.ContentBlocks` field must be able to carry `thinking`, `redacted_thinking`, `tool_use`, and `tool_result` blocks verbatim including `signature` fields, even if Slice 1 doesn't use them. Cheap insurance.

**Async daemon.** Keep all projections inline at MVP-0. Marten documents async as the recommendation for multi-stream projections at scale, but tens of users with inline `SingleStreamProjection`s is zero operational overhead and avoids a background service.

**Observability.** Run Langfuse (self-hosted, MIT, OTel-based) or LangSmith with `sessionId = userId`, `traceId = turnId` from day one. The event log gives you replay; the trace gives you latency, token costs, and per-turn cache-hit rates. Both are needed for the "why did the coach give me this plan?" debugging Slice 4+ will require.

**OpenAI Assistants API is deprecated** (hard cutoff August 26, 2026) and is not a viable pattern (c) even if you wanted it. OpenAI's successor is the Responses + Conversations API; Anthropic has no equivalent and isn't building one. This strengthens the case for owning your own event log.

## Conclusion

RunCoach's stack makes the decision unusually clear. You already have Marten + Wolverine in the build, the Plan aggregate is already event-sourced, conjoined tenancy already scopes everything per user, and Anthropic's stateless API combined with prefix-hash prompt caching actively rewards the deterministic-replay shape that event sourcing produces naturally. Pattern (d) costs one extra aggregate type, one deterministic-Guid helper, and one EF projection class, and in exchange buys you idempotent retries, literal replay audit, cross-device resume via user-keyed streams, GDPR erasure in a single tenant-wipe call, and ~70% input-token savings from caching on turn two onward. The "ruthlessly honest" answer is that (a) and (b) are sirens — they look simpler today and cost more every day after. The seam to Slice 4 is clean because onboarding and open-conversation are genuinely different data shapes, and the `ContextAssembler` interface is the right place for that difference to live.

Ship it.