> **Research artifact — Batch 28a · R-077.** Commissioned via the RunCoach research protocol; prompt at `docs/research/prompts/batch-28a-efcore10-npgsql-heterogeneous-jsonb-metrics.md`. Deep-web-research output landed & integrated 2026-05-31 (queue → Integrated). Locks **DEC-072** (`WorkoutLog.Metrics` persistence + canonical keys). Load-bearing version-specific claims were independently re-verified before lock — see DEC-072 `Verified:`. Verbatim research output follows.

---

# ADR-R: Persisting an Open-Ended JSONB "Metrics Bag" on `WorkoutLog` (EF Core 10.0.8 + Npgsql 10.0.2, coexisting with Marten 9.2.1)

**Status:** Research artifact (Slice 2b, MVP-0, RunCoach). **Date:** 2026-05-31. **Stack:** EF Core 10.0.8, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.2, Marten 9.2.1 + Marten.EntityFrameworkCore 9.2.1, Wolverine 6.1.0, PostgreSQL (current major), Swashbuckle 10, @rtk-query/codegen-openapi 2.2.0, Orval 8.12.1 (Zod v4), .NET 10 / C# 14.

## TL;DR
- **Store `Metrics` as a `string` property typed `jsonb` via `.HasColumnType("jsonb")` (default), or — if you want a typed CLR member — a `Dictionary<string, JsonElement>` mapped through a `ValueConverter` + `ValueComparer` (fallback).** EF Core 10's headline JSON feature (`ComplexProperty(...).ToJson()`) is for *closed* schemas only and cannot map an open key bag (dotnet/efcore #26903, "Can't map Dictionary<string,object> (e.g. to JSON) since it's detected as a property bag," is still open/Backlog). The string-jsonb approach satisfies all four constraints with zero DDL per new metric.
- **Express the bag in the DTO as `additionalProperties` (a free map) and ship the canonical key list as a generated C# enum/const** so the contract drift gate stays meaningful on the *core* fields while the metrics map stays open; do **not** index now — ship un-indexed and add a hand-authored expression-index migration when the first value-query slice lands.
- **`WorkoutLog` is a plain EF relational entity in the `public` schema; it introduces no Weasel/Marten diff noise** because Marten owns `runcoach_events` and EF owns `public`. Implement `Marten.Metadata.ITenanted` to match `UserProfile`; it becomes mandatory if the `WorkoutLogged` projection writes the row through an `EfCoreSingleStreamProjection` under conjoined tenancy.

## Key Findings

🔴 **The EF Core 10 "complex types → JSON" feature cannot model an open bag.** `ComplexProperty(...).ToJson()` requires a strongly-typed CLR shape; mapping `Dictionary<string,object>` still throws *"The navigation '…' must be configured in 'OnModelCreating' with an explicit name for the target shared-type entity type, or excluded by calling 'EntityTypeBuilder.Ignore'."* This is dotnet/efcore #26903 — titled verbatim *"Can't map Dictionary<string,object> (e.g. to JSON) since it's detected as a property bag,"* labeled `area-model-building` / `customer-reported` / `priority-bug`, still **open in Backlog**. The issue body notes the asymmetry directly: *"This works e.g. for Dictionary<string,string>, but fails for Dictionary<string,object> since the property is detected as a property bag."* EF maintainer roji has confirmed it still reproduces on latest `main`, and that the intended future direction is `JsonDocument`/`JsonElement` weakly-typed support (#28871/#29825), not `Dictionary`.

🔴 **The lowest-regret representation is a `jsonb` column the developer serializes itself.** Per the Npgsql "JSON Mapping" docs: *"With string mapping, the EF Core provider will save and load properties to database JSON columns, but will not do any further serialization or parsing — it's the developer's responsibility to handle the JSON contents, possibly using System.Text.Json to parse them."* The DOM alternative is just as low-ceremony: *"neither a data annotation nor the fluent API are required, as JsonDocument is automatically recognized and mapped to jsonb"* (note `JsonDocument` is disposable, which forces the entity to be disposable too — a reason to prefer the plain `string` form). Both accept unlimited unknown keys with no migration.

