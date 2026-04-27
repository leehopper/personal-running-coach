# Research Prompt: Batch 23b — R-070

# Marten 8.32 + `Marten.EntityFrameworkCore` `EfCoreSingleStreamProjection<TDoc, TKey, TDbContext>` registration regression — Slice 1 integration tests fail at SUT boot with `InvalidDocumentException` on `UserProfile`

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: Given a .NET 10 / Marten 8.32.1 / `Marten.EntityFrameworkCore` 8.32.1 / Wolverine 5.x backend that has registered an `EfCoreSingleStreamProjection<UserProfile, Guid, RunCoachDbContext>` against an EF Core entity (`UserProfile`, PK `UserId : Guid`) — what is the **correct registration call** on `StoreOptions.Projections` (or whatever Marten/Marten.EntityFrameworkCore surface owns this) that (a) runs the projection as a transaction participant on the same `NpgsqlConnection` as Marten's session per DEC-060, and (b) does NOT cause Marten's document-store side to also try to materialize `UserProfile` as a Marten document (which fails because EF entities have no Marten-style `Id` field)? Equivalently: what is the canonical 2026 way to register a 3-type-param `EfCoreSingleStreamProjection` so that it functions as an EF projection rather than getting interpreted as a generic projection-with-document-mapping?

Secondary question: is the `Marten.EntityFrameworkCore.EfCoreSingleStreamProjection<TDoc, TKey, TDbContext>` 3-type-param API (Marten 8.23+) actually present in `Marten.EntityFrameworkCore` 8.32.1, or is the spec's reference to it (per R-069) referring to a different overload / a name that has since been renamed / a generic that has since been changed?

## Context

I'm in the middle of Slice 1 (Onboarding → Plan) implementation. Several atomic tasks have shipped per DEC-060 / R-069:

