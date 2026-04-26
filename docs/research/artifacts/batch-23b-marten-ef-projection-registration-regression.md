# Registering EfCoreSingleStreamProjection in Marten 8.32.1

**Verdict on the developer's hypothesis (one line):** **Correct.** Registering `UserProfileFromOnboardingProjection` via `opts.Projections.Add(...)` routes the EF-target type through Marten's standard `DocumentMapping` pipeline, which then attempts to discover an `id/Id` member on `UserProfile` and fails because the PK is `UserId`. The fix is to register through the EF-Core-aware extension `opts.Add(...)` from `Marten.EntityFrameworkCore`, which sets up a transaction participant + Weasel migration for the EF entity tables and never tries to map `UserProfile` as a Marten document.

The first paragraph above is the operative fix. The rest of this artifact walks the API, the failure mode, the compatibility matrix with conjoined tenancy / Wolverine / lightweight sessions / inline-only mode, the version pins, the empirical verification path, and the 8.28→8.32 changelog.

## Concrete recommendation and corrected snippet

`Marten.EntityFrameworkCore` 8.32.1 ships **three EF Core projection base classes**, all parameterized in the order `<TDoc, TId, TDbContext>` (document type, identity type, DbContext type):

| Base class | Use case |
| --- | --- |
| `EfCoreSingleStreamProjection<TDoc, TId, TDbContext>` | Aggregate one stream into one EF entity row |
| `EfCoreMultiStreamProjection<TDoc, TId, TDbContext>` | Aggregate across streams into one EF entity row |
| `EfCoreEventProjection<TDbContext>` | React to individual events, write to both EF and Marten |

Your `UserProfileFromOnboardingProjection : EfCoreSingleStreamProjection<UserProfile, Guid, RunCoachDbContext>` shape is correct — the **3-type-parameter form does exist** in 8.32.1 and the generic order matches your usage. The bug is purely on the registration side.

Replace this in `MartenConfiguration.cs`:

```csharp
opts.Projections.Add(new OnboardingProjection(),                     ProjectionLifecycle.Inline);
opts.Projections.Add(new UserProfileFromOnboardingProjection(),      ProjectionLifecycle.Inline); // ❌ wrong path
```

with this:

```csharp
// Marten document projection — keep on opts.Projections.Add
opts.Projections.Add(new OnboardingProjection(), ProjectionLifecycle.Inline);

// EF Core projection — MUST go through the StoreOptions.Add extension
// brought in by Marten.EntityFrameworkCore. This single call
//   (a) registers the projection at the chosen lifecycle,
//   (b) installs a transaction participant that calls
//       RunCoachDbContext.SaveChangesAsync inside Marten's tx
//       (this is the atomic dual-write hook),
//   (c) registers UserProfile's table with Weasel for migration,
//   (d) does NOT route UserProfile through DocumentMapping.
opts.Add(new UserProfileFromOnboardingProjection(), ProjectionLifecycle.Inline);
```

No `IServiceCollection`-side registration is required — there is **no** `IntegrateWithEntityFrameworkCore<TDbContext>()`, no `AddMartenStore(...).IntegrateWithEfCore(...)`, no `[EfCoreProjection]` attribute, and no `services.AddMartenEfCore<TDbContext>()` extension in 8.32.x. Everything lives on `StoreOptions`. You also do not need `services.AddDbContext<RunCoachDbContext>()` for the projection to function — Marten constructs a per-slice DbContext bound to the same Npgsql connection as the active session. Keep your existing `AddDbContext` registration only if you also use `RunCoachDbContext` for read-side queries from controllers.

The `opts.Add(...)` call coexists with everything in your stack (`IntegrateWithWolverine()`, `Policies.AllDocumentsAreMultiTenanted()`, `UseLightweightSessions()`, inline-only) — see compatibility section below.

## Minimal API-surface walk of EfCoreSingleStreamProjection<,,>

The canonical override surface, taken verbatim from the official docs page (`martendb.io/events/projections/efcore.html`), is:

