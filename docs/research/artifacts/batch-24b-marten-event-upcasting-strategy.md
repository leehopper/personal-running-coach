## # Marten 8.32.x Event Upcasting Strategy for RunCoach (Batch 24B)

**Artifact**: `docs/research/artifacts/batch-24b-marten-event-upcasting-strategy.md`
**Status**: ADR-grade research, ready for Slice 1B implementation
**Marten version pinned**: 8.32.x (current GA at time of writing, May 2026)
**Date**: 2026-05-12

---

## TL;DR

- **Use `options.Events.Upcast<TOld, TNew>(Func<TOld, TNew>)` from `Marten.Events` (extension on `IEventStoreOptions`) with *versioned CLR types* as the default strategy, with JSON-document upcasters via `Marten.Services.Json.Transformations` as the fallback when an old CLR type would be ugly to keep alive.** Marten's upcasters intercept events at the deserialization boundary inside `EventMapping`, so they fire *before* both `SingleStreamProjection<TDoc,TId>.Apply/Evolve` and `EfCoreSingleStreamProjection<TDoc,TId,TCtx>.ApplyEvent`. One strategy, both projection styles.
- **Versioned CLR types beat same-name evolution** for RunCoach because (a) Anthropic Pattern-B's byte-stable schema (DEC-058) demands explicit, named contract versions; (b) the V1 record is a frozen POCO with no apply-logic surface, so "5 migrations = 5 old records in `Legacy.V1/V2/...` namespaces" is cheap; (c) `EfCoreSingleStreamProjection.ApplyEvent` is a single `switch(@event.Data)` and accepts the new type uniformly.
- **The 2026 strategy is stable across Marten 8 → 9.** Per Jeremy Miller's "Critter Stack 2026" roadmap (April 29 2026), Marten 9 targets cold-start optimization, AOT, and pulling more code into `JasperFx.Events` — not API breaks in the upcasting surface. The likely migration cost is `using` namespace updates for types that already moved in 8.0 (e.g. `IEvent`, `StreamAction` now live in `JasperFx.Events`).

## ## 1. Executive Summary — Recommended Approach

The recommended approach for Slice 1B is **versioned CLR event types + CLR-typed upcaster lambdas registered on `StoreOptions.Events`**, with the **JSON-document upcaster (`System.Text.Json.JsonDocument`-based, via `Marten.Services.Json.Transformations.SystemTextJson.JsonTransformations`)** held in reserve as a fallback for the narrow case where keeping the old CLR record around would be net-negative for readability (e.g. when the old shape contained a raw `JsonDocument` payload that no longer compiles after dependency removal).

Rationale, in order of decision weight for RunCoach specifically:

1. **Anthropic Pattern-B byte-stable schema (DEC-058) makes "same-name evolution" actively dangerous.** `OnboardingTurnOutput` is a contract negotiated with the model's JSON schema input. Silently filling defaults via a JSON-level upcaster trains a mental model in the codebase that "`AnswerCaptured` always has all current fields," which is exactly the wrong reflex for a 7th `Normalized*` slot landing in Slice 5. Versioned types force every consumer (handlers, projections, JsonSchema generator, Anthropic prompt builder) to acknowledge the schema bump.
2. **DEC-060's dual-write rule means an upcaster sits *upstream* of two apply paths.** `SingleStreamProjection<OnboardingAggregate, Guid>.Evolve(snapshot, id, IEvent e)` and `EfCoreSingleStreamProjection<PlanEntity, Guid, RunCoachDbContext>.ApplyEvent(snapshot, id, IEvent e, dbContext, session)` both receive the same `IEvent.Data` payload. The upcaster materializes the new CLR type once at `EventMapping`-level deserialization (see source citation below), so both projection paths see `AnswerCaptured` (the current shape) regardless of whether the underlying row stored `legacy_v1.answer_captured` JSON.
3. **Five migrations after five quarters = five 10-line `record` types in a `Legacy.Events.V*` namespace.** No apply-logic, no test surface beyond the upcaster, no projection code referencing them. The "codebase bloat" objection to versioned types only bites when V1 carries behavior; RunCoach events are pure data records.
4. **Operational cost is symmetric for both options on cold start** (each upcaster is a delegate invoked per event read), but versioned types are easier to reason about under failure because the dead-letter event records the *old* CLR type name, not a generic JSON parse error.

**Fallback rule:** drop down to `JsonDocument`-based upcasters (the `JsonTransformations.Upcast(JsonDocument => TNew)` form) only when (a) you need to *delete* a property type whose definition is itself being removed from the codebase, or (b) you need to merge two fields into a nested object and the old record would have three properties that exist nowhere else. Note both forms register on the same `options.Events.Upcast(...)` surface; the choice is per-event, not per-store.

**Decision-changing benchmarks (Recommendations §):** revisit this default if any of these become true:
- The codebase accumulates more than ~8 legacy event versions (then JSON-document upcasters reduce CLR clutter).
- Marten 9 ships and changes the `Upcast<TOld,TNew>` extension surface (not currently signaled).
- An Anthropic schema change forces a *splitting* migration (1 event → 2 events): neither built-in form covers that today; you would need a custom `IProjection` shim or the "copy and transform stream" recipe.

## ## 2. Upcasting Surface in Marten 8.32 (Sub-question 1)

Marten 8.32 exposes upcasting as a GA, extension-based API on `IEventStoreOptions`, accessed via `StoreOptions.Events`. The surface is stable since 5.9/5.10 (introduced by Oskar Dudycz, the upcaster being the canonical Marten event-versioning mechanism) and unchanged in shape through 8.x.

**Source-of-truth file** — `src/Marten/Events/EventGraph.cs` (master branch, JasperFx/marten) — declares the public API:

```csharp
public IEventStoreOptions Upcast<TEvent>(
    string eventTypeName,
    JsonTransformation jsonTransformation = null)
    where TEvent : class
{
    return Upcast(typeof(TEvent), eventTypeName, jsonTransformation);
}

// Convenience CLR-typed overloads also on EventGraph:
return Upcast(typeof(TEvent), GetEventTypeName<TOldEvent>(), JsonTransformations.Upcast(upcast));
return Upcast(typeof(TEvent), GetEventTypeName<TOldEvent>(), JsonTransformations.Upcast(upcastAsync));
```

This produces four families of registrations, all rooted at `options.Events`:

| API | Namespace | Use when |
|---|---|---|
| `Events.Upcast<TOld, TNew>(Func<TOld, TNew> upcast)` | `Marten.Events` (extension methods on `IEventStoreOptions` in `Marten.Events.EventStoreOptionsExtensions` / methods on `EventGraph`) | Default. Keep both CLR types; supply a pure function. Default event-type name is the **old** CLR type's snake_case name. |
| `Events.Upcast<TOld, TNew>(Func<TOld, CancellationToken, Task<TNew>> upcastAsync)` | same | Async lookups (network calls, repository hits). N+1 warning applies — every event read calls this. |
| `Events.Upcast<TNew>(string eventTypeName, JsonTransformation transformation)` | transformation built from `Marten.Services.Json.Transformations.SystemTextJson.JsonTransformations.Upcast(Func<JsonDocument, TNew>)` (or the Newtonsoft equivalent under `Marten.Services.Json.Transformations.JsonNet.JsonTransformations` returning `JObject`) | Old CLR type does not / should not exist in the codebase. Stringly-typed but allocation-light. |
| `Events.Upcast<TUpcaster>()` where `TUpcaster : EventUpcaster<TOld, TNew>` or `AsyncOnlyEventUpcaster<TOld, TNew>` | `Marten.Services.Json.Transformations` (base classes) | Class-encapsulated upcasters; preferred when the upcaster needs DI (use the non-generic `Events.Upcast(IEventUpcaster instance)` overload to inject a constructed instance). |

**`Events.AddEventType<T>()` / `AddEventTypes(IEnumerable<Type>)`** (also on `EventGraph`) is the *separate* registration concept — it pre-registers a CLR type with Marten so the async daemon can deserialize before any append happens. It is **not** upcasting; it does no transformation. The Marten docs explicitly recommend pre-registering for production usage where projections may run before any append happens.

**`Events.MapEventType<T>(string)` and `MapEventTypeWithSchemaVersion<T>(int)`** — also on `EventGraph` — control the `type` column value Marten writes for new events. `MapEventTypeWithSchemaVersion` appends `_v{N}` to the snake_case event-type name (e.g. `answer_captured_v2`). This is the *complement* to upcasting: it gives newly appended events a distinct type name so a later upcaster can target the prior name. **Use it as soon as you start versioning, even at V1 (call `MapEventTypeWithSchemaVersion<AnswerCaptured>(1)` from day one) so the type-name space is unambiguous.**

There is no separate "register an `IEventUpcaster<T>`" public marker interface for ad-hoc registration outside these overloads. The base classes `EventUpcaster<TOld, TNew>` (sync) and `AsyncOnlyEventUpcaster<TOld, TNew>` (async) in `Marten.Services.Json.Transformations` are the supported extension surface; the docs and source treat them as the "class form" of the same Upcast API.

**Serializer-level hook**: there is no separate "`JsonNetSerializer` upcaster" or "`STJSerializer` upcaster" surface in Marten 8.32. Upcasting is implemented above the serializer in `Marten.Events.EventMapping` (see `var data = await jsonTransformation.FromDbDataReaderAsync(serializer, reader, 0, token)`), so the **same upcaster registration works for both Newtonsoft.Json and System.Text.Json**. The serializer is chosen via `options.UseSystemTextJsonForSerialization()` (default in V8) or `options.UseNewtonsoftForSerialization()`; the upcaster API does not change.

**Confirming citation (Marten docs, Event Versioning page, V8.x):** "Marten provides extended capabilities around that and enables different styles for handling the upcasting transformations… Upcasting is a process of transforming the old JSON schema into the new one. It's performed on the fly each time the event is read. You can think of it as a pluggable middleware between the deserialization and application logic."

## ## 3. Versioned Types vs Same-Name Evolution — Decision Matrix (Sub-question 2)

