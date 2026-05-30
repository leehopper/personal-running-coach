# R-076 / Batch 27a — Critter Stack 2026 upgrade (Marten 9 / Wolverine 6) migration map

**Status:** Integrated (DEC-071) · **Date:** 2026-05-30 · **Drove:** PR #125

Consolidated, primary-source-verified migration notes for upgrading RunCoach's
event-sourcing + outbox backbone from Marten 8.37.1 / Wolverine 5.39.3 to the
"Critter Stack 2026" wave (Marten 9.2.1 / Wolverine 6.1.0 on JasperFx 2.0).
Research was performed inline against the restored 9.2.1 / 6.1.0 assemblies
(reflection) and the JasperFx primary sources during the PR #125 work, rather
than via the usual prompt→artifact handoff; this artifact records the verified
findings so the rationale survives.

## 1. Version set shipped

| Package | From | To |
|---|---|---|
| Marten / Marten.EntityFrameworkCore | 8.37.1 | 9.2.1 |
| WolverineFx / .EntityFrameworkCore / .Marten | 5.39.3 | 6.1.0 |
| WolverineFx.RuntimeCompilation | — | 6.1.0 (new, dev/test path) |

JasperFx 2.2.0 / JasperFx.Events 2.2.0 / Weasel 9.0.1 resolve transitively. All
target `net10.0` (the 9/6 wave drops `net8.0`; RunCoach is on .NET 10). NuGet
restore is conflict-free; no JasperFx version pin was required.

## 2. Breaking changes encountered + fixes (ground truth)

Verified empirically — each surfaced as a real compile error or test-boot failure
and was fixed against the restored assemblies, not from docs alone.

1. **`[Identity]` attribute relocated** `Marten.Schema` → `JasperFx` (Marten 9 no
   longer defines its own; it honors the shared JasperFx attribute). Fix:
   `using JasperFx;` in `IdempotencyMarker.cs`.
2. **Enum/exception namespace moves** (JasperFx 2.0 consolidation):
   - `TenancyStyle` → `JasperFx.MultiTenancy`
   - `TrackLevel` → `JasperFx.OpenTelemetry`
   - `DocumentAlreadyExistsException` → `JasperFx` (out of `Marten.Exceptions`;
     the two stream-collision exceptions `ExistingStreamIdCollisionException`
     and `ConcurrentUpdateException` stayed in `Marten.Exceptions`).
3. **Projection programming-model change** — convention-method
   `SingleStreamProjection<TDoc,TId>` subclasses (`OnboardingProjection`,
   `PlanProjection`) must be declared `partial` so the compile-time
   `JasperFx.Events.SourceGenerator` (shipped in the Marten NuGet analyzer asset)
   emits the aggregate "Evolver" dispatcher. The runtime reflection fallback was
   removed — without `partial` the store throws `InvalidProjectionException: No
   source-generated dispatcher found` at first boot. `Apply`/`Create`/`ShouldDelete`
   must be `public`. `EfCoreSingleStreamProjection` subclasses that override
   `ApplyEvent` (e.g. `UserProfileFromOnboardingProjection`) use the explicit
   virtual path and do NOT need `partial`.