```csharp
public class UserProfileFromOnboardingProjection
    : EfCoreSingleStreamProjection<UserProfile, Guid, RunCoachDbContext>
{
    public override UserProfile? ApplyEvent(
        UserProfile? snapshot,
        Guid identity,
        IEvent @event,
        RunCoachDbContext dbContext,
        IQuerySession session)
    {
        switch (@event.Data)
        {
            case OnboardingSlotCompleted slot:
                snapshot ??= new UserProfile { UserId = identity };
                snapshot.ApplySlot(slot);
                return snapshot;

            case OnboardingFinalized finalized:
                if (snapshot is null) return null;
                snapshot.OnboardingCompletedAt = finalized.At;
                snapshot.CurrentPlanId = finalized.PlanId;
                return snapshot;
        }
        return snapshot;
    }

    // optional
    public override void ConfigureDbContext(
        DbContextOptionsBuilder<RunCoachDbContext> builder)
    {
        // Npgsql provider is registered by Marten BEFORE this hook runs.
        // builder.EnableSensitiveDataLogging();
    }
}
```

Key contract notes:

- `ApplyEvent` returns the new/updated snapshot; returning `null` deletes. EF change tracking handles the insert/update split — detached entities are added, unchanged ones are marked modified. `UserId` (the EF PK) is set from the `identity` parameter inside the handler; you don't need an `Id` member on the EF entity, which is exactly why `opts.Add(...)` (and not the document path) is required.
- The `EfCoreMultiStreamProjection` variant has only 4 parameters on `ApplyEvent` (drops `IQuerySession`); the `EfCoreEventProjection` variant has `ProjectAsync(IEvent, TDbContext, IDocumentOperations, CancellationToken)` for combined EF + Marten writes inside one method body.
- The base type for `EfCoreSingleStreamProjection` is internally aligned with Marten's `SingleStreamProjection<TDoc, TId>` semantics (slicing, lifecycle, rebuild). The exact base class chain (`SingleStreamProjection<TDoc, TId>` vs. an intermediate `EfCoreProjectionBase<TDbContext>`) is an implementation detail and not part of the public override contract you author against; the only methods you override are `ApplyEvent` and optionally `ConfigureDbContext`.
- Namespace: `Marten.EntityFrameworkCore`. Assembly: `Marten.EntityFrameworkCore`.

## Failure-mode walkthrough — why the original code throws

The exception message originates in `src/Marten/Schema/DocumentMapping.cs` (`CompileAndValidate()`), which builds the format string `"Could not determine an 'id/Id' field or property for requested document type {DocumentType.FullName}"`. This code path runs **only** when Marten is registering a type as a *document*. It never runs for an EF entity that is properly routed through `Marten.EntityFrameworkCore`'s extension.

What happens with the wrong call:

1. `opts.Projections.Add(new UserProfileFromOnboardingProjection(), ProjectionLifecycle.Inline)` invokes the standard `ProjectionCollection.Add` overload.
2. That overload inspects the projection's generic argument `TDoc` (here `UserProfile`) and asks `StoreOptions.Storage` for the `DocumentMapping` for `UserProfile`.
3. `DocumentMapping` runs `FindIdMember` looking for a property/field named `Id`/`id`/`ID` or one decorated with `[Identity]`.
4. `UserProfile` exposes only `UserId`, so `IdMember` is null, and `CompileAndValidate` throws `InvalidDocumentException`.
5. Because the policy `Policies.AllDocumentsAreMultiTenanted()` is on, Marten *also* tries to enforce tenancy on this would-be document — but the id-discovery failure trips first, so you never see the secondary tenancy error.

Switching to `opts.Add(new UserProfileFromOnboardingProjection(), ProjectionLifecycle.Inline)` (the `Marten.EntityFrameworkCore` extension) takes a fundamentally different code path: the projection is registered as an EF Core projection, the EF entity is mapped through `RunCoachDbContext.OnModelCreating`, and `UserProfile` never enters Marten's document-storage subsystem. `DocumentMapping.CompileAndValidate` is not called for `UserProfile`. The error disappears.

### Alternative cause — EF entity not picked up by `OnModelCreating`