🟠 **`additionalProperties` survives the codegen pipeline.** Swashbuckle 10 emits `Dictionary<string,T>` with an unknown (non-enum) key type as `type: object` + `additionalPropertiesAllowed: true` + `additionalProperties: { …T… }`; Orval maps that to `z.record(...)` (not `z.strictObject`), and rtk-query emits a TS index signature `{ [key: string]: T }`. An open map therefore does not collide with `override.zod.strict = true`, which only converts `additionalProperties:false` → `z.strictObject` on *closed* property sets.

🟠 **No index now.** EF Core 10 + Npgsql can model a whole-column GIN index via `.HasIndex(b => b.Metrics).HasMethod("gin")` (exact fluent form per the Npgsql Indexes docs), but **cannot** model an *expression* index on an extracted key (`((metrics->>'rpe')::numeric)`). This is npgsql/efcore.pg #2568 ("Make HasIndex work on JSON properties," opened by roji on 2022-11-19, still open in Backlog): *"The simple, general thing is to create an expression index. It's also possible to speed up containment specifically via a GIN index, but that's limited."* An extracted-key expression in `HasIndex` throws *"The properties expression … is not valid. The expression should represent a simple property access."* Since MVP-0 does not query by metric value, ship un-indexed.

🟡 **`Distance`/`Duration` should map via `ValueConverter`** (`double` meters / `long` ticks ↔ value object) — establishing the repo's first converter. A `readonly record struct` needs no custom `ValueComparer`. EF Core 10 complex types are an option but overkill for a single scalar.

## Details

### Sub-question 1 — CLR representation of the bag

| Option | Open keys, no DDL | Query/index reach | Compile-time safety | STJ value pitfalls | null-vs-absent | Migration-diff stability |
|---|---|---|---|---|---|---|
| **(a) `string`/`JsonDocument` typed `jsonb`** | ✅ Yes — opaque blob | ✅ via raw `->>`/`@>` + `EF.Functions` | ❌ None (string) | None (you own STJ) | Explicit (you decide) | ✅ Most stable — one scalar column |
| **(b) `OwnsOne(..., b=>b.ToJson())` over open `Dictionary`** | ❌ Throws/property-bag | n/a | n/a | n/a | n/a | ❌ Does not work |
| **(c) `Dictionary<string,JsonElement>` direct** | ⚠️ Needs converter (property-bag detection) | ❌ no indexer translation | ⚠️ keys still strings | `object?` boxes to `JsonElement` | Converter-defined | ⚠️ needs `ValueComparer` |
| **(d) Typed owned record, one prop per metric** | ❌ DDL per metric | ✅ full LINQ | ✅ Best | None | Per-property | ❌ Defeats open-endedness |

**APIs/namespaces:** `Microsoft.EntityFrameworkCore.NpgsqlJsonDbFunctionsExtensions` (`EF.Functions.JsonContains` / `JsonExists` / `JsonExistAny` / `JsonExistAll` / `JsonContained` / `JsonTypeof`); `Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<TModel,TProvider>`; `Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<T>`; `PropertyBuilder.HasColumnType("jsonb")`, `.HasConversion(...)`, `.Metadata.SetValueComparer(...)`.

**Decision:** **(a) string-typed `jsonb` is the default**; **(c) `Dictionary<string,JsonElement>` + converter is the fallback** when the team wants a typed member rather than hand-serializing at the boundary. Avoid `Dictionary<string,object?>`: System.Text.Json boxes every value to `JsonElement` on deserialize regardless — per the Microsoft "Migrate from Newtonsoft.Json to System.Text.Json" doc, *"System.Text.Json stores a boxed JsonElement for both primitive and complex values whenever deserializing to Object, for example: An object property. An object dictionary value. An object array value. A root object."* So `Dictionary<string,JsonElement>` gives identical fidelity with explicit types. Option (b) does not work; option (d) violates the open-key constraint.