- **T01.1 (#89)** added `UserProfile` as an EF Core entity — primary key `UserId : Guid` (1:1 to `ApplicationUser`), six nullable JSONB slot columns, `OnboardingCompletedAt`, `CurrentPlanId`, audit columns. Migration `AddUserProfileEntity` ships clean. EF `DbContext` is `RunCoachDbContext`.
- **T01.4 (#92)** wired the inline `OnboardingProjection` (a `SingleStreamProjection<OnboardingView, Guid>` document projection) AND a `UserProfileFromOnboardingProjection : EfCoreSingleStreamProjection<UserProfile, Guid, RunCoachDbContext>` (the Marten.EntityFrameworkCore 3-type-param projection) so that the EIGHT onboarding events (`OnboardingStarted, TopicAsked, UserTurnRecorded, AssistantTurnRecorded, AnswerCaptured, ClarificationRequested, PlanLinkedToUser, OnboardingCompleted`) materialize into BOTH the in-flight `OnboardingView` Marten document AND the EF `UserProfile` row inside one transaction per DEC-060's atomic-dual-write pattern.

The implementation worker for #92 verified the spec assumption against live NuGet — `Marten.EntityFrameworkCore` 8.32.1 exists and was added (matching the installed Marten 8.32.1). The 3-type-param API and 5-param `ApplyEvent(snapshot, identity, @event, dbContext, session)` signature were verified against the actual decompiled DLL.

The current `MartenConfiguration.cs` registers BOTH projections at the same call site:

```csharp
opts.Projections.Add(new OnboardingProjection(), ProjectionLifecycle.Inline);
opts.Projections.Add(new UserProfileFromOnboardingProjection(), ProjectionLifecycle.Inline);
```

(Exact call site is in `backend/src/RunCoach.Api/Infrastructure/MartenConfiguration.cs` — read it before responding.)

## What's broken

Every integration test in `backend/tests/RunCoach.Api.Tests/` that boots `WebApplicationFactory<Program>` is failing at SUT host startup with:

```
Marten.Exceptions.InvalidDocumentException:
  Could not determine an 'id/Id' field or property for requested document type
  RunCoach.Api.Modules.Identity.Entities.UserProfile
```

This blocks:
- T01.6 (#94)'s deferred `OnboardingFlowIntegrationTests`, `OnboardingResumeIntegrationTests`, `DualWriteAtomicityTests` (the `pg_stat_activity.backend_xid` observer regression test from R-069 §11).
- T02.4 (#98)'s 6 new integration cases (`PlanRenderingControllerIntegrationTests`).
- T02.5 (#99)'s plan-gen integration tests + OTel emission.
- ~45 pre-existing integration-test classes from earlier slices.

Build is clean (`dotnet build` 0/0). Unit tests that don't boot the SUT pass. The exception fires inside the Marten store-options resolver during DI container build — i.e., before any test method body runs.

The `#98` worker's diagnosis hypothesis (in `docs/specs/13-spec-slice-1-onboarding/01-proofs/T02.4-proofs.md`):

> "UserProfile is being registered for Marten document mapping by `opts.Projections.Add(new UserProfileFromOnboardingProjection(), ProjectionLifecycle.Inline)` despite the `Marten.EntityFrameworkCore` extension intent. The 3-type-param `EfCoreSingleStreamProjection<UserProfile, Guid, RunCoachDbContext>` is supposed to make Marten treat `UserProfile` as an EF-projected target, NOT as a Marten document. The current registration shape causes Marten's document-store to also try to register `UserProfile` as one of its own documents, which then fails because `UserProfile` (an EF entity) has no Marten-style `Id` property — its PK is `UserId`."

This is a hypothesis. It's plausible (the symptom is consistent with Marten falling back to its document discovery when the projection's target type can't be classified as "EF-managed"), but it has not been verified against `JasperFx/marten` or `Marten.EntityFrameworkCore` source.

## What I want from this research pass

The question is concrete and time-boxed: **what registration shape is correct for `Marten.EntityFrameworkCore` 8.32.1's `EfCoreSingleStreamProjection<TDoc, TKey, TDbContext>`** so the SUT boots without `InvalidDocumentException`? Possibilities to weigh:

1. The hypothesis is right and `opts.Projections.Add(new EfCoreProjection(), ProjectionLifecycle.Inline)` is the wrong API. Some other call lives on `StoreOptions` (possibly an extension method shipped by `Marten.EntityFrameworkCore` itself, e.g. `opts.UseEntityFrameworkCoreProjections<TDbContext>()` or `opts.Projections.UseEfProjection<TProjection, TDbContext>()` or a config-time registration on the DI side via `AddMartenStore(...).IntegrateWithEntityFrameworkCore<RunCoachDbContext>()`). Identify the actual API and the canonical 2026 idiom.
2. The hypothesis is right but the fix is simpler — `EfCoreSingleStreamProjection` requires the EF entity to declare its key in a specific way (`[Key]` attribute, `IIdentifier<Guid>` interface, configuration callback) that Marten's projection-target inspection looks for. Identify the precondition.
3. The hypothesis is wrong — the failure is happening for a different reason (missing tenancy attribute on `UserProfile` despite `Policies.AllDocumentsAreMultiTenanted()`, missing `ITenanted` interface on `UserProfile` because the global tenancy policy applies even to projection targets, missing schema-name configuration, missing `CompiledQueryDirectory` registration that the EF projection's runtime needs, etc.). Identify the actual root cause.
4. The 3-type-param API does not exist on `Marten.EntityFrameworkCore` 8.32.1 — the spec's reference to it (per R-069 / DEC-060) is wrong or out of date. The actual API in 8.32.1 might be a 2-type-param `EfCoreSingleStreamProjection<TDoc, TDbContext>` with the key derived from `TDoc`, or `EfCoreInlineProjection<...>`, or something else. Identify the actual public API surface.

## Deliverables

- **Concrete recommendation** for the registration call site in `MartenConfiguration.cs`. Show the corrected snippet — both the `Marten.EntityFrameworkCore` package's call (if it requires a different extension method) AND the `RunCoachDbContext` DI registration on `IServiceCollection` if THAT changes too.
- **Verification path** that doesn't require booting the full SUT. Ideally: a minimal Marten `DocumentStore` build that constructs the same `StoreOptions` graph the production code does and either succeeds or surfaces the exact error in isolation. (`Marten.Storage.Database.AssertConnectivityAsync(ct)` against a Testcontainers Postgres might suffice.) Cite which `JasperFx/marten` or `Marten.EntityFrameworkCore` test fixture pattern this matches.
- **Minimal API-surface walk** of `Marten.EntityFrameworkCore.EfCoreSingleStreamProjection<,,>` 8.32.1 — class hierarchy (does it derive from `Marten.Events.Projections.SingleStreamProjection<TDoc>` or from a different base?), what overrides Marten's projection inspector relies on, and whether the projection target type (`UserProfile`) is supposed to flow through `opts.Schema.For<UserProfile>(...)` at all (or whether registering it for the schema is what's wrongly making Marten try to treat it as a Marten doc).
- **One-line verdict** on whether the `#98` worker's "UserProfile is being registered for Marten document mapping" hypothesis is correct, partially correct, or wrong.
- **Failure-mode walkthrough** for the case where the projection registration is correct but the EF entity isn't picked up by `RunCoachDbContext`'s `OnModelCreating` because of namespace / module-first organization issues. Confirm or rule out as a contributing cause.
- **Library version pins.** Confirm that `Marten.EntityFrameworkCore` 8.32.1 is the right pin given Marten 8.32.1, OR identify the correct pinned version.
- **Compatibility notes.** Does the recommended fix interact with: (a) the conjoined-tenancy `Policies.AllDocumentsAreMultiTenanted()` registration from Slice 0, (b) the `IntegrateWithWolverine()` Marten/Wolverine bridge from R-047, (c) the `UseLightweightSessions()` performance pattern, (d) the `AsyncMode = false` / inline-only constraint we've adopted? List any caveats.
- **Gotchas** specific to Marten 8.32 (point releases between 8.28 and 8.32 may have changed the EF projection registration API — flag any breaking changes).

## Why It Matters

Three downstream effects gate on this:

1. **Slice 1 cannot ship without integration tests.** T02.4 (#98), T01.6 (#94)'s deferred suite, T02.5 (#99), and the `DualWriteAtomicityTests` regression test (R-069 §11) are all blocked by this single registration error. The architectural-invariant test `DualWriteAtomicityTests` is what proves DEC-060's atomic dual-write claim; without it landing, the whole DEC-060 architectural rule is unverified empirically.
2. **The Slice 0 startup smoke test is also failing** for the same reason, which means even `dotnet test` on the existing test surface is reporting 45+ regressions. CI is currently red (or would be — local runs are already failing).
3. **Pattern repeats across Slice 3 and Slice 4.** DEC-060 establishes the architectural rule: handler bodies emit events, projections own EF state. Slice 3's `PlanAdaptedFromLog` and Slice 4's `ConversationTurnRecorded` will both add NEW `EfCoreSingleStreamProjection` registrations. Getting the canonical registration shape right NOW means we don't re-fail this on every future slice.

## Existing research references

- **R-047 (`batch-15d-marten-per-user-aggregate-patterns.md`)** — locks Marten 8.28 + Wolverine 5.28 + Npgsql 9. Does NOT cover EF projection registration.
- **R-048 (`batch-15e-marten-projection-prompt-shape.md` / DEC-047)** — confirms `EfCoreSingleStreamProjection` apply paths share Marten's transaction. Does NOT cover the registration call site.
- **R-069 (`batch-23a-marten-ef-dual-write-atomicity.md` / DEC-060)** — specifies the 3-type-param API and `EfCoreSingleStreamProjection<UserProfile, Guid, RunCoachDbContext>` shape. Does NOT show the corresponding `MartenConfiguration` registration call.

## Repo files to examine

- `backend/src/RunCoach.Api/Infrastructure/MartenConfiguration.cs` — current registration call site
- `backend/src/RunCoach.Api/Modules/Coaching/Onboarding/UserProfileFromOnboardingProjection.cs` — projection class
- `backend/src/RunCoach.Api/Modules/Identity/Entities/UserProfile.cs` — EF entity declaration
- `backend/src/RunCoach.Api/Modules/Identity/Entities/UserProfileConfiguration.cs` — EF Fluent API config
- `backend/src/RunCoach.Api/Infrastructure/RunCoachDbContext.cs` — DbContext + DbSet registration
- `backend/Directory.Packages.props` — confirm Marten + Marten.EntityFrameworkCore version pins

## Out of scope for this prompt

- The secondary `LayeredPromptSanitizerTests` corpus failures from T06.1 (#115). Those will be a separate research / fix-up pass.
- The Wolverine `OnException<ConcurrencyException>().MoveToErrorQueue()` policy concurrency-handling shape (already covered by R-066).
- Any changes to the architectural rule itself (DEC-060 stays).

## Format expected

Per the existing `docs/research/artifacts/batch-*.md` format:

- Concrete recommendation with rationale, citing source code or docs URLs verified against the current 8.32.1 release.
- Alternatives considered and why rejected.
- Library/tool version pins.
- Failure-mode walkthrough.
- Empirical verification plan (the test fixture pattern that proves the fix).
- Gotchas and compatibility notes.
- One-line verdict on the `#98` worker's hypothesis.