This is **not** what you are hitting (the error wording is unambiguously the Marten document path), but for completeness — if `UserProfile` were missing from `RunCoachDbContext.OnModelCreating` because of module-first organization (e.g., `Identity/Entities/UserProfile.cs` not referenced by a `DbSet<UserProfile>` and not picked up by `modelBuilder.ApplyConfigurationsFromAssembly(...)`), the symptom would be different: at projection runtime EF would throw `InvalidOperationException: The entity type 'UserProfile' was not found` from `dbContext.Set<UserProfile>()` or `FindAsync`. That throws **after** host startup (during the first event flush), not during DI build, and the message contains "entity type … was not found", not "Could not determine an 'id/Id' field". You would diagnose it by running `dbContext.Model.FindEntityType(typeof(UserProfile))` in a unit test — if it returns `null`, fix the model registration. If it returns a non-null `IEntityType`, that branch is exonerated.

A related-but-distinct gotcha: schema-name mismatch. Marten defaults to schema `public`; if `RunCoachDbContext` uses `modelBuilder.HasDefaultSchema("identity")` and Weasel migrates the table to `public`, EF reads/writes will silently target an empty table. Pick one schema and pin both Marten (`opts.DatabaseSchemaName = "..."`) and EF (`HasDefaultSchema(...)`) to it.

## Empirical verification path (no SUT boot)

Don't go through `WebApplicationFactory<Program>` to reproduce or verify. Mirror the JasperFx test pattern: build a bare `DocumentStore` from the same `StoreOptions`. This is the same pattern used throughout `src/EfCoreTests/` in the JasperFx/marten repo.

```csharp
[Fact]
public void marten_options_compose_without_exception()
{
    using var store = DocumentStore.For(opts =>
    {
        opts.Connection(TestConnectionString);
        opts.Events.TenancyStyle = TenancyStyle.Conjoined;
        opts.Policies.AllDocumentsAreMultiTenanted();

        opts.Projections.Add(new OnboardingProjection(), ProjectionLifecycle.Inline);

        // The line under test:
        opts.Add(new UserProfileFromOnboardingProjection(), ProjectionLifecycle.Inline);
    });

    // Touch the storage subsystem to force schema/projection compilation.
    store.Storage.AllDocumentMappings.ShouldNotContain(
        m => m.DocumentType == typeof(UserProfile),
        "UserProfile must NOT be registered as a Marten document");
}
```

This test fails fast with the exact `InvalidDocumentException` if you write `opts.Projections.Add(...)` instead of `opts.Add(...)`, and passes silently when the registration is correct. Run it without spinning up Wolverine, ASP.NET Core, or the rest of the SUT. It typically completes in <500 ms against a local Postgres.

A second, even cheaper smoke check: assert that the type implements the expected hierarchy at compile time:

```csharp
typeof(EfCoreSingleStreamProjection<UserProfile, Guid, RunCoachDbContext>)
    .ShouldBeAssignableFrom(typeof(UserProfileFromOnboardingProjection));
```

Both fixtures match the JasperFx convention of building `DocumentStore.For(opts => ...)` directly in xUnit tests rather than through generic-host bootstrapping (e.g., `src/CoreTests/StoreOptionsTests.cs`, `src/EfCoreTests/`).

## Library version pins

Pin both packages to the **same 8.32.1 release**. `Marten.EntityFrameworkCore` ships from the same monorepo on the same release cadence as Marten core; satellite packages (`Marten.EntityFrameworkCore`, `Marten.AspNetCore`, `Marten.NodaTime`) are versioned in lockstep.

```xml
<PackageReference Include="Marten"                       Version="8.32.1" />
<PackageReference Include="Marten.EntityFrameworkCore"   Version="8.32.1" />
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.*" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.*" />
<PackageReference Include="WolverineFx.Marten"           Version="5.*"  />
```