**Why not (c) as the default?** It is detected as an EF "property bag" and requires `.HasConversion(...)` plus a `ValueComparer` (a mutable reference type silently breaks change tracking without one). That is more moving parts than (a) for identical query reach — neither (a) nor (c) translates `dict["rpe"]` to SQL.

### Sub-question 2 — `splits` array-of-objects

Storing `splits: [{ index, distanceMeters, durationSeconds, paceSecondsPerKm }, …]` **inside the same JSONB document is sound** for MVP-0. PostgreSQL handles nested arrays natively; you can still reach them with `@>` containment and `jsonb_array_elements` in raw SQL. A separate child table only earns its keep when you need to query/aggregate splits relationally (e.g., "all splits faster than X across all runs") — defer that to a later slice. **Codegen consequence:** because the bag is a free map (`additionalProperties`), `splits` is invisible to codegen as a typed array unless you surface a *named* `Split` schema. Recommendation: define a `Split` DTO record and expose it as its own typed array property so the array-of-objects is typed on the frontend, while the rest of the bag stays open (see sub-question 5).

### Sub-question 3 — Indexing: now vs deferred

🟠 **Recommendation: ship un-indexed; add an expression-index migration when the first value-query slice lands.** Rationale:
- MVP-0 never filters by metric value, so any index is pure write-overhead now.
- A whole-column **GIN** index (`CREATE INDEX … USING gin (metrics jsonb_path_ops)`) accelerates containment (`@>`) and key-existence (`?`) but **not** range queries like `(metrics->>'rpe')::numeric >= 8`.
- The future queries ("HR > 150," "RPE ≥ 8") are **range scans on extracted scalars**, best served by **B-tree expression indexes**, e.g. `CREATE INDEX ix_workoutlog_rpe ON public."WorkoutLog" (((metrics->>'rpe')::numeric));`
- Adding either index later is a **pure additive migration** — the `jsonb` column does not change shape, so there is **no re-architecting**. This is the entire point of choosing a single opaque column.

