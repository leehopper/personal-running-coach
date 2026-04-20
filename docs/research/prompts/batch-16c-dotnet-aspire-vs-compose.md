# Research Prompt: Batch 16c — R-050

# .NET Aspire vs Docker Compose + Tilt for Local Dev Orchestration (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a single-developer .NET 10 + React 19 SPA project that currently uses Docker Compose + Tilt for local-dev orchestration, has just locked in foundation patterns for ASP.NET Core Identity + Marten + Wolverine + EF Core + Testcontainers (Slice 0), and is approaching a hosted MVP-1 milestone, is .NET Aspire the right local-dev orchestration choice in 2026, what does adoption cost, what does deferred-adoption cost, and which target deployment scenarios does Aspire's manifest-publish path actually serve well?

## Context

I'm building MVP-0 of an AI-powered running coach (RunCoach). The project committed to **Docker Compose + Tilt** for local dev orchestration in DEC-032 (March 2026), at which time Aspire was newer and less mature. Since then:

- **Aspire matured significantly** in .NET 9 (Nov 2024) and .NET 10 (Nov 2025). Recent releases ship a non-Azure publish pipeline, a migration-worker pattern, and integrated OpenTelemetry dashboards.
- **R-046 (just landed)** flagged Aspire's migration-worker pattern as "the right local-orchestration story if and when RunCoach adopts Aspire" but stopped short of making the recommendation, noting that "as of April 2026 Aspire's production publish pipeline (outside Azure Container Apps) is still maturing."
- **Slice 0 just locked in** ASP.NET Core Identity (cookie session), Marten 8.28 (Wolverine integration), EF Core 10 (Wolverine outbox storage), Testcontainers + xUnit v3 AssemblyFixture, DataProtection key volume mount in Compose. These commitments make Slice 0 implementation increasingly Compose-shaped if implementation starts before the Aspire question is decided.
- **No hosted environment exists yet.** MVP-1 is the trigger for one. Likely candidates: single VPS, Render, Fly.io, Azure Container Apps (Aspire's first-class target), or Kubernetes.
- **Single-developer side project.** Operational complexity must be justified.

The strategic question: defer Aspire and continue with Compose+Tilt (familiar, working), or pivot now while the cost of pivoting is bounded by Slice 0 alone? The window is narrowing as Slice 0 implementation begins, and "decide later" effectively means "decide against, because the migration cost will be prohibitive."

## Research Question

**Primary:** For the project context above, is .NET Aspire the right local-dev orchestration choice in April 2026, and if so, what does the adoption / deferred-adoption decision matrix look like across the project's MVP-0 → MVP-1 → public-beta trajectory?

**Sub-questions (must be actionable):**

1. **Aspire's current capability surface.** As of April 2026 (Aspire 9.x or whatever the current major is): what does Aspire actually provide on the local-dev side — service discovery, multi-service orchestration, integrated dashboards, OpenTelemetry collection, container management, hot reload coordination, secret injection, dependency health management? What's still rough vs polished?

2. **Aspire's publish/deploy story for MVP-1's likely targets.** For each candidate hosted target — single VPS, Render, Fly.io, Azure Container Apps, generic Kubernetes — what's Aspire's publish-pipeline state? What manifest format does it emit (Bicep / Helm / Compose / something Aspire-native)? Can it publish to a non-Azure target without rough edges?

3. **Compose + Tilt as the alternative baseline.** What does the project lose by not adopting Aspire and staying on Compose + Tilt? What does it gain (familiarity, stability, well-trodden path, fewer tool dependencies)? Is there a meaningful Tilt-specific story that Aspire doesn't replicate?

4. **Marten + Wolverine integration with Aspire.** R-047 locked in Marten 8.28 + Wolverine 5.28 with `IntegrateWithWolverine`, `AddDbContextWithWolverineIntegration<AppDbContext>`, `AutoApplyTransactions()`, `DaemonMode.Solo`. Does Aspire integrate cleanly with this stack? Are there published Aspire integrations for JasperFx libraries or are we on our own to wire it? Any known sharp edges around Wolverine's transactional outbox or Marten's async daemon under Aspire's service-host model?

5. **Testcontainers + xUnit v3 AssemblyFixture interaction.** R-046 locked in `[assembly: AssemblyFixture(typeof(RunCoachAppFactory))]` with one `WebApplicationFactory<Program>` + one `PostgreSqlContainer` per assembly. Does Aspire change this story — does it want its own test-host pattern, does it offer test-specific orchestration, or does the existing AssemblyFixture pattern compose cleanly with Aspire?

6. **Migration-worker pattern.** Aspire ships a recommended migration-worker pattern: a separate project that runs migrations and exits, orchestrated to run before the API starts. R-046 documented `dotnet ef migrations bundle` as the production path with two Postgres roles. Does Aspire's migration-worker make the bundle-as-Job split obsolete, complement it, or conflict with it? What's the correct interaction?

7. **Observability integration.** Aspire bundles OpenTelemetry dashboards. R-047 mentioned `marten.{projection}.gap` histogram + other Marten OTel metrics worth alerting on. The project also has SonarQube Cloud + CodeQL + Codecov (per DEC-043) — those handle code quality and CVEs, not runtime observability. Does Aspire's bundled observability actually cover RunCoach's needs (Marten daemon health, Wolverine queue depth, HTTP latency, LLM-call duration), or does it just give the dashboard skeleton and require per-signal wiring?

8. **DataProtection key handling under Aspire.** R-044 (DEC-044) made the DataProtection master key load-bearing for the cookie auth substrate. The Slice 0 spec wires it to a mounted Compose volume. Does Aspire have an opinion on DataProtection persistence across restarts? Does it integrate with secret managers (related to R-049's secrets-management question)?

9. **The cost of pivoting now (Slice 0).** Concrete file/wiring scope for migrating the current Slice 0 spec from Compose + Tilt to Aspire — `Program.cs` changes, `docker-compose.yml` deletion / replacement with `AppHost` project, Tilt config retirement, Testcontainers fixture adjustments, Marten/Wolverine wiring changes, `/keys` volume handling.

10. **The cost of pivoting later (after MVP-0 ships).** If we keep Compose for MVP-0 and pivot to Aspire pre-MVP-1, what's the scope? What's the opportunity cost of running Compose for the duration of MVP-0 (e.g., do we miss out on observability dashboards we'd otherwise have)?

11. **Aspire stability & lifecycle.** Aspire is on a regular release cadence with breaking changes between minor versions historically. Has it stabilized? What's the LTS story? Is it committed to the .NET 10 LTS train or does it have its own release cadence? Are there versions to specifically pin or avoid?

12. **Single-developer ergonomics.** Aspire's pitch is enterprise-team-friendly orchestration. For a one-person side project, does the AppHost project add cognitive overhead that outweighs its benefits, or does it remove operational tasks (multi-service start/stop, log aggregation, dashboard setup) that justify itself even for one developer?

13. **Decision triggers.** What concrete project characteristics flip the recommendation — number of services, deployment target, team size, observability requirements, multi-environment-consistency needs? Where does RunCoach sit relative to those triggers?

14. **Aspire's claim about being the future of .NET dev.** Microsoft positions Aspire as the modern way to build .NET cloud apps. Is that positioning credible in 2026, or is it still aspirational? What's the GitHub-discoverable adoption signal — major OSS .NET projects using Aspire vs sticking with Compose / Helm / hand-rolled?

## Why It Matters

- **The window is closing.** Slice 0 implementation, once started, will harden the Compose + Tilt commitment. After Slice 0 ships, the migration cost is non-trivial (every wired service, every fixture, every deploy assumption gets touched). Decided now: spec amendment + a few hours of wiring redirection. Decided after Slice 0: a multi-day refactor.
- **MVP-1 is the deployment trigger.** Aspire's strongest pitch is its publish pipeline. If MVP-1 ships to one of Aspire's strong targets, adoption now removes a decision later. If MVP-1 ships to a target Aspire doesn't serve well, adoption now adds friction with no offsetting benefit.
- **R-049 (secrets management, queued in parallel) and R-050 are interlinked.** Aspire has opinions on secret injection and observability that affect R-049's recommended pattern. Coordinating their findings makes the foundation coherent.
- **DEC-032 was made when Aspire was less mature.** Revisiting the decision under current information is healthy hygiene, not second-guessing.
- **This is the same-pattern-as-Batch-15 logic.** Foundation patterns at Slice 0 propagate; rework cost compounds; research before — not after — implementation.

## Deliverables

- **A concrete recommendation** — adopt Aspire now (with rationale), defer to MVP-1 (with rationale), or commit to Compose + Tilt long-term (with rationale).
- **A capability matrix** — Aspire vs Compose + Tilt across local-dev DX, multi-service orchestration, observability, deploy publish, Marten/Wolverine integration, Testcontainers interaction, secret handling, single-developer overhead.
- **A target-deployment matrix** — for each likely MVP-1 host (VPS, Render, Fly.io, Azure Container Apps, K8s), Aspire's publish-pipeline support quality and the operational story.
- **A pivot-now scope estimate** — the file-by-file change list for migrating the Slice 0 spec from Compose + Tilt to Aspire. Scope in hours.
- **A pivot-later scope estimate** — the comparable scope if Aspire is adopted at MVP-1 instead.
- **An Aspire-version pin** if the recommendation is adoption — current stable, any versions to avoid, LTS posture.
- **A Marten + Wolverine + Aspire wiring sketch** if the recommendation is adoption.
- **A migration-worker / bundle-as-Job interaction note** — how the R-046 production migration story changes (or doesn't) under Aspire.
- **An observability story** if the recommendation is adoption — what Aspire gives for free, what still needs custom wiring.
- **Citations** — current Aspire docs, JasperFx blog or GitHub for any Marten/Wolverine + Aspire integration notes, real .NET 10 OSS projects using Aspire in production (not Microsoft sample apps).

## Out of Scope

- Choice of cloud provider — we want Aspire's publish-pipeline coverage as a property of each candidate, not a recommendation of which provider to adopt.
- Choice of language / framework — .NET 10 is locked in.
- Choice of frontend tooling — React 19 + Vite is locked in; Aspire's frontend integration is interesting but not a substitution candidate for Vite.
- Choice of database — Postgres is locked in; Aspire's database integration is interesting only insofar as it changes how we wire/start Postgres in dev.
- Microservices architecture — RunCoach is a single API + SPA + Postgres for the foreseeable future. Aspire's multi-service pitch is interesting for context but not the primary axis.
- Aspire-on-Windows specifically — assume macOS / Linux dev environments per the project's existing Docker / Colima setup.