4. **Wolverine 6 extracted runtime Roslyn codegen** into the opt-in
   `WolverineFx.RuntimeCompilation` package. Any host booting with
   `CodeGeneration.TypeLoadMode.Auto` (RunCoach's dev + integration-test host)
   throws `No IAssemblyGenerator is registered` without it. Production uses
   `TypeLoadMode.Static` (pre-generated) and ships without Roslyn — the intended
   design. The package auto-registers `IAssemblyGenerator` via its module.

**Verified unchanged (migrate as-is):** the reflection helper resolving the
assembly-root `EventStoreOptionsExtensions.MapEventTypeWithSchemaVersion<T>(IEventStoreOptions, uint)`
(byte-identical signature in 9.2.1); `StreamIdentity.AsGuid`; `TenancyStyle.Conjoined`;
`EfCoreSingleStreamProjection` materializing an EF row in the Marten transaction;
`AddAsyncDaemon(DaemonMode.Solo)` ↔ `DurabilityMode.Solo`; `ApplyAllDatabaseChangesOnStartup`;
per-event schema-version tagging + `Events.Upcast<TOld,TNew>` upcaster;
`DeleteAllTenantDataAsync` (GDPR); `IntegrateWithWolverine()`-alone envelope wiring
(DEC-048); the `TrackConnections` / `TrackEventCounters` OTel surface.

## 3. Behavioral / default changes (no code break, but relevant)

- **`EventAppendMode` default flipped** `Rich` → `QuickWithServerTimestamps`. We
  set `Rich` explicitly, so the flip is inert today — but `Rich` is now
  load-bearing rather than coincidental. Switching to QuickAppend is a deferred
  throughput lever (see DEC-071 / ROADMAP).
- **`UseIdentityMapForAggregates` default flipped** `false` → `true`. We set
  `true` explicitly; no change.
- **`EnableAdvancedAsyncTracking` default ON** — records high-water skips in a new
  `mt_high_water_skips` table (created cleanly by `ApplyAllDatabaseChangesOnStartup`).
  Improves Solo-daemon catch-up; verified the boot is clean.
- **DI defaults**: lightweight sessions + System.Text.Json are now the defaults
  (Newtonsoft moved to `Marten.Newtonsoft`). We already use lightweight sessions
  and STJ, so no change. `RestoreV8Defaults()` was deliberately NOT used.
- **Wolverine `ServiceLocationPolicy` default** flipped `AllowedButWarn` →
  `NotAllowed`. Our constructor-injection DI is unaffected (no service-location
  fallback in handlers).

## 4. What the upgrade gets RunCoach

**Directly useful now:** source-generated projection dispatch (faster apply, the
change that forced `partial`); `EnableAdvancedAsyncTracking` for better Solo-daemon
catch-up; PostgreSQL LISTEN/NOTIFY async-daemon wakeup (lower projection latency);
Roslyn removed from the hot path + AOT-clean `TypeLoadMode.Static` (leaner/faster
prod cold start); BigInt events (removes the ~2.1B-event ceiling, auto-migrates);
and the Wolverine 6.1 EF-outbox flush-timing correctness fix (the outbox flush
completes before the HTTP response is written — touches our "EF write + Marten
append in one TX" pattern; included in our 6.1.0 pin).

**Available levers, not adopted:** QuickAppend append mode (~50% append
throughput); `Marten.PgVector` (9.3) embeddings-in-Postgres + `VectorProjection`
for the LLM/coaching layer; DCB tag-based cross-stream invariants; per-event binary
serialization (`Marten.MemoryPack`). A future patch bump to Wolverine 6.2.x would
add outgoing-envelope pooling (~90% fewer publish-path allocations) and the 6.2.2
EF transaction-middleware codegen fix — neither is in our 6.1.0 pin.

**Plumbing:** JasperFx 2.0 / JasperFx.Events 2.0 / Weasel 9.0 extraction; Lamar
removed (Wolverine uses MS DI fully); coordinated namespace moves.

## 5. Verification

`dotnet build RunCoach.slnx` clean (0 warnings under `TreatWarningsAsErrors` with
SonarAnalyzer 10.27 + StyleCop). Full suite **1124/1124 passing**
(`dotnet test --solution RunCoach.slnx`) on Testcontainers Postgres, including the
Solo async daemon + `ApplyAllDatabaseChangesOnStartup` clean boot, the legacy-event
upcaster synthetic-row regression test, and the idempotency Wolverine error-routing
tests. CI green on PR #125 including `Backend (build + test)` and the OpenAPI
codegen drift gate (confirming zero API-contract drift).

## 6. Sources

- JasperFx release announcement (2026-05-24) — `jeremydmiller.com/2026/05/24/marten-9-0-polecat-4-0-and-wolverine-9-0-are-live/`
- Wolverine migration guide — `wolverinefx.net/guide/migration`; codegen — `wolverinefx.net/guide/codegen`
- Marten releases — `github.com/JasperFx/marten/releases` (9.0.0–9.3); Wolverine releases — `github.com/JasperFx/wolverine/releases` (6.0.0–6.2.2)
- Wolverine 6.0 release punchlist — `github.com/JasperFx/wolverine/issues/2745`
- Verified in-repo against the restored `Marten 9.2.1` / `WolverineFx 6.1.0` / `JasperFx 2.2.0` assemblies.

## Related

- DEC-071 (this upgrade), DEC-048 / DEC-049 (Marten+Wolverine startup composition), DEC-067 (event upcasting — verified intact).