**EF modeling:** `.HasIndex(b => b.Metrics).HasMethod("gin")` works for the whole-column GIN case; **expression indexes on extracted keys cannot be modeled by `HasIndex`** (npgsql/efcore.pg #2568, still open; an extracted-key expression throws *"The expression should represent a simple property access"*) and must be written with `migrationBuilder.Sql("CREATE INDEX …")`.

**Trigger to add the index:** when a query/endpoint that filters or sorts by a metric value ships, AND the `WorkoutLog` table exceeds roughly 10k rows per active user cohort (below that, a sequential scan is cheaper than index maintenance).

### Sub-question 4 — LINQ query surface (Npgsql 10)

Translated today via `EF.Functions` (on `string` / `JsonDocument` / POCO-mapped jsonb):
- `JsonContains(col, json)` → `col @> json` (must pass a `JsonElement` or JSON string, **not** a bare scalar; historically required explicit `::jsonb` casts — efcore.pg #1139/#2363).
- `JsonExists(col, "rpe")` → `col ? 'rpe'`; `JsonExistAny`/`JsonExistAll` → `?|`/`?&`.
- `JsonContained`, `JsonTypeof`.
- For `JsonDocument`: `col.RootElement.GetProperty("rpe").GetInt32()` → `(col #>> '{rpe}')::int`; `GetArrayLength()` → `jsonb_array_length(...)`.

**Where you fall back to raw SQL:** range predicates on extracted text (`(metrics->>'rpe')::numeric >= 8`), indexer access on a mapped `Dictionary` (`dict["rpe"]` is **not** translated — efcore.pg #1825), and querying nested array-element sub-properties (`splits[].pace < X` — efcore.pg #1616). Use `FromSql`/`ExecuteSql` with `->>`/`@>` for those. This bounds sub-question 3: because rich value-querying needs raw SQL or expression indexes anyway, deferring the index costs nothing architecturally.

### Sub-question 5 — OpenAPI / Swashbuckle → Zod round-trip (load-bearing)

**DTO shape recommendation:** keep `WorkoutLog`'s **core fields as normal typed properties** (so the drift gate is meaningful on them) and expose `Metrics` as a **dictionary** (`IDictionary<string, JsonElement>`), plus a **typed `Split[]` member if you want splits typed**:

```csharp
public sealed record CreateWorkoutLogRequest(
    Guid? PlannedWorkoutId,
    DateTimeOffset LoggedAt,
    double DistanceMeters,
    long DurationSeconds,
    CompletionStatus CompletionStatus,
    string? Notes,
    IDictionary<string, JsonElement>? Metrics);   // → additionalProperties (open map)
```

- **Swashbuckle 10** emits a `Dictionary<string,T>` with an *unknown* (non-enum) key type as `type: object` + `additionalPropertiesAllowed: true` + `additionalProperties: { …T… }` (Swashbuckle SchemaGenerator: *"Unknown keys: Generated as type: object with additionalPropertiesAllowed: true and additionalProperties containing the value schema"*). The surrounding DTO still gets `required` + a fixed `properties` set for the core fields, and with the repo's `RequireNonNullablePropertiesSchemaFilter` an `additionalProperties:false` on the *outer* object — so the gate stays strict on core fields.
- **Orval 8.12.1 (`client:'zod'`, Zod v4):** `additionalProperties: { … }` becomes `z.record(z.string(), …)` — **not** `z.strictObject`. Orval's `override.zod.strict=true` only maps `additionalProperties:false` → `z.strictObject`; a present `additionalProperties` value-schema is emitted as a record and is unaffected. So the open bag and the strict-object gate coexist: strict applies to the closed core DTO, the record stays open. (Per Zod v4: *"z.looseObject() will never set additionalProperties: false; z.strictObject() will always set additionalProperties: false."*)
- **rtk-query/codegen-openapi 2.2.0:** emits a TS index signature — `metrics?: { [key: string]: …}` — so the form never hand-maintains a key list (confirmed by the codegen's documented `additionalProperties` → `{ [key: string]: string }` output).

**Reconciliation:** the gate stays meaningful because core fields live in a closed, `strictObject`-validated schema; the metrics map is a separate `z.record(...)` that intentionally accepts unknown keys. To keep splits typed, declare a named `Split` record so its array is `z.array(splitSchema)` — model it as its own DTO property rather than a key inside the free map if you want compile-time safety on it.

### Sub-question 6 — Canonical metric-key constants (single source of truth)

Put the canonical key set in **one C# file**, `Modules/Training/Constants/WorkoutMetricKeys.cs`, as a `static class` of `const string` plus an **enum surfaced in the OpenAPI doc**:

```csharp
public static class WorkoutMetricKeys
{
    public const string Rpe = "rpe";
    public const string HrAvg = "hrAvg";
    // … HrMax, Calories, Hrv, SleepScore, RecoveryScore, Weather, Terrain
}

public enum WorkoutMetricKey { Rpe, HrAvg, HrMax, Calories, Hrv, SleepScore, RecoveryScore, Weather, Terrain }
```

**Mechanism to reach the frontend without a hand-mirrored TS list:** add a small response model that *uses* `WorkoutMetricKey` (e.g. a `MetricKeyCatalog` DTO with a `WorkoutMetricKey[]` field, or document it as an enum schema referenced by an endpoint). Swashbuckle emits the enum into `swagger.json`; Orval/rtk-query then generate a TS union/enum the form consumes. The free `Metrics` map stays open (`additionalProperties`), but the *canonical* keys flow through codegen as a generated enum — manual entry today and Garmin/HealthKit auto-fill later both write the **same** `const` strings (server) / enum members (client). This is the "documented free map + generated key enum" pattern, and it is the only one of the three options (const/enum surfaced, x-extension, free map + generated enum) that keeps the map open while still single-sourcing the keys.

### Sub-question 7 — Value objects inside vs beside the bag

`Distance` and `Duration` are **core columns**, not metrics. For EF Core 10:
- **Use a `ValueConverter`** (the repo's first): `Distance` (`readonly record struct`, stores meters) ↔ `double`; `Duration` ↔ `long` (ticks or seconds).
- **`ValueConverter` on a `readonly record struct` is fully supported.** Per the EF Core Value Comparers docs, *"this value object is implemented as a readonly struct. This means that EF Core can snapshot and compare values without issue"* — so a single-property `readonly record struct` needs **no custom `ValueComparer`** (the default value-type logic works; `record struct` adds value-equality automatically).
- **Complex types (EF 8+/10) are overkill** for a single scalar value object — they shine for multi-property value objects (Money, Address). Use `ComplexProperty` only if a value object maps to >1 column.
- **Primitive-at-boundary** (store `double`, convert in app code) is the lowest-ceremony alternative but loses type safety in the entity; the converter is preferred now that you're establishing the pattern.

```csharp
public sealed class DistanceConverter() : ValueConverter<Distance, double>(
    d => d.Meters, m => Distance.FromMeters(m));
```

**Units inside the bag:** if a unit-bearing metric lands in the JSONB bag (e.g. elevation), **encode the unit in the key name and use canonical SI units by convention** — e.g. always store `elevationGainMeters` (meters), never a bare `elevation`. The canonical-key file documents the unit per key. Do not store `{value, unit}` pairs in MVP-0; canonical-SI-by-key-name keeps the map flat and queryable.

### Sub-question 8 — Coexistence with Marten 9 + DEC-060/DEC-062

- **No Weasel/Marten diff noise.** Marten manages only `runcoach_events`; `RunCoachDbContext` pins all EF entities to `public` via `ApplyConfigurationsFromAssembly`. `dotnet ef migrations add` diffs only the EF model against `public`; Marten's Weasel diff runs separately against its own schema. The new `jsonb` column lives entirely in EF's world. (Note: Marten 9 + `Marten.EntityFrameworkCore` *can* unify schema management under Weasel for EF-projection entities — Marten docs state entity tables defined in the DbContext used by an `EfCoreSingleStreamProjection` are *"automatically migrated alongside Marten's own schema objects through Weasel … dotnet ef migrations are not needed."* Weasel docs warn that mixing the two for the same table causes conflicts. Keep `WorkoutLog` under standard `dotnet ef` migrations unless it becomes a Marten EF-projection target; do not mix.)
- **`ITenanted`:** implement `Marten.Metadata.ITenanted` (adds a `string? TenantId` property) **mandatorily if** the `WorkoutLogged` event is projected into `WorkoutLog` via `EfCoreSingleStreamProjection<WorkoutLog, Guid, RunCoachDbContext>` AND the event store uses `TenancyStyle.Conjoined`. Per Marten's EF Core Projections docs, under conjoined tenancy *"the projection infrastructure automatically writes the tenant ID to each projected entity. Your aggregate entity must implement ITenanted,"* and *"if the event store uses conjoined tenancy but your aggregate type does not implement ITenanted, Marten throws an InvalidProjectionException."* If `WorkoutLog` is instead written directly by a command handler (DEC-060: handlers emit events, projections own EF state), `ITenanted` is not required by EF itself, but implementing it for consistency with `UserProfile` is reasonable and harmless (it is just a `TenantId` column). **Recommendation: mirror `UserProfile` and implement `ITenanted`.**
- **Large JSONB inside Marten's transaction:** when an `EfCoreSingleStreamProjection` writes a row carrying a large bag, the DbContext `SaveChangesAsync` runs inside Marten's transaction (Marten *"register[s] a transaction participant so the DbContext's SaveChangesAsync is called within Marten's transaction, ensuring atomicity"*). The only gotcha is the usual one — a very large jsonb payload inflates the projection write; keep the bag to genuine metrics (no raw sensor streams). Also beware `EfCoreMultiStreamProjection` + `FindAsync` lookups by PK only (use GUID keys or a composite key including `tenant_id`).

### Sub-question 9 — Migration safety

✅ **"New metric key requires no schema change" holds** for the recommended `string`/`jsonb` (and the `Dictionary`+converter fallback): the column type is a fixed `jsonb` scalar; adding `recoveryScore` or a Garmin-only key is a pure data change, **no migration**. ✅ **Adding/removing canonical keys in `WorkoutMetricKeys.cs` produces no EF migration** — those are C# constants/enum members, not mapped properties; the only artifact that changes is `swagger.json` (caught by the DEC-066 `git diff --exit-code` drift gate, which is the desired behavior). EF's model snapshot records the `jsonb` column once and never again. (Caveat: if you use the `Dictionary`+converter fallback, ensure the `ValueComparer` is set so EF doesn't emit spurious "column changed" on every save, and confirm the snapshot stores `"jsonb"` as the column type, not a default `text`.)

## Recommendations

**Recommended representation (default) — `string` typed `jsonb`:**

```csharp
using Marten.Metadata;

public sealed class WorkoutLog : ITenanted   // ITenanted for conjoined-tenancy parity with UserProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? PlannedWorkoutId { get; set; }
    public DateTimeOffset LoggedAt { get; set; }
    public Distance Distance { get; set; }           // value object (meters)
    public TimeSpan Duration { get; set; }            // or a Duration VO
    public CompletionStatus CompletionStatus { get; set; }
    public string? Notes { get; set; }

    // Open metrics bag — opaque jsonb; serialize at the boundary with System.Text.Json.
    public string? Metrics { get; set; }              // jsonb; null = no metrics captured

    public string? TenantId { get; set; }             // ITenanted
}

public enum CompletionStatus { Complete, Partial, Skipped }
```

```csharp
public sealed class WorkoutLogConfiguration : IEntityTypeConfiguration<WorkoutLog>
{
    public void Configure(EntityTypeBuilder<WorkoutLog> b)
    {
        b.ToTable("WorkoutLog");                      // public schema (DbContext default)
        b.HasKey(w => w.Id);
        b.Property(w => w.UserId).IsRequired();
        b.Property(w => w.PlannedWorkoutId);
        b.Property(w => w.LoggedAt).IsRequired();

        b.Property(w => w.Distance)
            .HasConversion(d => d.Meters, m => Distance.FromMeters(m))
            .IsRequired();                            // readonly record struct → no ValueComparer needed

        b.Property(w => w.Duration)
            .HasConversion(t => t.Ticks, ticks => TimeSpan.FromTicks(ticks))
            .IsRequired();

        b.Property(w => w.CompletionStatus)
            .HasConversion<string>()                  // store enum as text
            .IsRequired();

        b.Property(w => w.Notes);

        b.Property(w => w.Metrics)
            .HasColumnType("jsonb");                  // open bag; no DDL per new key

        b.Property(w => w.TenantId);                  // mapped for ITenanted

        b.HasIndex(w => w.UserId);
        b.HasIndex(w => w.LoggedAt);
        // NO metrics index now (see indexing decision).
    }
}
```

**Fallback representation — typed `Dictionary<string,JsonElement>`** (when a typed member is wanted):

```csharp
public Dictionary<string, JsonElement>? Metrics { get; set; }
```
```csharp
private static readonly JsonSerializerOptions J = new();
b.Property(w => w.Metrics)
    .HasColumnType("jsonb")
    .HasConversion(
        v => JsonSerializer.Serialize(v, J),
        v => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(v, J),
        new ValueComparer<Dictionary<string, JsonElement>>(
            (a, c) => JsonSerializer.Serialize(a, J) == JsonSerializer.Serialize(c, J),
            v => v == null ? 0 : JsonSerializer.Serialize(v, J).GetHashCode(),
            v => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(v, J), J)!));
```

**OpenAPI + Zod shape:** core fields → closed `z.strictObject`; `Metrics` → `z.record(z.string(), …)`; `Split[]` → typed `z.array(...)` via a named `Split` schema. Drift gate stays meaningful on core fields; metrics map stays open.

**Canonical-key mechanism:** `WorkoutMetricKeys` (const strings, server) + `WorkoutMetricKey` enum surfaced through a catalog DTO → codegen emits a TS enum; manual + wearable ingestion write identical keys.

**Indexing decision:** **Ship un-indexed.** Trigger: first value-query endpoint + table growth → add `CREATE INDEX … (((metrics->>'rpe')::numeric))` via `migrationBuilder.Sql(...)` (hand-authored; not modelable by `HasIndex`, per efcore.pg #2568).

**Value-object mapping:** `ValueConverter` on `readonly record struct` (no `ValueComparer` needed); establishes the repo's first converter pattern.

**Round-trip/regression test pattern:**

```csharp
[Fact]
public async Task Metrics_bag_roundtrips_sparse_keys_and_splits()
{
    var json = """
    { "rpe": 7, "hrAvg": 152,
      "splits": [ { "index": 1, "distanceMeters": 1000, "durationSeconds": 300, "paceSecondsPerKm": 300 } ] }
    """;
    var log = new WorkoutLog { Id = Guid.NewGuid(), UserId = userId, LoggedAt = DateTimeOffset.UtcNow,
        Distance = Distance.FromMeters(5000), Duration = TimeSpan.FromMinutes(25),
        CompletionStatus = CompletionStatus.Complete, Metrics = json, TenantId = userId.ToString() };
    db.WorkoutLogs.Add(log); await db.SaveChangesAsync();
    db.ChangeTracker.Clear();

    var read = await db.WorkoutLogs.SingleAsync(w => w.Id == log.Id);
    using var doc = JsonDocument.Parse(read.Metrics!);
    Assert.Equal(7, doc.RootElement.GetProperty("rpe").GetInt32());
    Assert.False(doc.RootElement.TryGetProperty("calories", out _));   // absent ≠ null
    Assert.Equal(1, doc.RootElement.GetProperty("splits").GetArrayLength());
}
```
Plus a **migration no-op check**: after adding a new canonical key constant, run `dotnet ef migrations add Probe --no-build` in CI and assert the generated `Up`/`Down` are empty (or assert `dotnet ef migrations has-pending-model-changes` reports none) — proving a new key needs no DDL.

**Marten coexistence note:** no Weasel diff noise (EF owns `public`, Marten owns `runcoach_events`); implement `ITenanted` to match `UserProfile`. If `WorkoutLog` becomes an `EfCoreSingleStreamProjection` target, `ITenanted` becomes mandatory under conjoined tenancy and schema management moves to Weasel (don't run `dotnet ef` on that table then).

## Caveats
- dotnet/efcore #26903 (open-`Dictionary`→jsonb auto-mapping) is **open/Backlog** as of 2026, labeled `priority-bug` requiring an API break; revisit when EF ships `JsonDocument`/`JsonElement` weakly-typed mapping (#28871/#29825) — it may later allow a cleaner typed bag without a converter.
- `EF.Functions.JsonContains` has a history of requiring explicit `::jsonb` casts and `JsonElement`-wrapped scalars (efcore.pg #1139/#2363); validate generated SQL when the value-query slice lands.
- The fallback `ValueComparer` serializes on every change-detection pass — fine for small bags, but do not use the fallback for very large payloads.
- Confirm in the actual generated `swagger.json` that the repo's `RequireNonNullablePropertiesSchemaFilter` does **not** stamp `additionalProperties:false` on the *Metrics* sub-schema (it should target the outer DTO only); if it does, the open map would wrongly become `z.strictObject` — adjust the filter to skip dictionary-valued properties.
- Orval/Zod v4 has known codegen quirks with `strictObject` + min/max constraints requiring post-processing workarounds (orval-labs/orval #2933) and `z.strictObject({}).extend(...)` typing changes from v3→v4 (colinhacks/zod #4823); pin versions and smoke-test the generated `*.zod.ts` after the first generation.
- One EF Core 10 migration caveat applies to SQL Server only (auto-conversion of `nvarchar(max)` JSON columns to the native `json` type at compatibility level 170, efcore #37275); it is **not** relevant on PostgreSQL/Npgsql, where the column is already `jsonb`.