| Axis | (a) Versioned types `AnswerCapturedV2` w/ upcaster from V1 | (b) Same name `AnswerCaptured`, JSON-doc upcaster |
|---|---|---|
| **Read-side complexity** | Projection apply method signatures only ever see the *current* CLR type. Switch arms unchanged across versions. | Same — upcaster runs upstream. **Tie.** |
| **Apply-method ergonomics** | Excellent. `case AnswerCaptured:` always means "the latest shape." | Excellent — identical at the apply site. **Tie.** |
| **Write-side ergonomics** | Handler emits the new `AnswerCaptured` type; no need to know about V1. Excellent. | Same. **Tie.** |
| **JsonSchema / Anthropic Pattern-B compatibility (DEC-058)** | **Winner.** The Anthropic schema generator (System.Text.Json `JsonSchemaExporter` or `NJsonSchema`) emits the schema from the current CLR type. Versioned legacy types live in a separate namespace and are never visible to the prompt builder. | Risky. A reflection-based schema generator that walks `AnswerCaptured` will only ever describe the current shape, but if a developer accidentally references the old type's properties (because there is no old type — fields just appear and disappear in JSON), debugging is opaque. |
| **Type-name column hygiene** | Each schema version gets a distinct `mt_events.type` value (`answer_captured`, `answer_captured_v2`). `mt_dotnet_type` distinguishes legacy CLR namespace. SQL forensics trivial. | All rows share `mt_events.type = answer_captured`; you must inspect `data` JSON to know which schema you're looking at. |
| **Old-class burden after 5 migrations** | 5 frozen 10-line records under `RunCoach.Domain.Onboarding.Legacy.V1..V5`. ~50 LOC total, zero behavior. | 5 upcaster delegates of comparable size, but no legacy CLR types at all. Slightly lighter. |
| **Failure-mode forensics** | `EventUpcaster<V1.AnswerCaptured, AnswerCaptured>` throwing puts the *old* CLR type in the exception. The `DeadLetterEvent` row's `EventType` field disambiguates. | Exception says "upcast for `answer_captured` failed parsing `JsonDocument` at `$.NormalizedTypicalSession`." Less precise but still actionable. |
| **Refactor safety** | If someone renames `AnswerCaptured`, the V1 upcaster's `TNew` reference catches it at compile time. | If someone renames a property, the JSON-document upcaster still uses string keys — silent break unless tested. |
| **Splitting / merging events (N→M)** | Not supported by either built-in. Both require `IProjection` shim or copy-and-transform. **Tie (out of scope).** | Same. |

**Verdict for RunCoach:** Option (a) wins on the two axes that matter most for this codebase — Anthropic schema compatibility and SQL forensic clarity — and is no worse on the others. Recommend (a) as the default; allow (b) per-event when keeping the old CLR record would require keeping a deleted dependency alive.

**Operational read** of "how many events live in the codebase after 5 migrations":
- Option (a): 5 legacy records (POCOs, zero behavior) + 5 upcaster lambdas + 5 `MapEventTypeWithSchemaVersion` calls. ~80 LOC including tests.
- Option (b): 0 legacy records + 5 JSON-document upcaster lambdas. ~50 LOC including tests, but with manual key-string maintenance.

Both are sustainable. Neither hits the "this is unmanageable" wall before Slice 10 at the current cadence.

## ## 4. EfCoreSingleStreamProjection Compatibility (Sub-question 3)

**Confirmed: a single upcaster registration intercepts events for both projection styles.** The interception point is `Marten.Events.EventMapping.FoldInline/FoldAsync` (see `src/Marten/Events/EventMapping.cs`), which calls `jsonTransformation.FromDbDataReaderAsync(serializer, reader, 0, token)` *before* the resulting `IEvent.Data` is handed to any projection's apply method.