`Marten.EntityFrameworkCore` 8.32.1 transitively pins `Weasel.EntityFrameworkCore` ≥ 8.11.4, which is the version that fixes EF Core 10 JSON column mapping (Marten 8.29.0, PR #4227). On .NET 10 + EF Core 10 you must be on at least Marten 8.29.x for that fix; you are on 8.32.1, so that bar is cleared. Do not float `Marten.EntityFrameworkCore` higher than `Marten` — there is no published guarantee that a newer companion package supports an older core.

## Compatibility notes

**Conjoined tenancy.** `opts.Policies.AllDocumentsAreMultiTenanted()` applies to **Marten documents only**. It does not auto-tenant your EF entity. Under `opts.Events.TenancyStyle = TenancyStyle.Conjoined`, however, Marten validates at startup that the EF projection target implements `Marten.Metadata.ITenanted` (a `string? TenantId { get; set; }` property) and throws `InvalidProjectionException` if it does not. Add `ITenanted` to `UserProfile`, map `TenantId` as a column in `OnModelCreating`, and you're good. The base infrastructure populates `TenantId` from the slice context automatically. If you stay on per-document tenancy via the policy and your event store is *not* set to `TenancyStyle.Conjoined`, this validation does not fire — but RLS won't apply to the EF table either, so prefer making the event store conjoined and the EF entity `ITenanted`. (Marten 8.31.0 added Postgres Row Level Security for conjoined tenancy, PR #4259, which extends to EF entity tables when the entity is `ITenanted`.)

**`IntegrateWithWolverine()`.** Fully compatible. Wolverine's outbox runs on Marten's transaction; the EF Core projection's transaction participant runs on the *same* Marten transaction. Order: `services.AddMarten(opts => { ... opts.Add(new UserProfileFromOnboardingProjection(), ProjectionLifecycle.Inline); }).UseLightweightSessions().IntegrateWithWolverine();`. No extra Wolverine wiring is needed for the EF projection. Wolverine's `[Transactional]` handlers will see `UserProfile` rows committed atomically with the events that produced them.

**`UseLightweightSessions()`.** Compatible. The EF projection's transaction participant uses the lightweight session's underlying Npgsql transaction; lightweight vs. identity-mapped is orthogonal to whether projections write to EF or to Marten documents. If you need per-aggregate identity-map semantics for `OnboardingView`, set `opts.Projections.UseIdentityMapForAggregates = true` separately — that flag affects Marten document aggregation, not the EF projection.

**`AsyncMode = false` / inline-only.** Compatible and in fact the preferred mode for the atomic dual-write contract. With `ProjectionLifecycle.Inline`, both `OnboardingView` and `UserProfile` are written in the same transaction as the event append. With `ProjectionLifecycle.Async`, you would lose atomicity. Stay on `Inline` for both projections per DEC-060.

## Marten 8.28 → 8.32.1 changelog (EF-projection-relevant items)

The `EfCoreSingleStreamProjection<TDoc, TId, TDbContext>` API surface was stable across this entire window. The feature itself shipped in **Marten 8.23** under issue #4145 ("First class EF Core projections"). EF-Core-relevant deltas in 8.28 → 8.32.1:

- **8.27.0** — Weasel 8.11.1 with EF Core `ToJson()` schema-diff support (PR #4218). Pre-8.28 baseline.
- **8.29.0 (6 Apr 2026)** — **EF Core 10 JSON column mapping fix** via Weasel 8.11.4 (PR #4227); `ConfigureNpgsqlDataSourceBuilder` for Npgsql plugin registration (PR #4228); `EnrichEventsAsync` hook on `EventProjection` (not `EfCoreEventProjection`-specific).
- **8.30.0** — `ProjectLatest` API (general projection feature, not EF-specific); `Lazy<T>` registration for ancillary stores in projections.
- **8.31.0 (19 Apr 2026)** — Postgres Row Level Security for conjoined tenancy (PR #4259) — applies to EF entity tables when the entity is `ITenanted`.
- **8.32.0** — JasperFx.Events 1.29.0 internal bump only.
- **8.32.1 (23 Apr 2026)** — bug fixes only: `ValueTypes` as duplicated fields (#4274), `NaturalKey` on self-aggregating aggregates (#4279), preserve original session tenant on `IEvent` for `AddGlobalProjection` (#4280), `Count`/`LongCount` after `GroupBy().Select()` (#4281), `IsOneOf` for collection members (#4283). **No `EfCoreSingleStreamProjection` changes.**

There were **no breaking changes** to EF projection registration, generic shape, or `ProjectionCollection.Add` semantics in this window. The 3-type-parameter form `<TDoc, TId, TDbContext>` is the same in 8.28 as in 8.32.1.

## Gotchas specific to Marten 8.32

- **`opts.Projections.Add` vs. `opts.Add` is silent except at startup.** The two methods differ only by a missing identifier; intellisense will surface both, and the broken form compiles. Add a Roslyn analyzer or a code-review checklist line that flags any `opts.Projections.Add(new EfCore*Projection(...))` instance.
- **EF entity must NOT have a property named `Id`.** If `UserProfile` happens to gain an `Id` shadow property (e.g., from a base class), `opts.Projections.Add(...)` would *succeed* in document mapping and silently create a Marten `mt_doc_userprofile` table parallel to the EF table — a much worse failure mode than the one you have. Stay on `opts.Add(...)`.
- **`Policies.AllDocumentsAreMultiTenanted()` runs after projection registration.** If you ever revert to `opts.Projections.Add(...)`, the policy will additionally try to enforce `ITenanted` on `UserProfile` — but only after id-discovery passes. The compounded error message is harder to read; another reason to stay on the EF-aware path.
- **Per-slice DbContext lifetime.** The DbContext is created and disposed per slice (per stream-batch). Don't cache state on `RunCoachDbContext` instance fields; treat it as transient. If you have `DbContext`-scoped caches, move them to a singleton service injected via `ConfigureDbContext` is **not** sufficient — use a Marten interceptor instead.
- **Schema-name pinning.** Set `opts.DatabaseSchemaName` and `RunCoachDbContext.OnModelCreating`'s `HasDefaultSchema(...)` to the same value. Marten's default is `public`; EF Core defaults to `dbo`-equivalent (which for Npgsql is also `public`), but if either is overridden in isolation you get a silent split-brain.
- **Weasel migrations cover the EF tables.** Once you switch to `opts.Add(...)`, Weasel will create/alter the EF entity tables alongside Marten's `mt_*` tables on `ApplyAllConfiguredChangesToDatabaseAsync()`. You do **not** need `dotnet ef migrations` for the projection target. If you are running EF migrations elsewhere for the same `RunCoachDbContext`, the two systems will fight; pick one (Weasel, for atomic dual-write).
- **`UserProfile.UserId` must equal the stream id.** The `identity` parameter passed to `ApplyEvent` is the Marten stream id (`Guid`). Set `snapshot.UserId = identity` on creation; do not derive it from event payload data, or you'll desynchronize the EF row from the stream.
- **Inline-mode rebuilds.** `ProjectionLifecycle.Inline` projections are not rebuildable through the async daemon. If you need to rebuild `UserProfile` from history, you'll need a one-off script that re-runs the projection, or temporarily switch the lifecycle to `Async`/`Live` for the rebuild. With `AsyncMode = false` locked in, plan rebuilds as offline operations.

## Conclusion

The breakage is exclusively a registration-call shape problem: `opts.Projections.Add(...)` puts the EF target type on Marten's document-mapping path, where `UserProfile.UserId` is invisible to id-discovery. The `Marten.EntityFrameworkCore` 8.32.1 package supplies a `StoreOptions.Add(IProjection, ProjectionLifecycle)` extension that is the documented and only correct registration site for `EfCoreSingleStreamProjection<,,>` and `EfCoreMultiStreamProjection<,,>`; switching to it eliminates the document-mapping inspection, installs the transaction participant that gives you atomic dual-write, and registers the EF tables with Weasel. The 3-type-parameter generic shape `<TDoc, TId, TDbContext>` is correct as written. No `IServiceCollection`-side extension is needed. Pin both `Marten` and `Marten.EntityFrameworkCore` to 8.32.1. The fix is one character of text removed (`Projections.`) — but the failure mode is informative: it is the canonical signature that an EF projection has been mis-routed onto the document path, and the assertion `store.Storage.AllDocumentMappings.ShouldNotContain(m => m.DocumentType == typeof(UserProfile))` is the cleanest regression guard you can leave behind.