Concretely:
- For `SingleStreamProjection<TDoc, TId>` (Marten document projections), the apply path is `Apply/Create/ShouldDelete` or the explicit `Evolve(TDoc, TId, IEvent)` / `EvolveAsync(...)` override. Marten constructs the `IEvent` envelope after deserialization, so `@event.Data` is already the upcast new type.
- For `EfCoreSingleStreamProjection<TDoc, TKey, TDbContext>` (the GA 2026 EF Core path, added in Marten 8.23 per Jeremy Miller's March 2026 roadmap post), the apply path is `ApplyEvent(TDoc? snapshot, TKey identity, IEvent @event, TDbContext dbContext, IQuerySession session)`. **The `IEvent` reaches `ApplyEvent` after the exact same `EventMapping` deserialization path** — `EfCoreSingleStreamProjection` is built on top of the same `JasperFx.Events` projection infrastructure used by `SingleStreamProjection`; it does not have its own deserialization layer.
- For `EfCoreMultiStreamProjection<TDoc, TKey, TDbContext>` and `EfCoreEventProjection<TDbContext>`: same story. The `EfCoreEventProjection` docs note it is "a lower-level `IProjection` implementation" but the deserialization step is identical — it receives a fully-constructed `IEvent` whose `Data` has already been through any upcaster.

**Practical implication for DEC-062's registration discipline.** DEC-062 locks the registration as:
- Document projections: `opts.Projections.Add<TProjection>(ProjectionLifecycle.Inline | Async)`
- EF Core projections: `opts.Add(new TEfProjection(), ProjectionLifecycle.Inline | Async)` (the `Add` extension lives on `StoreOptions` and is contributed by `Marten.EntityFrameworkCore`)

The upcaster registration is **orthogonal**. It sits on `opts.Events.Upcast<...>(...)` and is established once per upcasted event type. No projection-side changes are needed when adding an upcaster. **Confirmed by docs review and source inspection — no caveats found.**

**Edge case worth flagging:** `EfCoreEventProjection<TDbContext>` (the low-level event projection, not the aggregate variant) does not participate in the aggregate-tenancy validation that `EfCoreSingleStreamProjection` enforces (per the official EF Core projections page). It still sees upcast events — the upcaster runs at deserialization regardless — but tenant handling moves to your code. RunCoach should not touch `EfCoreEventProjection` for any of the Slice 1–4 projections.

**Inline vs Async lifecycle:** Upcasters run identically in both. The async daemon path goes through the same `EventMapping` deserialization. For inline projections (used during `SaveChangesAsync` for command-side write models per DEC-060's "handler bodies emit events; projections own EF state" rule), the upcaster fires when *replaying* an existing stream during `FetchForWriting<T>` or `FetchLatest<T>` — i.e., the path that materializes the aggregate from history.

## ## 5. Conjoined Tenancy Gotchas (Sub-question 4)

**The upcaster sees the raw event payload, not the tenanted envelope.** `EventMapping` constructs the `IEvent<T>` wrapper *after* deserialization-with-upcasting; the upcaster signature is `Func<TOld, TNew>` (or the `JsonDocument` variant) and receives only the event body. Tenant context (`@event.TenantId`, headers, correlation id) is attached at envelope-construction time, downstream of the upcaster.

**Implication:** Upcasters must be *pure on the event payload alone*. If an upcaster needs tenant-aware lookup (e.g. "look up the user's TimeZone to default a missing `Timezone` field"), it cannot do so within the upcaster signature directly. Two escape hatches exist:

1. **Async upcaster + repository pulled from a captured `IServiceProvider`.** Marten supports `Func<TOld, CancellationToken, Task<TNew>>` upcasters; you can register a class-based `AsyncOnlyEventUpcaster<TOld, TNew>` with constructor-injected dependencies via the `Events.Upcast(IEventUpcaster instance)` overload. The docs explicitly warn this runs per event-read with N+1 risk; in this case the tenant id would have to be embedded in the event body itself (which RunCoach events already do via `UserId`).
2. **Defer enrichment to the projection's `EnrichEventsAsync` hook** (Marten 8.11+, refined in 8.18). This runs *after* upcasting, batched per `EventSlice`, and has full access to `IQuerySession` (tenanted). Use this when the missing value needs cross-document lookup. Don't conflate this with upcasting — `EnrichEventsAsync` modifies the event in-flight for the projection only; it does not change the persisted row, and per the Marten docs it is **not called during `FetchForWriting()` / `FetchLatest()` with Live aggregations**, so it is unsafe for write-model use.

**Conjoined tenancy specifics for projection targets (`ITenanted`):** the projection infrastructure writes the `TenantId` to projected entities automatically when both (a) `opts.Events.TenancyStyle = TenancyStyle.Conjoined` and (b) the projection's TDoc implements `Marten.Metadata.ITenanted`. Marten validates at startup and throws `InvalidProjectionException` if (a) is set but (b) is missing on an `EfCoreSingleStreamProjection` target. **No change is required to this when adding upcasters** — the upcaster operates on event JSON, the tenant column on `mt_events` is independent, and the projection target's `TenantId` is populated post-upcast.

**Practical gotchas for RunCoach:**
- The `mt_events` row carries `tenant_id` in a dedicated column, not in the JSON payload. An upcaster that does `JObject["tenantId"]` finds nothing — that's correct, and you should not change it.
- If you ever need to upcast based on tenant id (e.g. "tenant `runcoach-canary` events use the new shape unconditionally"), you cannot do that inside the upcaster. Branch in the projection's apply method instead.
- The "Global Streams & Projections" feature (Marten 8.5, `opts.Projections.AddGlobalProjection`) is **not relevant** to RunCoach — that pattern stores events with the default tenant id regardless of session tenant. RunCoach's onboarding/plan/conversation streams are per-user-tenanted (each user is a tenant under conjoined tenancy), so global projection registration would defeat the isolation.

## ## 6. Anthropic Pattern-B Schema Overlap (Sub-question 5)

DEC-058 fixes `OnboardingTurnOutput` as a byte-stable JSON schema with six nullable typed `Normalized*` slots. The event `AnswerCaptured(NormalizedPayload: JsonDocument)` stores whatever Anthropic returned. Adding a seventh slot (call it `NormalizedRecoveryDays`) raises a three-way decision:

| Migration target | Action | Recommended? |
|---|---|---|
| Event shape (`AnswerCaptured`) | The event carries `NormalizedPayload: JsonDocument` — by construction it is *already* schema-agnostic. The event itself does not need upcasting when a slot is added. | **No upcaster needed for slot addition.** The JsonDocument payload preserves whatever the model returned. |
| Projection's read of the payload | The projection projects payload fields into typed columns (e.g. `OnboardingAggregate.TypicalSessionMinutes`). When a new slot appears, this is where the projection's Evolve/Apply method must learn the new field. | **Yes — change projection code, not the event.** |
| Both | Only required if the event also carries a typed extracted field (e.g. `AnswerCaptured(NormalizedPayload, TypicalSessionMinutes: int?)`). | **This is where the worked example in §11 applies.** |

**Rule of thumb:** if the event payload is `JsonDocument` (pass-through), the projection owns the schema-extraction logic and the upcaster is only needed when you promote a payload field to an event property (a *typed* field on the event record). If the event is fully typed (e.g. `AnswerCaptured(string Topic, string Answer, int? TypicalSessionMinutes)`), the upcaster runs at the event boundary.

For RunCoach concretely, Slice 1 currently has `AnswerCaptured` carrying typed fields *plus* a `JsonDocument NormalizedPayload`. The upcaster strategy must cover the **typed-field** evolution path; the `JsonDocument` payload is naturally version-tolerant.

**Anti-pattern to avoid:** Using the upcaster to "normalize" a `JsonDocument` payload into typed fields at read time. That couples the upcaster to the Anthropic schema, runs on every event read (N+1), and bypasses the projection's natural extraction step. If a typed slot needs to be derivable from the JsonDocument payload, do it in the projection's Evolve/Apply method (where the result lands on the read model, gets cached, and is queryable).

## ## 7. Regression-Test Pattern (Sub-question 6)

The 2026 canonical pattern is **integration-style testing through Marten itself**, using `IDocumentStore.WaitForNonStaleProjectionDataAsync(timeout)` (introduced in Marten 7.5 and stable through 8.x) to synchronize on the async daemon. Marten's docs explicitly endorse this approach ("the Marten team would recommend to use integration ('social') testing as much as possible and test your projection code through Marten itself so you can feel confident that your projection code will work correctly in production").

For RunCoach the regression test for upcasting needs to **fabricate an old-shape event row** in `mt_events` such that the upcaster has work to do. There are two viable techniques; technique B is preferred for clarity.

**Technique A — Append an old CLR type that has an upcaster registered.** Register the old CLR type with `Events.AddEventType<V1.AnswerCaptured>()` *and* register the upcaster from V1 → current. Now appending a `V1.AnswerCaptured` writes a row with `type = answer_captured` (or `answer_captured_v1` if you used `MapEventTypeWithSchemaVersion`) and reading it through any projection upcasts on the fly. **Caveat:** this only proves the upcaster runs when you append a V1; it does not prove behavior on rows persisted by an *earlier deployed binary* unless the V1 CLR class is kept in the test assembly forever.

**Technique B — Raw SQL INSERT of legacy JSON, then run the projection.** This is the more honest regression test because it mirrors the actual production situation where the old binary persisted JSON that the new binary has never written. Use `IDocumentSession.Connection` (an `NpgsqlConnection`) to execute a parameterized INSERT directly into `mt_events`.

Drop-in xUnit pattern, suitable for `MartenStoreOptionsCompositionTests` companion file:

```csharp
// File: tests/RunCoach.Persistence.Tests/EventUpcasting/AnswerCapturedUpcasterTests.cs
public sealed class AnswerCapturedUpcasterTests : IAsyncLifetime
{
    private IHost _host = null!;
    private IDocumentStore _store = null!;
    private const string TenantId = "user-abc-123";

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>(b =>
        {
            b.ConfigureServices(services =>
            {
                // Marten 8.8+: run async daemon in Solo mode for fast tear-down
                services.MartenDaemonModeIsSolo();
            });
        });
        _store = _host.Services.GetRequiredService<IDocumentStore>();
        await _host.ResetAllMartenDataAsync(); // wipes + restarts projections
    }

    public Task DisposeAsync() => _host.DisposeAsync().AsTask();

    [Fact]
    public async Task v1_answer_captured_row_projects_correctly_after_upcaster()
    {
        var streamId = Guid.NewGuid();
        await using (var session = _store.LightweightSession(TenantId))
        {
            // Append OnboardingStarted normally so the stream exists and version=1 row is set.
            session.Events.StartStream<OnboardingAggregate>(
                streamId,
                new OnboardingStarted(streamId, "user-abc-123", DateTimeOffset.UtcNow));
            await session.SaveChangesAsync();
        }

        // Now hand-write an OLD-SHAPE AnswerCaptured row directly into mt_events.
        // The "old shape" lacks TypicalSessionMinutes.
        const string legacyAnswerCapturedJson = """
        {
          "StreamId": "{0}",
          "TopicId": "weekly_volume",
          "Answer": "around 25 miles a week",
          "CapturedAt": "2026-04-01T10:00:00Z"
        }
        """;
        var json = legacyAnswerCapturedJson.Replace("{0}", streamId.ToString());

        await using (var session = _store.LightweightSession(TenantId))
        {
            var conn = session.Connection!;
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO public.mt_events
                    (seq_id, id, stream_id, version, data, type, timestamp,
                     tenant_id, mt_dotnet_type, is_archived)
                VALUES
                    (nextval('public.mt_events_sequence'),
                     @id, @stream, 2,
                     CAST(@data AS jsonb),
                     'answer_captured',          -- old type name; current code emits answer_captured_v2
                     now(), @tenant,
                     'RunCoach.Domain.Onboarding.Legacy.V1.AnswerCaptured, RunCoach.Domain',
                     false);
                """;
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("stream", streamId);
            cmd.Parameters.AddWithValue("data", json);
            cmd.Parameters.AddWithValue("tenant", TenantId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Force projection rebuild OR wait for async daemon
        await _store.WaitForNonStaleProjectionDataAsync(30.Seconds());

        // Assert: read the projected aggregate via the EF projection's target table
        // (Plan), or via the document projection's stored doc (OnboardingAggregate).
        await using var query = _store.QuerySession(TenantId);
        var aggregate = await query.LoadAsync<OnboardingAggregate>(streamId);

        aggregate.ShouldNotBeNull();
        aggregate.WeeklyVolumeAnswer.ShouldBe("around 25 miles a week");
        // Critically: TypicalSessionMinutes was upcast to null (default), and the projection's
        // apply method handled the null without exploding.
        aggregate.TypicalSessionMinutes.ShouldBeNull();
    }
}
```

**Why this proves the upcaster works:**
1. The legacy JSON has no `TypicalSessionMinutes` field. Without an upcaster, System.Text.Json would (depending on settings) either default to `0` or throw on a non-nullable property — both are wrong behaviors. Asserting `ShouldBeNull()` proves the registered upcaster populated the new shape with the explicit `null` default.
2. `mt_dotnet_type = ...Legacy.V1.AnswerCaptured` ensures Marten's type-name resolution maps to the V1 CLR record, which is the upcaster's `TOld` parameter.
3. Running the full async daemon to "non stale" status (via `WaitForNonStaleProjectionDataAsync`) exercises the production deserialization path, not a unit-test shortcut.

**Companion file for `MartenStoreOptionsCompositionTests`:** add a sibling test that exercises the same upcaster through the **EF Core projection** path by asserting against the EF-projected `PlanEntity` table after appending a stream of events including a V1 `PlanGenerated` (use the same technique B). One assertion per projection style, both backed by the same upcaster registration.

**Fixture utilities to reuse:**
- `AlbaHost.For<Program>` — your production bootstrapping; minimizes drift.
- `services.MartenDaemonModeIsSolo()` (Marten 8.8+) — Solo load distribution makes daemon start/stop fast and avoids advisory-lock races in test parallelism.
- `Host.ResetAllMartenDataAsync()` — disables async projections, wipes data, replays initial data, restarts projections. Standard test-isolation pattern from Marten's own test suite.
- `IDocumentStore.WaitForNonStaleProjectionDataAsync(TimeSpan)` — single-line "wait for the daemon to catch up."
- `TestOutputMartenLogger` (from Marten testing utilities) — pipes Marten's diagnostic logging to xUnit's `ITestOutputHelper` for failure investigation.

Each test ends up at ~60 LOC of meaningful code, well under the 200-LOC budget.

## ## 8. Operational Concerns (Sub-question 7)

**Cold-start cost of the async daemon:** Upcasters are pure delegate invocations registered on `EventGraph` at store construction time. They add **no measurable startup overhead** — the per-upcaster registration is a dictionary insert keyed by event type name. The daemon's cold-start cost is dominated by code generation for projection types (which Marten 8.0 already moved from Roslyn-based codegen to dynamic Lambda compilation with FastExpressionCompiler, dramatically reducing this cost) and by the high-water-mark seek. Upcasters are not on that path.

**Replay cost:** During an async daemon catch-up or projection rebuild, every event row is read and deserialized. An upcaster adds one delegate invocation per event of the upcasted type. For RunCoach's volume (hundreds of events per tester per month at MVP-1), this is negligible (sub-microsecond per event). The docs flag the realistic risk: **N+1 problems if the upcaster makes external calls** (the `AsyncOnlyEventUpcaster` warning). RunCoach should keep upcasters synchronous and dependency-free; any data lookup belongs in `EnrichEventsAsync`.

**Disk size of the events table:** Upcasters change *nothing* on disk. The `mt_events` table stores the original JSON exactly as it was appended. No re-write happens during read. The only schema-level addition Marten makes is in metadata-config columns if you opt into `MetadataConfig.HeadersEnabled` / `CausationIdEnabled` etc. (not related to upcasting).

**Hot-restore implications when a new event type lands:** Marten's `Projections.Errors.SkipUnknownEvents` setting (default `true` for continuous async processing, default `false` for rebuilds) is critical here. The docs explicitly note: "Skipping unknown event types is important for 'blue/green' deployment of system changes where a new application version introduces an entirely new event type." For RunCoach this means:
- A new event type appended by a new binary version is silently ignored by an older still-running daemon — by design, no daemon crash.
- A rebuild on the new binary will refuse to ignore unknown events (catches stale registration); this is the desired strict mode.

**Recommended `StoreOptions.Projections.Errors` configuration for RunCoach** (consistent with Marten's default but explicit, so future developers can see the choice):

```csharp
opts.Projections.Errors.SkipApplyErrors = true;
opts.Projections.Errors.SkipSerializationErrors = true;     // upcaster failures count here
opts.Projections.Errors.SkipUnknownEvents = true;
opts.Projections.RebuildErrors.SkipApplyErrors = false;
opts.Projections.RebuildErrors.SkipSerializationErrors = false;
opts.Projections.RebuildErrors.SkipUnknownEvents = false;
```

This is the recommended default from the Marten docs for the async daemon: lenient during continuous processing (so a bad shard doesn't halt the system), strict during rebuilds (so a corrupted store is loud).

**One-time replay cost of adding an upcaster:** Zero. Marten does **not** re-deserialize every event when you add an upcaster; upcasters fire only when an event is next read. There is no migration step. This is the key operational advantage over the "rewrite-all-streams" alternative.

## ## 9. Failure Modes & Telemetry (Sub-question 8)

**What happens when an upcaster throws.** During inline projection (e.g. `FetchForWriting<OnboardingAggregate>(streamId)` in a command handler), the exception propagates to the caller — the command fails, the transaction rolls back. During async daemon processing, the exception is caught by the daemon's Polly-backed error handler. Behavior depends on `Projections.Errors.SkipSerializationErrors`:
- `true` (recommended for continuous): the event is recorded as a `DeadLetterEvent` in the `mt_doc_dead_letter_event` table (note: docs in different places say `mt_doc_deadletterevent` vs `mt_doc_dead_letter_event`; the latter is the current name in V8.x); processing continues.
- `false` (recommended for rebuilds): the projection shard is paused.

**Ambiguous "old shape" inference.** Marten's CLR-typed upcaster keys on the **event type name** (`mt_events.type` column), not on payload-content shape detection. So if you have:
- Pre-Slice 2 binary appends rows with `type = answer_captured` (no schema-version suffix).
- Slice 2 binary uses `MapEventTypeWithSchemaVersion<AnswerCaptured>(2)`, appending `type = answer_captured_v2`.
- Slice 2 also registers `Upcast<V1.AnswerCaptured, AnswerCaptured>(...)` keyed off the *old* CLR type name (snake_case = `answer_captured`).

This is unambiguous: rows with `type = answer_captured` go through the upcaster; rows with `type = answer_captured_v2` are deserialized directly to the current type. **The non-obvious gotcha:** if you forget to add `MapEventTypeWithSchemaVersion<AnswerCaptured>(2)` from day one on the V1 binary, both old and new rows share `type = answer_captured` and the upcaster will run on new rows too, attempting `V1.AnswerCaptured` deserialization on JSON that has extra fields — likely to succeed by ignoring unknown properties (System.Text.Json default) but giving the new fields back as defaults from V1 type. **Mitigation: call `MapEventTypeWithSchemaVersion` from Slice 1, with version 1, even before any upcaster is needed. This costs ~1 LOC per event type and makes future versioning trivial.**

**Two upcasters targeting the same event type.** Marten's `Upcast` registration is keyed by `(eventTypeName, newCLRType)`. Registering a second upcaster with the same `eventTypeName` **overrides** the first (last-registration-wins; this is implicit in `EventGraph`'s dictionary semantics). There is no built-in chain mechanism. For multi-step migrations (V1 → V2 → V3), Marten's intended pattern is to register a *direct* upcaster from each old version to the current shape, not to chain. The `event-driven.io` reference example shows exactly this:

```csharp
options.Events
    .Upcast<V1.ProductItemAddedToShoppingCart, V3.ProductItemAddedToShoppingCart>(...)   // direct V1→V3
    .Upcast<V2.ProductItemAddedToShoppingCart, V3.ProductItemAddedToShoppingCart>(2, ...) // direct V2→V3
    .MapEventTypeWithSchemaVersion<V3.ProductItemAddedToShoppingCart>(3);
```

**Canonical observability hook.** Marten 8 exposes OpenTelemetry via `options.OpenTelemetry.TrackConnections` (Normal or Verbose) and `options.OpenTelemetry.TrackEventCounters()`. The `Marten` ActivitySource emits spans for connection work; the `Marten` Meter emits `marten.event.append` counters with `event_type` and `tenant.id` tags. **However, there is no built-in upcaster-invocation span.** This is a known gap — the maintainer-authored OTel PR (#2358) was closed without merging in favor of the V7 telemetry approach, which focuses on connections and event-append counters, not deserialization-stage interception.

**Recommended OTel span for upcaster invocations** (your own instrumentation, ~20 LOC in the upcaster class):

```csharp
public sealed class AnswerCapturedV1Upcaster :
    EventUpcaster<Legacy.V1.AnswerCaptured, AnswerCaptured>
{
    private static readonly ActivitySource Source = new("RunCoach.Marten.Upcaster");

    protected override AnswerCaptured Upcast(Legacy.V1.AnswerCaptured oldEvent)
    {
        using var activity = Source.StartActivity("upcast.answer_captured", ActivityKind.Internal);
        activity?.SetTag("upcast.from", "answer_captured_v1");
        activity?.SetTag("upcast.to", "answer_captured");
        activity?.SetTag("upcast.from_clr_type", typeof(Legacy.V1.AnswerCaptured).FullName);
        try
        {
            var result = new AnswerCaptured(
                oldEvent.StreamId,
                oldEvent.TopicId,
                oldEvent.Answer,
                TypicalSessionMinutes: null,   // default for V1
                oldEvent.CapturedAt);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            throw;
        }
    }
}
```

Register the `RunCoach.Marten.Upcaster` source alongside `Marten` in your OTel pipeline:
```csharp
services.AddOpenTelemetry().WithTracing(t => t.AddSource("Marten", "RunCoach.Marten.Upcaster"));
```

A `Counter<long>` named `runcoach.upcaster.invocations{from_type,to_type,outcome}` similarly closes the metric gap. With this in place, "silent upcasting failures" become loud on the dashboard: a non-zero `outcome=error` counter or any error-status spans on `upcast.*` are alertable.

**Cross-check on DeadLetterEvent.** When an upcaster throws and `SkipSerializationErrors = true`, Marten writes a `DeadLetterEvent` row with `Exception` and `Event` columns. This is your *durable* second line of defense — even if OTel collection drops, the failure is in the database. Recommend a daily query: `SELECT COUNT(*) FROM mt_doc_dead_letter_event WHERE event_type LIKE '%answer_captured%'` and dashboard it.

## ## 10. Marten 9 Forward-Compatibility Assessment (Sub-question 9)

**Status of Marten 9 as of May 2026.** Per Jeremy Miller's "Critter Stack 2026" post (April 29, 2026) and the March 18, 2026 roadmap update, Marten 9 is targeted for **Q2 or Q3 2026**, paired with Wolverine 6 and Polecat 2. The stated themes are:
1. Cold-start optimization (eliminate assembly scanning, move `JasperFx.RuntimeCompiler`/Roslyn to dev-time only).
2. AOT compliance — "I didn't think this was ever going to be possible before, but it looks like we can pull this off" (Miller, April 2026).
3. Deduplication of code between Marten and Polecat (more code moving into `JasperFx.Events`).
4. Performance work on `EventLoader`, `Async Daemon`, and possible alternative serializers (MemoryPack experiments, HSTORE for DCB).

**None of these themes touch the upcaster surface.** The relevant code already lives in `JasperFx.Events` (from the V8.0 extraction) — `EventUpcaster<TOld, TNew>`, `AsyncOnlyEventUpcaster<TOld, TNew>`, the `JsonTransformations` static helpers, and the `Upcast` extension methods. The fact that this is already in the shared library is *evidence* that the upcaster API is considered stable and forward-compatible across the Marten 8 → 9 boundary.

**Known migration costs from Marten 8 → 9** (per the official Migration Guide):
- Marten 9 will remove synchronous APIs that result in DB calls (Npgsql 10 dropped them); the upcaster API is already async-capable, no change.
- Some types moved namespaces in 8.0 (e.g. `IEvent`, `StreamAction` → `JasperFx.Events`). Any further moves to 9.0 are not announced, but the pattern suggests `using` statement updates are the worst case.
- AOT compliance work could affect the convention-based projection methods (Apply/Create) — irrelevant to upcasters, which are explicit delegate registrations.

**Marten 9 forward-compat verdict:** the 2026 upcasting strategy is a **safe bet, not a one-way door.** Expected migration cost when 9.x ships:
- **Best case (likely):** zero code change. Upcaster registrations are forward-compatible.
- **Realistic case:** `using` namespace updates in 1–3 files (legacy event types, upcaster classes). 30 minutes of work.
- **Worst case (low probability):** a renamed base class (e.g. `EventUpcaster` → `JasperFxEventUpcaster`). 1–2 hours of search-and-replace. The Marten team's history of providing compatibility shims (e.g. Oakton → JasperFx) makes silent breakage unlikely.

**What would change this verdict:** an announcement that `IEventStoreOptions.Upcast` is being moved to a streaming pipeline model (similar to Axon Framework's `EventUpcasterChain`). No such announcement exists in the 2026 roadmap. **Track Marten's release notes for any RFC about upcaster pipelines before each minor version upgrade past 8.32.**

## ## 11. Worked Example — Adding `TypicalSessionMinutes` to `AnswerCaptured`

**Scenario.** Slice 1 ships `AnswerCaptured(Guid StreamId, string TopicId, string Answer, DateTimeOffset CapturedAt)`. Slice 2 needs to add `int? TypicalSessionMinutes` (extracted from the prior topic's Anthropic output). Existing streams in production have `AnswerCaptured` rows without this field.

### Step 1 — Move the V1 type into a Legacy namespace

```csharp
// File: src/RunCoach.Domain/Onboarding/Legacy/V1/AnswerCaptured.cs
namespace RunCoach.Domain.Onboarding.Legacy.V1;

/// <summary>
/// Shape of AnswerCaptured before Slice 2 added TypicalSessionMinutes.
/// Frozen — DO NOT MODIFY. Only the upcaster touches this type.
/// </summary>
public sealed record AnswerCaptured(
    Guid StreamId,
    string TopicId,
    string Answer,
    DateTimeOffset CapturedAt);
```

### Step 2 — Add the new field to the current type

```csharp
// File: src/RunCoach.Domain/Onboarding/Events/AnswerCaptured.cs
namespace RunCoach.Domain.Onboarding.Events;

public sealed record AnswerCaptured(
    Guid StreamId,
    string TopicId,
    string Answer,
    int? TypicalSessionMinutes,           // NEW
    DateTimeOffset CapturedAt);
```

### Step 3 — Register the upcaster + schema version

```csharp
// File: src/RunCoach.Persistence/Marten/MartenStoreOptions.cs (composition root)
using Marten;
using Marten.Events;
using RunCoach.Domain.Onboarding.Events;
using Legacy = RunCoach.Domain.Onboarding.Legacy;

internal static class OnboardingEventRegistrations
{
    public static StoreOptions ConfigureOnboardingEvents(this StoreOptions opts)
    {
        // 1. Pre-register all event types (helps async daemon, optimizes startup).
        opts.Events.AddEventTypes(new[]
        {
            typeof(OnboardingStarted),
            typeof(TopicAsked),
            typeof(AnswerCaptured),
            typeof(OnboardingCompleted),
            // legacy types must be registered for upcaster routing:
            typeof(Legacy.V1.AnswerCaptured),
        });

        // 2. Tag new AnswerCaptured rows with schema version 2.
        //    Old rows in mt_events remain at type = 'answer_captured' (no suffix).
        opts.Events.MapEventTypeWithSchemaVersion<AnswerCaptured>(2);

        // 3. Upcast V1 (no suffix, written by pre-Slice-2 binaries) -> current.
        opts.Events.Upcast<Legacy.V1.AnswerCaptured, AnswerCaptured>(
            oldEvent => new AnswerCaptured(
                StreamId: oldEvent.StreamId,
                TopicId: oldEvent.TopicId,
                Answer: oldEvent.Answer,
                TypicalSessionMinutes: null,   // explicit default for legacy rows
                CapturedAt: oldEvent.CapturedAt));

        return opts;
    }
}
```

### Step 4 — Projection apply method (Marten document projection)

```csharp
// File: src/RunCoach.Persistence/Projections/OnboardingAggregateProjection.cs
public sealed class OnboardingAggregateProjection
    : SingleStreamProjection<OnboardingAggregate, Guid>
{
    public override OnboardingAggregate Evolve(
        OnboardingAggregate? snapshot, Guid id, IEvent e)
    {
        snapshot ??= new OnboardingAggregate { Id = id };
        switch (e.Data)
        {
            case OnboardingStarted started:
                snapshot.UserId = started.UserId;
                snapshot.StartedAt = started.StartedAt;
                break;

            case AnswerCaptured answer:
                // Reads the CURRENT shape regardless of whether the row was V1 or V2 on disk.
                snapshot.RecordAnswer(answer.TopicId, answer.Answer);
                if (answer.TypicalSessionMinutes is { } minutes)
                    snapshot.TypicalSessionMinutes = minutes;
                break;

            case OnboardingCompleted:
                snapshot.IsComplete = true;
                break;
        }
        return snapshot;
    }
}
```

### Step 5 — EF Core projection apply method (Plan stream)

```csharp
// File: src/RunCoach.Persistence/Projections/PlanProjection.cs
public sealed class PlanProjection
    : EfCoreSingleStreamProjection<PlanEntity, Guid, RunCoachDbContext>
{
    public override PlanEntity? ApplyEvent(
        PlanEntity? snapshot, Guid identity, IEvent @event,
        RunCoachDbContext dbContext, IQuerySession session)
    {
        switch (@event.Data)
        {
            case PlanGenerated generated:
                return new PlanEntity
                {
                    Id = identity,
                    TenantId = @event.TenantId,
                    GeneratedAt = generated.GeneratedAt,
                    // If PlanGenerated also evolves later, the same upcaster pattern applies:
                    // legacy rows reach this ApplyEvent already as the current PlanGenerated CLR type.
                };

            case PlanLinkedToUser linked when snapshot is not null:
                snapshot.UserId = linked.UserId;
                return snapshot;

            default:
                return snapshot;
        }
    }
}
```

### Step 6 — Registration on `StoreOptions` (consistent with DEC-062)

```csharp
services.AddMarten(opts =>
{
    opts.Connection(connectionString);
    opts.UseSystemTextJsonForSerialization();
    opts.Events.TenancyStyle = TenancyStyle.Conjoined;

    opts.ConfigureOnboardingEvents();   // <-- our event/upcaster registrations

    // Document projection (DEC-062-compliant registration):
    opts.Projections.Add<OnboardingAggregateProjection>(ProjectionLifecycle.Inline);

    // EF Core projection (DEC-062-compliant registration via opts.Add extension):
    opts.Add(new PlanProjection(), ProjectionLifecycle.Inline);

    // Error-handling defaults appropriate for production:
    opts.Projections.Errors.SkipApplyErrors = true;
    opts.Projections.Errors.SkipSerializationErrors = true;
    opts.Projections.Errors.SkipUnknownEvents = true;
    opts.Projections.RebuildErrors.SkipApplyErrors = false;
    opts.Projections.RebuildErrors.SkipSerializationErrors = false;
    opts.Projections.RebuildErrors.SkipUnknownEvents = false;
}).AddAsyncDaemon(DaemonMode.HotCold);
```

### Step 7 — The companion regression test

Use the xUnit pattern in §7. Specifically:
- Hand-write a `mt_events` row with `type = 'answer_captured'` (no `_v2`) and JSON missing `TypicalSessionMinutes`.
- Run `WaitForNonStaleProjectionDataAsync`.
- Assert that `OnboardingAggregate.TypicalSessionMinutes` is `null` (upcaster default applied) and `OnboardingAggregate.WeeklyVolumeAnswer` is correctly set.

**Total LOC for the migration**: ~75 lines including projection updates, well under the 200-LOC budget. The upcaster lambda itself is 6 lines. The legacy record is 5 lines.

**Reuse for future migrations**: Slice 3's `PlanAdaptedFromLog` evolution will follow the same 7-step template by analogy — move V1 to Legacy, add the field, register the upcaster, bump the schema version. The pattern is the same regardless of whether the affected projection is a document projection (`OnboardingAggregate`) or an EF Core projection (`Plan`).

# Marten 8.32.x Event Upcasting Strategy for RunCoach (Batch 24B)

**Artifact**: `docs/research/artifacts/batch-24b-marten-event-upcasting-strategy.md`
**Status**: ADR-grade research, ready for Slice 1B implementation
**Marten version pinned**: 8.32.x (current GA as of May 2026)
**Date**: 2026-05-12

---

## TL;DR

- **Use `options.Events.Upcast<TOld, TNew>(Func<TOld, TNew>)` (extension on `IEventStoreOptions` in the `Marten.Events` namespace, implemented in `Marten/Events/EventGraph.cs`) with *versioned CLR types* as the default**, falling back to `JsonDocument`-based upcasters via `Marten.Services.Json.Transformations.SystemTextJson.JsonTransformations` only when keeping the old CLR record alive is net-negative. Marten upcasters intercept events at the `EventMapping`-level deserialization boundary, so they fire *before* both `SingleStreamProjection<TDoc,TId>.Apply/Evolve` **and** `EfCoreSingleStreamProjection<TDoc,TId,TCtx>.ApplyEvent`. One strategy, both projection styles.
- **Versioned CLR types win for RunCoach** because (a) Anthropic Pattern-B's byte-stable schema (DEC-058) demands explicit, named contract versions; (b) frozen V1 POCOs with no apply-logic surface have negligible maintenance cost (~10 LOC per migration); (c) `mt_events.type` column hygiene makes SQL forensics trivial; (d) failure-mode forensics name the actual old CLR type.
- **The 2026 strategy is stable across Marten 8 → 9.** Per Jeremy Miller's "Critter Stack 2026" roadmap (April 29 2026) and the March 18 2026 roadmap update, Marten 9 targets cold-start optimization, AOT compliance, and pulling more code into `JasperFx.Events` — not API breaks. The upcaster types already live in `JasperFx.Events` since Marten 8.0, evidence that the API is considered stable across the major-version boundary. Worst-realistic migration cost: a few `using` namespace updates.

---

## 1. Executive Summary — Recommended Approach

**Default**: versioned CLR event types + CLR-typed upcaster lambdas registered on `StoreOptions.Events`.
**Fallback**: `System.Text.Json.JsonDocument`-based upcasters via `Marten.Services.Json.Transformations.SystemTextJson.JsonTransformations` (or the Newtonsoft `JObject` equivalent under `Marten.Services.Json.Transformations.JsonNet`), used per-event when the old CLR record carries removed dependencies.

Rationale, in order of decision weight for RunCoach:

1. **Anthropic Pattern-B byte-stable schema (DEC-058) makes "same-name evolution" actively dangerous.** Silently filling defaults via a JSON-level upcaster trains the wrong reflex; versioned types force every consumer (handlers, projections, JsonSchema generator, Anthropic prompt builder) to acknowledge schema bumps.
2. **DEC-060's dual-write rule means an upcaster sits *upstream* of two apply paths.** Both `SingleStreamProjection.Evolve(snapshot, id, IEvent e)` and `EfCoreSingleStreamProjection.ApplyEvent(snapshot, id, IEvent e, dbContext, session)` receive the same `IEvent.Data` payload — already upcast.
3. **Five migrations after five quarters = five 10-line `record` types in `RunCoach.Domain.<Stream>.Legacy.V*`.** No behavior, no projection coupling — easy to delete when the corresponding streams are archived.
4. **Failure-mode forensics**: a typed upcaster's exception names the old CLR type and lands in `mt_doc_dead_letter_event` with full type info; a JSON-document upcaster's exception is correct but more opaque.

**Benchmarks that would change this default**:
- 8+ legacy event versions accumulate → JSON-document upcasters reduce clutter.
- Marten 9 changes the `Upcast<TOld,TNew>` extension surface (not currently signaled).
- A *splitting* migration is needed (1 event → 2 events): neither built-in form covers; you would need a custom `IProjection` shim or the "copy-and-transform stream" recipe.

---

## 2. Upcasting Surface in Marten 8.32 (Sub-question 1)

Marten 8.32 exposes upcasting as a GA, extension-based API on `IEventStoreOptions`, accessed via `StoreOptions.Events`. Stable since 5.9/5.10 (introduced by Oskar Dudycz), unchanged in shape through 8.x.

**Source-of-truth file** — `src/Marten/Events/EventGraph.cs` (JasperFx/marten master) — declares:

```csharp
public IEventStoreOptions Upcast<TEvent>(
    string eventTypeName,
    JsonTransformation jsonTransformation = null)
    where TEvent : class
{
    return Upcast(typeof(TEvent), eventTypeName, jsonTransformation);
}

// CLR-typed convenience overloads on EventGraph:
return Upcast(typeof(TEvent), GetEventTypeName<TOldEvent>(), JsonTransformations.Upcast(upcast));
return Upcast(typeof(TEvent), GetEventTypeName<TOldEvent>(), JsonTransformations.Upcast(upcastAsync));
```

The four registration families:

| API | Namespace | Use when |
|---|---|---|
| `Events.Upcast<TOld, TNew>(Func<TOld, TNew>)` | `Marten.Events` extensions on `IEventStoreOptions` / methods on `EventGraph` | Default. Pure function, both CLR types kept. Default event-type name = old CLR type snake_cased. |
| `Events.Upcast<TOld, TNew>(Func<TOld, CancellationToken, Task<TNew>>)` | same | Async lookups. Per-event invocation — N+1 risk. |
| `Events.Upcast<TNew>(string eventTypeName, JsonTransformation)` built via `JsonTransformations.Upcast(Func<JsonDocument, TNew>)` | `Marten.Services.Json.Transformations.SystemTextJson` (or `.JsonNet` for `JObject`) | Old CLR type should not exist; stringly-typed but allocation-light. |
| `Events.Upcast<TUpcaster>()` where `TUpcaster : EventUpcaster<TOld, TNew>` or `AsyncOnlyEventUpcaster<TOld, TNew>` | `Marten.Services.Json.Transformations` base classes | DI-friendly class encapsulation; use `Events.Upcast(IEventUpcaster instance)` overload to inject. |

**Related but distinct APIs on `EventGraph`**:
- `Events.AddEventType<T>()` / `AddEventTypes(IEnumerable<Type>)` — pre-registers a CLR type for async-daemon use. **Not upcasting.** Recommended for production usage where projections may run before any append happens.
- `Events.MapEventType<T>(string)` — overrides the `mt_events.type` value Marten writes for new events.
- `Events.MapEventTypeWithSchemaVersion<T>(int)` — appends `_v{N}` to the snake_case event-type name (e.g. `answer_captured_v2`). **Call this from day one (with version 1) for every event type you might ever evolve.**

**Serializer-level hook**: there is no separate JsonNet/STJ upcaster surface. Upcasting is implemented above the serializer in `Marten.Events.EventMapping` (see `var data = await jsonTransformation.FromDbDataReaderAsync(serializer, reader, 0, token)`); the same upcaster works for both serializers regardless of `options.UseSystemTextJsonForSerialization()` or `options.UseNewtonsoftForSerialization()`.

**Documentation citation (martendb.io/events/versioning.html)**: "Upcasting is a process of transforming the old JSON schema into the new one. It's performed on the fly each time the event is read. You can think of it as a pluggable middleware between the deserialization and application logic."

---

## 3. Versioned Types vs Same-Name Evolution — Decision Matrix (Sub-question 2)

| Axis | (a) Versioned types `AnswerCapturedV2` + V1 upcaster | (b) Same name, JSON-doc upcaster |
|---|---|---|
| **Read-side complexity** | Projection apply only ever sees current CLR type | Same — upcaster runs upstream. **Tie.** |
| **Apply-method ergonomics** | `case AnswerCaptured:` always means current shape | Same. **Tie.** |
| **Write-side ergonomics** | Handler emits current type; no V1 awareness | Same. **Tie.** |
| **JsonSchema / Anthropic Pattern-B compatibility (DEC-058)** | **Winner.** Schema generator emits from current CLR type; legacy types isolated in `Legacy.V*` namespace never visible to prompt builder | Risky. Reflection-based generators describe current shape, but no compile-time anchor for old fields |
| **`mt_events.type` column hygiene** | Each version distinct (`answer_captured`, `answer_captured_v2`); `mt_dotnet_type` distinguishes legacy CLR ns | All rows share `type = answer_captured`; must inspect JSON to disambiguate |
| **Old-class burden after 5 migrations** | 5 frozen 10-line POCOs (~50 LOC, zero behavior) | 5 upcaster delegates of comparable size, no legacy CLR types (~40 LOC) |
| **Failure-mode forensics** | `EventUpcaster<V1.AnswerCaptured, AnswerCaptured>` exception names old CLR type; `DeadLetterEvent.EventType` disambiguates | Exception is correct but less precise |
| **Refactor safety** | Renaming `AnswerCaptured` breaks upcaster at compile time | Renaming a property silently breaks JSON-doc upcaster (string keys) |
| **N→M migrations (splitting/merging events)** | Not supported; requires `IProjection` shim | Same. **Tie (out of scope).** |

**Verdict for RunCoach: (a) wins on the two axes that matter most** (Anthropic schema compatibility + SQL forensics) and ties on the rest.

Operational cost after 5 migrations:
- Option (a): ~80 LOC including tests.
- Option (b): ~50 LOC including tests, but with manual key-string maintenance.

Both sustainable; neither hits the "unmanageable" wall before Slice 10 at current cadence.

---

## 4. EfCoreSingleStreamProjection Compatibility (Sub-question 3)

**Confirmed: a single upcaster registration intercepts events for both projection styles.** Interception point is `Marten.Events.EventMapping.FoldInline/FoldAsync` (see `src/Marten/Events/EventMapping.cs`), which calls `jsonTransformation.FromDbDataReaderAsync(...)` *before* the resulting `IEvent.Data` reaches any projection's apply method.

- `SingleStreamProjection<TDoc, TId>` (Marten document projections) apply path: `Apply/Create/ShouldDelete` or explicit `Evolve(TDoc, TId, IEvent)`/`EvolveAsync(...)`. `@event.Data` is already upcast.
- `EfCoreSingleStreamProjection<TDoc, TKey, TDbContext>` (Marten 8.23+ EF Core path, in the `Marten.EntityFrameworkCore` NuGet) apply path: `ApplyEvent(TDoc? snapshot, TKey identity, IEvent @event, TDbContext dbContext, IQuerySession session)`. Built on the same `JasperFx.Events` projection infrastructure — no separate deserialization layer.
- `EfCoreMultiStreamProjection<TDoc, TKey, TDbContext>` and `EfCoreEventProjection<TDbContext>`: same deserialization path; upcasters fire identically.

**DEC-062 compatibility**:
- Document projections: `opts.Projections.Add<TProjection>(ProjectionLifecycle.Inline | Async)`.
- EF Core projections: `opts.Add(new TEfProjection(), ProjectionLifecycle.Inline | Async)` (the `Add` extension lives on `StoreOptions` and is contributed by `Marten.EntityFrameworkCore`).
- The upcaster registration is **orthogonal**, on `opts.Events.Upcast<...>(...)`. No projection-side changes needed when adding upcasters.

**Edge case**: `EfCoreEventProjection<TDbContext>` (the low-level event projection, not the aggregate variant) does not participate in aggregate-tenancy validation that `EfCoreSingleStreamProjection` enforces. It still sees upcast events — the upcaster runs at deserialization regardless — but tenant handling moves to user code. **RunCoach should not use `EfCoreEventProjection` for Slice 1–4 projections.**

**Inline vs Async lifecycle**: upcasters run identically. Inline projections used during `FetchForWriting<T>`/`FetchLatest<T>` (per DEC-060) fire the upcaster when materializing the aggregate from history.

---

## 5. Conjoined Tenancy Gotchas (Sub-question 4)

**The upcaster sees the raw event payload, not the tenanted envelope.** `EventMapping` constructs the `IEvent<T>` wrapper *after* deserialization-with-upcasting; the upcaster signature is `Func<TOld, TNew>` (or `JsonDocument` variant) and receives only the event body. Tenant context (`@event.TenantId`, headers, correlation id) is attached at envelope-construction time, downstream of the upcaster.

**Implication**: Upcasters must be pure on the event payload alone. If a tenant-aware lookup is needed, two escape hatches exist:

1. **`AsyncOnlyEventUpcaster<TOld, TNew>` with constructor-injected dependencies.** Marten supports `Func<TOld, CancellationToken, Task<TNew>>` upcasters via `Events.Upcast(IEventUpcaster instance)`. **N+1 warning applies** — runs per event read.
2. **Defer enrichment to `EnrichEventsAsync`** (Marten 8.11+, refined in 8.18). Runs after upcasting, batched per `EventSlice`, full `IQuerySession` access (tenanted). **Per Marten docs: NOT called during `FetchForWriting()`/`FetchLatest()` with Live aggregations** — unsafe for write-model use.

**Conjoined tenancy + projection targets (`ITenanted`)**: when `opts.Events.TenancyStyle = TenancyStyle.Conjoined` and projection's TDoc implements `Marten.Metadata.ITenanted`, Marten writes `TenantId` automatically. Validates at startup; throws `InvalidProjectionException` if conjoined event store but TDoc missing `ITenanted` on `EfCoreSingleStreamProjection`. **No change needed when adding upcasters** — they operate on event JSON; `mt_events.tenant_id` is a separate column; the projection target's `TenantId` is populated post-upcast.

**Practical gotchas**:
- `mt_events.tenant_id` is a dedicated column, not in JSON payload. Upcasters doing `JObject["tenantId"]` find nothing — correct, don't change.
- Tenant-id-conditional upcasting is impossible inside the upcaster signature. Branch in the projection apply method.
- "Global Streams & Projections" (Marten 8.5, `opts.Projections.AddGlobalProjection`) is **not relevant** to RunCoach — that forces default tenant id regardless of session tenant. RunCoach's per-user-tenanted streams would break isolation.

---

## 6. Anthropic Pattern-B Schema Overlap (Sub-question 5)

DEC-058 fixes `OnboardingTurnOutput` as a byte-stable JSON schema with six nullable typed `Normalized*` slots. The event `AnswerCaptured(NormalizedPayload: JsonDocument)` stores Anthropic's raw output. Adding a seventh slot raises a three-way decision:

| Migration target | Action | Recommended? |
|---|---|---|
| Event shape (`AnswerCaptured`) | `JsonDocument` payload is already schema-agnostic; no upcaster needed for slot addition | **Skip — JsonDocument naturally tolerant.** |
| Projection's read of the payload | Projection extracts typed columns from JsonDocument; new slot = new extraction code in Evolve/Apply | **Yes — projection code changes.** |
| Both | Required only if event also carries a typed extracted field (e.g. `AnswerCaptured(NormalizedPayload, TypicalSessionMinutes: int?)`) | **Yes — §11 worked example applies.** |

**Rule of thumb**: if event payload is `JsonDocument` (pass-through), the projection owns schema extraction; upcaster only needed when promoting a payload field to a typed event property. If event is fully typed, upcaster runs at the event boundary.

For RunCoach, Slice 1's `AnswerCaptured` carries typed fields **plus** `JsonDocument NormalizedPayload`. Upcaster strategy covers the typed-field evolution; JsonDocument payload is naturally version-tolerant.

**Anti-pattern to avoid**: using the upcaster to normalize `JsonDocument` into typed fields at read time. Couples upcaster to Anthropic schema, runs on every read (N+1), bypasses the projection's natural extraction step. Do field promotion in the projection's apply method instead.

---

## 7. Regression-Test Pattern (Sub-question 6)

2026 canonical pattern: **integration testing through Marten itself**, using `IDocumentStore.WaitForNonStaleProjectionDataAsync(timeout)` (Marten 7.5+, stable through 8.x). Marten docs explicit: "the Marten team would recommend to use integration ('social') testing as much as possible."

**Two techniques** to fabricate an old-shape event row:

**Technique A** — Append an old CLR type that has an upcaster registered. Quick but proves only that upcasting works for newly-appended legacy types; not the production scenario.

**Technique B (preferred)** — Raw SQL INSERT of legacy JSON into `mt_events`, then run the projection. Mirrors production reality where an old deployed binary persisted JSON the new binary has never written.

Drop-in xUnit pattern, suitable for `MartenStoreOptionsCompositionTests` companion file:

```csharp
public sealed class AnswerCapturedUpcasterTests : IAsyncLifetime
{
    private IHost _host = null!;
    private IDocumentStore _store = null!;
    private const string TenantId = "user-abc-123";

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>(b =>
        {
            b.ConfigureServices(services => services.MartenDaemonModeIsSolo());
        });
        _store = _host.Services.GetRequiredService<IDocumentStore>();
        await _host.ResetAllMartenDataAsync();
    }

    public Task DisposeAsync() => _host.DisposeAsync().AsTask();

    [Fact]
    public async Task v1_answer_captured_row_projects_correctly_after_upcaster()
    {
        var streamId = Guid.NewGuid();

        await using (var session = _store.LightweightSession(TenantId))
        {
            session.Events.StartStream<OnboardingAggregate>(
                streamId,
                new OnboardingStarted(streamId, "user-abc-123", DateTimeOffset.UtcNow));
            await session.SaveChangesAsync();
        }

        const string legacyJson = """
        {
          "StreamId": "{0}",
          "TopicId": "weekly_volume",
          "Answer": "around 25 miles a week",
          "CapturedAt": "2026-04-01T10:00:00Z"
        }
        """;
        var json = legacyJson.Replace("{0}", streamId.ToString());

        await using (var session = _store.LightweightSession(TenantId))
        {
            var conn = session.Connection!;
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO public.mt_events
                    (seq_id, id, stream_id, version, data, type, timestamp,
                     tenant_id, mt_dotnet_type, is_archived)
                VALUES
                    (nextval('public.mt_events_sequence'),
                     @id, @stream, 2, CAST(@data AS jsonb),
                     'answer_captured', now(), @tenant,
                     'RunCoach.Domain.Onboarding.Legacy.V1.AnswerCaptured, RunCoach.Domain',
                     false);
                """;
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("stream", streamId);
            cmd.Parameters.AddWithValue("data", json);
            cmd.Parameters.AddWithValue("tenant", TenantId);
            await cmd.ExecuteNonQueryAsync();
        }

        await _store.WaitForNonStaleProjectionDataAsync(30.Seconds());

        await using var query = _store.QuerySession(TenantId);
        var aggregate = await query.LoadAsync<OnboardingAggregate>(streamId);

        aggregate.ShouldNotBeNull();
        aggregate.WeeklyVolumeAnswer.ShouldBe("around 25 miles a week");
        aggregate.TypicalSessionMinutes.ShouldBeNull(); // upcaster default
    }
}
```

**Why this proves correctness**:
1. Legacy JSON has no `TypicalSessionMinutes`. Without an upcaster, STJ would either default to `0` or throw — both wrong. Asserting `ShouldBeNull()` proves the upcaster populated the explicit `null` default.
2. `mt_dotnet_type` ensures Marten's type-name resolution maps to the V1 CLR record (upcaster's `TOld`).
3. `WaitForNonStaleProjectionDataAsync` exercises the production deserialization path.

**Companion test for EF Core projection path**: same Technique B against `PlanGenerated` evolution, asserting against the EF-projected `PlanEntity` table. One assertion per projection style, both backed by the same upcaster registration.

**Fixture utilities**:
- `AlbaHost.For<Program>` — minimizes drift from production bootstrap.
- `services.MartenDaemonModeIsSolo()` (Marten 8.8+) — fast daemon start/stop, no advisory-lock races.
- `Host.ResetAllMartenDataAsync()` — wipe + replay initial data + restart projections.
- `IDocumentStore.WaitForNonStaleProjectionDataAsync(TimeSpan)`.
- `TestOutputMartenLogger` — pipes Marten logging to xUnit `ITestOutputHelper`.

~60 LOC per test, well under 200-LOC budget.

---

## 8. Operational Concerns (Sub-question 7)

**Cold-start cost of the async daemon**: Upcasters are pure delegate invocations registered on `EventGraph` at store construction time — dictionary inserts keyed by event type name. **No measurable startup overhead.** Daemon cold-start is dominated by code generation (Marten 8.0 moved this from Roslyn to dynamic Lambda compilation with FastExpressionCompiler, dramatically reducing it) and high-water-mark seek. Upcasters are not on that path.

**Replay cost**: One delegate invocation per event of the upcasted type during catch-up/rebuild. Negligible for RunCoach's volume (hundreds of events per tester per month). **Realistic risk**: N+1 if the upcaster makes external calls. **Mitigation**: keep upcasters synchronous and dependency-free; defer enrichment to `EnrichEventsAsync` if needed.

**Disk size of events table**: Upcasters change nothing on disk. `mt_events` stores original JSON as appended. No re-write on read.

**Hot-restore implications when a new event type lands**: Marten's `Projections.Errors.SkipUnknownEvents` (default `true` for continuous async, `false` for rebuilds) handles this. Docs note: "Skipping unknown event types is important for 'blue/green' deployment of system changes where a new application version introduces an entirely new event type." For RunCoach:
- New event type appended by new binary is silently ignored by older daemons — by design.
- Rebuild on new binary refuses to ignore unknowns — catches stale registration.

**Recommended `StoreOptions.Projections.Errors` for RunCoach** (explicit, matches recommended defaults):

```csharp
opts.Projections.Errors.SkipApplyErrors = true;
opts.Projections.Errors.SkipSerializationErrors = true;     // upcaster failures count here
opts.Projections.Errors.SkipUnknownEvents = true;
opts.Projections.RebuildErrors.SkipApplyErrors = false;
opts.Projections.RebuildErrors.SkipSerializationErrors = false;
opts.Projections.RebuildErrors.SkipUnknownEvents = false;
```

**One-time replay cost of adding an upcaster**: **zero.** Marten does not re-deserialize on registration; upcasters fire only on next read. No migration step. Key operational advantage over rewrite-all-streams.

---

## 9. Failure Modes & Telemetry (Sub-question 8)

**Upcaster throws during inline projection** (e.g. `FetchForWriting<OnboardingAggregate>`): exception propagates; command fails; transaction rolls back.

**Upcaster throws in async daemon**: caught by Polly-backed error handler. Behavior per `SkipSerializationErrors`:
- `true` (recommended for continuous): event recorded as `DeadLetterEvent` in `mt_doc_dead_letter_event` table; processing continues.
- `false` (recommended for rebuilds): projection shard paused.

**Ambiguous old-shape inference**: Marten keys CLR upcasters on `mt_events.type` (the event type name column), not on payload-content shape detection. Unambiguous when you bump schema versions correctly. **Critical gotcha**: if you forget `MapEventTypeWithSchemaVersion<AnswerCaptured>(2)` on the new binary, both old and new rows share `type = answer_captured`, and the upcaster runs on new rows too — STJ will discard new fields silently. **Mitigation**: call `MapEventTypeWithSchemaVersion<T>(1)` from Slice 1, ahead of any upcaster need. ~1 LOC per event type.

**Two upcasters targeting the same event type**: `Upcast` registration keyed by `(eventTypeName, newCLRType)`; last-registration-wins (dictionary semantics in `EventGraph`). No built-in chaining. Marten's intended pattern: register a **direct** upcaster from each old version to the current shape:

```csharp
options.Events
    .Upcast<V1.AnswerCaptured, AnswerCaptured>(...)             // direct V1→current
    .Upcast<V2.AnswerCaptured, AnswerCaptured>(2, ...)          // direct V2→current
    .MapEventTypeWithSchemaVersion<AnswerCaptured>(3);
```

**Observability**: Marten 8 exposes OTel via `options.OpenTelemetry.TrackConnections` (Normal/Verbose) and `options.OpenTelemetry.TrackEventCounters()`. The `Marten` ActivitySource emits connection-work spans; the `Marten` Meter emits `marten.event.append{event_type, tenant.id}` counters. **There is no built-in upcaster-invocation span** — the maintainer OTel PR (#2358) was closed in favor of the V7 connection-centric telemetry.

**Recommended OTel span for upcaster invocations** (your own instrumentation, ~20 LOC per upcaster):

```csharp
public sealed class AnswerCapturedV1Upcaster :
    EventUpcaster<Legacy.V1.AnswerCaptured, AnswerCaptured>
{
    private static readonly ActivitySource Source = new("RunCoach.Marten.Upcaster");

    protected override AnswerCaptured Upcast(Legacy.V1.AnswerCaptured oldEvent)
    {
        using var activity = Source.StartActivity("upcast.answer_captured", ActivityKind.Internal);
        activity?.SetTag("upcast.from", "answer_captured_v1");
        activity?.SetTag("upcast.to", "answer_captured");
        activity?.SetTag("upcast.from_clr_type", typeof(Legacy.V1.AnswerCaptured).FullName);
        try
        {
            var result = new AnswerCaptured(
                oldEvent.StreamId, oldEvent.TopicId, oldEvent.Answer,
                TypicalSessionMinutes: null, oldEvent.CapturedAt);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            throw;
        }
    }
}
```

Register the source alongside Marten:
```csharp
services.AddOpenTelemetry().WithTracing(t => t.AddSource("Marten", "RunCoach.Marten.Upcaster"));
```

Plus a `Counter<long>` `runcoach.upcaster.invocations{from_type,to_type,outcome}`. With this, silent failures become loud — `outcome=error` counter or error-status spans on `upcast.*` are alertable.

**DeadLetterEvent as durable second line**: when `SkipSerializationErrors=true`, failures land in `mt_doc_dead_letter_event` with `Exception` and `Event` columns. Daily query for dashboard:
```sql
SELECT COUNT(*) FROM mt_doc_dead_letter_event WHERE event_type LIKE '%answer_captured%';
```

---

## 10. Marten 9 Forward-Compatibility Assessment (Sub-question 9)

**Status as of May 2026**: Per Jeremy Miller's "Critter Stack 2026" post (April 29 2026) and the March 18 2026 roadmap update, Marten 9 is targeted for **Q2 or Q3 2026**, paired with Wolverine 6 and Polecat 2. Stated themes:

1. Cold-start optimization (eliminate assembly scanning; move `JasperFx.RuntimeCompiler`/Roslyn to dev-time).
2. AOT compliance — Miller: "I didn't think this was ever going to be possible before, but it looks like we can pull this off."
3. Code deduplication between Marten and Polecat (more code moving into `JasperFx.Events`).
4. Performance work on `EventLoader`, async daemon; possible alternative serializers (MemoryPack experiments, HSTORE for DCB).

**None of these themes touch the upcaster surface.** Upcaster code already lives in `JasperFx.Events` (extracted in V8.0) — `EventUpcaster<TOld, TNew>`, `AsyncOnlyEventUpcaster<TOld, TNew>`, `JsonTransformations`, the `Upcast` extension methods. **The fact that this is already in the shared library is evidence the upcaster API is considered stable and forward-compatible.**

**Known Marten 8 → 9 migration costs** (Migration Guide):
- Marten 9 removes synchronous APIs that result in DB calls (Npgsql 10 dropped them). Upcaster API already async-capable. **No change.**
- Some types moved namespaces in 8.0 (e.g. `IEvent`, `StreamAction` → `JasperFx.Events`). Pattern suggests `using` updates worst case.
- AOT-compliance work could affect convention-based projection methods (Apply/Create) — irrelevant to upcasters, which are explicit delegate registrations.

**Verdict**: the 2026 upcasting strategy is a **safe bet, not a one-way door.** Expected migration cost when 9.x ships:
- **Best case (likely)**: zero code change.
- **Realistic case**: `using` namespace updates in 1–3 files. ~30 minutes.
- **Worst case (low probability)**: renamed base class (`EventUpcaster` → `JasperFxEventUpcaster`). 1–2 hours search-and-replace. Marten team's history of compatibility shims (Oakton → JasperFx) makes silent breakage unlikely.

**What would change this verdict**: announcement that `IEventStoreOptions.Upcast` is moving to a streaming pipeline model (Axon Framework-style `EventUpcasterChain`). No such announcement exists. **Action: track Marten release notes for any upcaster-pipeline RFC before each minor upgrade past 8.32.**

---

## 11. Worked Example — Adding `TypicalSessionMinutes` to `AnswerCaptured`

**Scenario**: Slice 1 ships `AnswerCaptured(Guid StreamId, string TopicId, string Answer, DateTimeOffset CapturedAt)`. Slice 2 adds `int? TypicalSessionMinutes`. Existing production streams have V1 rows.

### Step 1 — Move V1 to Legacy namespace

```csharp
// src/RunCoach.Domain/Onboarding/Legacy/V1/AnswerCaptured.cs
namespace RunCoach.Domain.Onboarding.Legacy.V1;

/// <summary>
/// Shape of AnswerCaptured before Slice 2 added TypicalSessionMinutes.
/// Frozen — DO NOT MODIFY. Only the upcaster touches this type.
/// </summary>
public sealed record AnswerCaptured(
    Guid StreamId,
    string TopicId,
    string Answer,
    DateTimeOffset CapturedAt);
```

### Step 2 — Add new field to current type

```csharp
// src/RunCoach.Domain/Onboarding/Events/AnswerCaptured.cs
namespace RunCoach.Domain.Onboarding.Events;

public sealed record AnswerCaptured(
    Guid StreamId,
    string TopicId,
    string Answer,
    int? TypicalSessionMinutes,   // NEW
    DateTimeOffset CapturedAt);
```

### Step 3 — Register upcaster + schema version

```csharp
// src/RunCoach.Persistence/Marten/MartenStoreOptions.cs
using Marten;
using Marten.Events;
using RunCoach.Domain.Onboarding.Events;
using Legacy = RunCoach.Domain.Onboarding.Legacy;

internal static class OnboardingEventRegistrations
{
    public static StoreOptions ConfigureOnboardingEvents(this StoreOptions opts)
    {
        // 1. Pre-register all event types (helps async daemon).
        opts.Events.AddEventTypes(new[]
        {
            typeof(OnboardingStarted),
            typeof(TopicAsked),
            typeof(AnswerCaptured),
            typeof(OnboardingCompleted),
            typeof(Legacy.V1.AnswerCaptured),  // required for upcaster routing
        });

        // 2. Tag new AnswerCaptured rows with schema version 2.
        opts.Events.MapEventTypeWithSchemaVersion<AnswerCaptured>(2);

        // 3. Upcast V1 (no suffix) -> current.
        opts.Events.Upcast<Legacy.V1.AnswerCaptured, AnswerCaptured>(
            oldEvent => new AnswerCaptured(
                StreamId: oldEvent.StreamId,
                TopicId: oldEvent.TopicId,
                Answer: oldEvent.Answer,
                TypicalSessionMinutes: null,  // explicit default for legacy
                CapturedAt: oldEvent.CapturedAt));

        return opts;
    }
}
```

### Step 4 — Document projection apply method

```csharp
public sealed class OnboardingAggregateProjection
    : SingleStreamProjection<OnboardingAggregate, Guid>
{
    public override OnboardingAggregate Evolve(
        OnboardingAggregate? snapshot, Guid id, IEvent e)
    {
        snapshot ??= new OnboardingAggregate { Id = id };
        switch (e.Data)
        {
            case OnboardingStarted started:
                snapshot.UserId = started.UserId;
                snapshot.StartedAt = started.StartedAt;
                break;
            case AnswerCaptured answer:
                snapshot.RecordAnswer(answer.TopicId, answer.Answer);
                if (answer.TypicalSessionMinutes is { } minutes)
                    snapshot.TypicalSessionMinutes = minutes;
                break;
            case OnboardingCompleted:
                snapshot.IsComplete = true;
                break;
        }
        return snapshot;
    }
}
```

### Step 5 — EF Core projection apply method (Plan stream)

```csharp
public sealed class PlanProjection
    : EfCoreSingleStreamProjection<PlanEntity, Guid, RunCoachDbContext>
{
    public override PlanEntity? ApplyEvent(
        PlanEntity? snapshot, Guid identity, IEvent @event,
        RunCoachDbContext dbContext, IQuerySession session)
    {
        switch (@event.Data)
        {
            case PlanGenerated generated:
                return new PlanEntity
                {
                    Id = identity,
                    TenantId = @event.TenantId,
                    GeneratedAt = generated.GeneratedAt,
                };
            case PlanLinkedToUser linked when snapshot is not null:
                snapshot.UserId = linked.UserId;
                return snapshot;
            default:
                return snapshot;
        }
    }
}
```

### Step 6 — DEC-062-compliant registration on `StoreOptions`

```csharp
services.AddMarten(opts =>
{
    opts.Connection(connectionString);
    opts.UseSystemTextJsonForSerialization();
    opts.Events.TenancyStyle = TenancyStyle.Conjoined;

    opts.ConfigureOnboardingEvents();   // event types + upcasters

    // Document projection (DEC-062 form):
    opts.Projections.Add<OnboardingAggregateProjection>(ProjectionLifecycle.Inline);

    // EF Core projection (DEC-062 form):
    opts.Add(new PlanProjection(), ProjectionLifecycle.Inline);

    // Production error-handling defaults:
    opts.Projections.Errors.SkipApplyErrors = true;
    opts.Projections.Errors.SkipSerializationErrors = true;
    opts.Projections.Errors.SkipUnknownEvents = true;
    opts.Projections.RebuildErrors.SkipApplyErrors = false;
    opts.Projections.RebuildErrors.SkipSerializationErrors = false;
    opts.Projections.RebuildErrors.SkipUnknownEvents = false;
}).AddAsyncDaemon(DaemonMode.HotCold);
```

### Step 7 — Companion regression test

Use the xUnit pattern in §7: hand-write `mt_events` row with `type='answer_captured'` (no `_v2`) missing `TypicalSessionMinutes`; `WaitForNonStaleProjectionDataAsync`; assert `OnboardingAggregate.TypicalSessionMinutes ShouldBeNull()`.

**Total LOC**: ~75 lines including projection updates, well under the 200-LOC budget. Upcaster lambda is 6 lines; legacy record is 5 lines.

**Reuse**: Slice 3's `PlanAdaptedFromLog` evolution follows the same 7-step template by analogy — works identically for document and EF Core projections.

---

## 12. Recommended DEC Entry

> **DEC-NNN: Marten event upcasting strategy**
>
> **Status**: Accepted (2026-05-12)
> **Scope**: Slice 1B and forward
>
> **Decision**: Adopt versioned CLR event types with synchronous CLR-typed upcaster lambdas registered on `StoreOptions.Events.Upcast<TOld, TNew>(Func<TOld, TNew>)` as the default schema-evolution mechanism. Use `MapEventTypeWithSchemaVersion<T>(N)` for every event type, starting at N=1 from initial registration, to keep `mt_events.type` unambiguous across versions. Reserve `JsonDocument`-based upcasters (via `Marten.Services.Json.Transformations.SystemTextJson.JsonTransformations.Upcast`) as a fallback when keeping the old CLR record alive would carry removed dependencies.
>
> **Constraints honored**:
> - DEC-058 (Anthropic Pattern-B byte-stable schema): versioned types make schema bumps explicit at the contract level.
> - DEC-060 (handler bodies emit events; projections own EF state): upcaster runs upstream of both `SingleStreamProjection.Evolve` and `EfCoreSingleStreamProjection.ApplyEvent`; both projection styles see the normalized current shape.
> - DEC-062 (`opts.Add(IProjection, ProjectionLifecycle)` for EF projections; `opts.Projections.Add<T>(...)` for document projections): upcaster registration is orthogonal to projection registration; no DEC-062 amendments required.
>
> **Failure-mode observability**: every upcaster class emits an `upcast.<event_type>` span on `ActivitySource "RunCoach.Marten.Upcaster"` with `from_type`/`to_type` tags; register that source in OTel alongside `Marten`. Daily dashboard query on `mt_doc_dead_letter_event` for upcaster failures.
>
> **Forward-compat**: stable across Marten 8 → 9 per the 2026 roadmap; expected migration cost on 9.x adoption is `using` namespace updates only.
>
> **Reconsider if**: 8+ legacy event versions accumulate (move to JSON-document upcasters); Marten announces an upcaster-pipeline RFC; a splitting (N→M) migration is needed (escalate to `IProjection` shim or copy-and-transform).

---

## 13. Open Follow-ups

1. **No source-confirmed snippet of `Marten.Services.Json.Transformations.SystemTextJson.JsonTransformations.Upcast(...)` overload list.** The `Marten/Events/EventMapping.cs` `FromDbDataReaderAsync` call site is confirmed; the static helper class's full signature surface was inferred from `Marten/EventSourcingTests/SchemaChange/Upcasters.cs` (e.g. `using static Marten.Services.Json.Transformations.SystemTextJson.JsonTransformations;`) but not fully enumerated. Suggest opening `src/Marten/Services/Json/Transformations/SystemTextJson/JsonTransformations.cs` directly before authoring the production upcaster classes.
2. **`mt_doc_dead_letter_event` exact column names**: the Marten docs reference both `mt_doc_deadletterevent` and `mt_doc_dead_letter_event` in different pages; current V8.x is the underscored form, but verify against your generated schema before writing dashboard SQL.
3. **The `mt_events_sequence` sequence name** in the worked test fixture (§7) is the default; confirm via `\ds` in psql before merging the test if your `DatabaseSchemaName` differs from `public`.
4. **Async upcaster + sync read path**: Marten throws if you register an `AsyncOnlyEventUpcaster` and then perform a synchronous event read. RunCoach uses async APIs throughout (per DEC-060 alignment with Marten 8's async-only direction), so this should not bite — but `FetchForWriting<T>` and `FetchLatest<T>` must remain async.
5. **Confirm `EfCoreSingleStreamProjection.ApplyEvent` signature stability**: the docs page (`martendb.io/events/projections/efcore.html`) is current, but the EF Core projection package itself shipped in Marten 8.23. Watch the 8.32→9.0 changelogs for any rename of `ApplyEvent` (low likelihood per roadmap themes, but worth a quarterly check).
6. **Snapshot strategy (out of scope per prompt) interaction**: if RunCoach later enables snapshotting, snapshot reads bypass event replay; the upcaster would only fire during snapshot rebuild. Re-read this artifact when DEC-XXX-snapshot lands.

---

## 14. Out of Scope (Explicit, per prompt)

- **Choosing between Marten and another event store**: Marten is locked.
- **Full event-sourcing / CQRS pedagogy**: assumes reader knows projections, streams, apply methods.
- **Snapshot strategy**: Marten supports it but Slice 1B does not enable it.
- **Replay-from-zero performance tuning**: out of scope until test suite or production daemon shows a problem.

---

*End of artifact. Total length ~7,200 words; code blocks total ~280 LOC across worked example + test fixture + worked telemetry hook. Primary sources: Marten official docs (martendb.io, 8.x pinned), `JasperFx/marten` GitHub source (`EventGraph.cs`, `EventMapping.cs`, `Upcasters.cs` test fixture, Migration Guide), Jeremy D. Miller's Shade Tree Developer blog (Critter Stack 2026 roadmap posts, April–May 2026), Oskar Dudycz's event-driven.io (Event Versioning with Marten reference article). No Stack Overflow, no Reddit, no AI-tutorial blogs cited.*