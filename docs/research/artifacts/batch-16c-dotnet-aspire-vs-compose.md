# Aspire can wait: defer the pivot, stay on Compose and Tilt through MVP-0

**Recommendation: defer the Aspire adoption decision to MVP-1, and plan for a conditional adoption — not a default one.** Slice 0 is already Compose-shaped, the current cost of pivoting buys almost no realized value (no hosted environment, no team, no second service), and the single most load-bearing Slice 0 commitment — the DataProtection `/keys` volume — maps poorly onto Aspire's project-resource model. JasperFx's own public guidance on Aspire for Marten/Wolverine is lukewarm to negative as of 2025–2026, and there is still no official `Aspire.Hosting.Marten` or `Aspire.Hosting.Wolverine` package. The narrow but real win — a first-class OpenTelemetry dashboard that Jeremy D. Miller himself uses as his Marten testbed — is reproducible in Compose with an OTel Collector plus Jaeger/Tempo for roughly half a day of work.

The window to pivot cheaply does not slam shut at MVP-1; Aspire 13.2 ships a now-stable Docker Compose publisher, meaning an Aspire AppHost can emit a production-deployable `docker-compose.yaml` for a VPS later without re-architecting Slice 0 today. Pivot later when (a) MVP-1's hosted target is committed, (b) a second .NET service or a cross-service trace actually exists, or (c) JasperFx ships a first-class integration. Until then, the March-2026 DEC-032 commitment to Compose + Tilt was correct and should stand.

## Aspire's capability surface in April 2026

Aspire's current stable release is **13.2.2** (patches 13.2.1 and 13.2.2 landed in late March–early April 2026 to fix 13.2.0 IDE-execution regressions for Azure Functions and class library projects). Microsoft dropped the ".NET" prefix at v13.0 in November 2025 at .NET Conf. Aspire follows its **own release cadence** — annual majors, roughly quarterly minors, monthly patches — and it has **no LTS**: Microsoft's Modern Lifecycle policy supports only the latest minor, so Aspire 8.x and 9.x are already **out of support** as of April 2026 even though .NET 10 itself is LTS through November 2028.

The capability surface breaks into three tiers. **Polished** capabilities include typed service discovery via `WithReference`, AppHost/DCP multi-service orchestration, the developer dashboard (resource graph, structured logs, trace UI, 13.2 telemetry export/import), OpenTelemetry auto-collection for .NET projects, `WaitFor`/`WaitForCompletion` health gating, and the Aspire CLI (13.2 was explicitly a CLI overhaul with `aspire start/stop/ps/secret/doctor/wait`). The integration-package **breadth** is polished — official Postgres, Redis, SQL Server, RabbitMQ, Kafka, Key Vault, plus a Community Toolkit that adds Ollama, KurrentDB, Bun, Go, Java. **Workable** capabilities include container management (Docker and Podman supported; Colima works as a Docker-compatible runtime but is not officially listed), hot reload (works for host-process .NET projects, inconsistent in containers), secret injection (improved with 13.2's `aspire secret` CLI), and non-.NET resource support (Python/JS are first-class since 13.0; Go and Java remain in the Community Toolkit, which is explicitly "not officially supported"). **Rough** areas: dashboard telemetry is **in-memory only by design** (tracking issue `dotnet/aspire#7355` remains open), Blazor WASM service discovery (`#7524`), and `AddViteApp`/`AddJavaScriptApp` cannot yet be deployed as standalone services (`#12697`).

Practitioner sentiment is mixed along predictable lines: Phil Haack and Milan Jovanović praise the F5-to-run dashboard experience for single-solution .NET stacks; Andrew Lock is measured-positive but explicit he "hasn't used it in anger"; Oskar Dudycz published "Why I won't use .NET Aspire for now" citing DCP as a non-standard control plane; codewithmukesh summarizes the sweet spot bluntly: **"For single-API projects with just a database, Aspire adds unnecessary overhead."** That quote maps directly onto RunCoach.

## Publish and deploy story by target

| Target | Aspire publish | Aspire deploy | Quality (Apr 2026) | Notes |
|---|---|---|---|---|
| Azure Container Apps | ✅ via `azd` + ACA publisher | ✅ | Polished | First-class, Microsoft's flagship path |
| Docker Compose (VPS, Railway, Coolify, Dokku) | ✅ `Aspire.Hosting.Docker` | ✅ (`aspire deploy` runs `compose up`) | **Stable as of 13.2 (March 2026)** | Emits `docker-compose.yaml` + parameterized `.env`; **does NOT emit `build:` sections** — assumes pre-built images pushed to a registry |
| Generic Kubernetes | ✅ `Aspire.Hosting.Kubernetes` (Helm) | ❌ bring your own `helm`/GitOps | Preview/workable | Replaces community `aspir8`; `initContainers` don't emit correctly (#15021 — blocks EF migrations); no Ingress story yet |
| Fly.io | ❌ no first-party publisher | ❌ | Not supported | Would deploy individual services with `fly launch` and lose Aspire orchestration |
| Render.com | ❌ | ❌ | Not supported | No integration |
| AKS / EKS / GKE | 🔲 planned | 🔲 planned | Not shipped | AKS marked "planned, not started" on the Q1 2026 roadmap |

**Lock-in verdict**: Adopting Aspire in April 2026 does not commit RunCoach to Azure. The Compose publisher is genuinely production-usable for a VPS deploy — the realistic pipeline is GitHub Actions → push images to GHCR → `scp` or GitOps the emitted compose file to the VPS → `docker compose up -d`. This is David Fowler's reference pattern in `aspire-ai-chat-demo`. Terraform/Pulumi publishers and GitHub Actions/GitLab pipeline generation are "in progress" on the Q1 2026 roadmap but not shipped.

## Capability matrix: Aspire 13.2 vs Compose + Tilt

| Dimension | Aspire 13.2 | Compose + Tilt |
|---|---|---|
| Local-dev DX (.NET) | Typed `WithReference`, F5-to-dashboard, compile-time errors on wiring | String-typed env vars, `tilt up` UI, runtime-typo failures |
| Multi-service orchestration | DCP (proprietary MS control plane, K8s-API-compatible) | Compose engine + Tilt resource graph |
| Observability | Built-in OTel dashboard, in-memory only, local-dev only | DIY OTel Collector + Jaeger/Tempo/Grafana in compose (~half day) |
| Deploy publish | Compose & K8s publishers stable/preview; no VPS-specific path | The compose file IS the deploy artifact |
| Marten/Wolverine | Indirect via `NpgsqlDataSource`; JasperFx has de-prioritized deeper integration | Native — no friction, maintainer-recommended |
| Testcontainers + xUnit v3 | `Aspire.Hosting.Testing` composes with AssemblyFixture but doubles startup cost | Direct — `PostgreSqlContainer` per assembly, fastest path |
| Secret/DataProtection handling | Volumes **only on container resources**, not project resources — major friction for `/keys` | Native bind mounts, exact prod parity |
| Single-dev overhead | Two extra projects (AppHost + ServiceDefaults), DCP workload, learning curve ~1 week to confident | Already learned; stable infrastructure |
| Polyglot (React, Ollama, Postgres) | Python/JS first-class since 13.0, but `AddViteApp` is build-only | First-class by default |
| Vendor neutrality | Microsoft-owned, quarterly breaking-minor cadence, no LTS | Compose-spec is an open standard |

## Integration notes on the RunCoach stack

### Marten and Wolverine with Aspire

There is **no `Aspire.Hosting.Marten` and no `Aspire.Hosting.Wolverine` package** on NuGet. Integration flows through the shared `NpgsqlDataSource` — Marten 7+ and Wolverine 3+ were explicitly refactored to consume it from DI, which is what `builder.AddNpgsqlDataSource("runcoach")` registers. **Jeremy D. Miller's public position in the Critter Stack 2025 roadmap (January 2025) is: "I honestly don't know what is going to happen with Wolverine & Aspire. Aspire doesn't really play nicely with frameworks like Wolverine right now. My strong preference right now is to just use Docker Compose for local development."** That is the single most important external signal for this decision. Issue `JasperFx/wolverine#635` (Aspire support) was closed `wontfix`.

The wiring, if adopted, is straightforward:

```csharp
// AppHost
var postgres = builder.AddPostgres("postgres").WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
var runcoachDb = postgres.AddDatabase("runcoach");
var api = builder.AddProject<Projects.RunCoach_Api>("api")
    .WithReference(runcoachDb).WaitFor(runcoachDb);

// API
builder.AddNpgsqlDataSource("runcoach");  // the Aspire seam
builder.Services.AddMarten(opts => { opts.DatabaseSchemaName = "runcoach"; })
    .UseNpgsqlDataSource()                 // <-- MUST use this, not opts.Connection(...)
    .IntegrateWithWolverine().AutoApplyTransactions();
builder.Services.AddMartenAsyncDaemon(DaemonMode.Solo);
builder.Services.AddDbContextWithWolverineIntegration<AppDbContext>(opts =>
    opts.UseNpgsql(sp => sp.GetRequiredService<NpgsqlDataSource>()));
```

The single sharp edge: `DaemonMode.Solo` has no documented Aspire-specific issue, but you **must** chain `.WaitFor(runcoachDb)` to avoid the daemon flailing while Postgres is still bootstrapping. The Wolverine+Rabbit startup race (`wolverine#752`) does not apply to a Postgres-only stack.

### Testcontainers and `AssemblyFixture`

`Aspire.Hosting.Testing` is stable in 13.2 and **composes** with the existing `[assembly: AssemblyFixture(typeof(RunCoachAppFactory))]` pattern — it does not replace `WebApplicationFactory<Program>`. The idiomatic shape is: AppHost via `DistributedApplicationTestingBuilder.CreateAsync<Projects.RunCoach_AppHost>()` inside `IAsyncLifetime.InitializeAsync`, wait for Postgres healthy, grab the connection string, then construct a separate in-process `WebApplicationFactory<Program>` that reuses that connection string via `UseSetting`. A working public example (Ben Sampica's blog, with TUnit — shape identical for xUnit v3) exists.

Three frictions matter. First, this means **two running API instances** per test (AppHost-launched plus in-memory `TestServer`), which the docs acknowledge. Second, `GetTestServer()` is explicitly unsupported on AppHost resources (`dotnet/aspire#11543`) — for cookie-auth tests needing `HandleCookies = true`, you must stay on the `WebApplicationFactory` branch. Third, per-assembly fixed startup cost increases by **2–5 seconds** (DCP + dashboard + Postgres vs just Postgres). Testcontainers is not deprecated by Aspire, and nothing prevents keeping `PostgreSqlContainer` directly if maximum speed matters.

### DataProtection: the single biggest architectural friction

**`.WithVolume(...)` and `.WithBindMount(...)` only work on container resources (`IResourceBuilder<ContainerResource>`). They are not available on `IResourceBuilder<ProjectResource>`.** The Slice 0 spec's mounted `/keys` volume does not port by a one-line change. Three options:

- **Containerize the API inside the AppHost** via `AddDockerfile("api", "../RunCoach.Api").WithBindMount("./data/keys", "/keys")`. Preserves prod parity but forfeits F5/dotnet-watch integration, which is Aspire's main DX pitch.
- **Move DataProtection keys into Postgres** via `PersistKeysToDbContext<T>()` (or Marten-backed). Cleanest Aspire-native pattern; persistence is free because Postgres has its volume. Requires a schema change and trusting the shared DB for DP keys.
- **Accept local dev/prod divergence** on DP persistence — dev uses a host path, prod uses whatever the target provides. Violates the Slice 0 parity principle.

There is no `Aspire.DataProtection.*` integration, no Aspire docs on cookie-auth, and volume-name churn has been reported with `dotnet user-secrets` regeneration (`microsoft/aspire#4770`). This is the single most consequential finding for the pivot-now analysis.

### Migration worker and `ef migrations bundle`

Aspire's official pattern is a separate Worker Service project that calls `Database.MigrateAsync()` and exits, with the API declared as `.WaitForCompletion(migration)`. Microsoft's own docs state that running migrations at startup is **"inappropriate in production"**, and maintainers have confirmed on `dotnet/aspire#6815` that this worker should be gated behind `IsRunMode` — **production should continue to use `dotnet ef migrations bundle`**. The patterns are orthogonal, not competing.

RunCoach's two-Postgres-roles design (migrator with DDL, app with DML-only) is **not covered by Aspire's sugar**. `AddPostgres().AddDatabase()` creates one superuser. The two roles require either a custom SQL init script mounted via `WithBindMount("./init", "/docker-entrypoint-initdb.d")` or role creation by the migrator itself on first run, plus `WithEnvironment("ConnectionStrings__runcoach", ...)` overrides per resource because `WithReference(runcoachDb)` injects the **same** connection string to everything that references it. This is annotated in community discussions, not in official docs (the official `dotnet/docs-aspire#64` documentation gap remains open).

### Observability

Automatic signals for any project using ServiceDefaults: ASP.NET Core HTTP request metrics and traces, HttpClient, .NET runtime metrics, `ILogger` logs. Manual wiring required for the signals RunCoach actually cares about: **Marten's `ActivitySource` and `Meter`** need `AddSource("Marten")` + `AddMeter("Marten")` plus `opts.OpenTelemetry.TrackConnections = TrackLevel.Normal` and `opts.OpenTelemetry.TrackEventCounters()` on the Marten options — then the `marten.{projection}.gap` histogram lights up. **Wolverine** emits an `ActivitySource` (`"Wolverine"`) once registered; deeper queue-depth metrics are on the CritterWatch roadmap and not canonically documented as of April 2026 (flagged uncertainty). **Custom LLM-call duration histograms** require a `Meter` like `RunCoach.Llm` — vanilla `System.Diagnostics.Metrics`; Aspire provides zero magic here.

The Aspire dashboard is **in-memory only** — "no telemetry is persisted when the dashboard is restarted" per the official docs — and Microsoft itself states it is "intended as a developer visualization tool, and not for production monitoring." Production requires Grafana/Prometheus/Honeycomb or similar, wired via `OTEL_EXPORTER_OTLP_ENDPOINT` override, often through an OTel Collector container added to the AppHost (see `practical-otel/opentelemetry-aspire-collector`). The notable Marten-specific upside: **Jeremy Miller's own testbed for Marten's OTel instrumentation was the Aspire dashboard** — for a Marten-heavy codebase, the local-dev visualization of projection gaps is the single most defensible reason to adopt Aspire for inner-loop use.

## Cost of pivoting now (Slice 0)

Estimated effort: **1.5 to 3 days** for a clean pivot, assuming the DataProtection decision is made cleanly.

File-by-file scope: create a new `RunCoach.AppHost` project with `Aspire.AppHost.Sdk 13.2.2` and an `IDistributedApplicationBuilder`-based `Program.cs` modelling Postgres + API + migrator (~2 hours); create a `RunCoach.ServiceDefaults` shared library with `AddServiceDefaults()` and `ConfigureOpenTelemetry()` wiring Marten + Wolverine meters and sources (~2 hours); refactor `RunCoach.Api/Program.cs` to `AddNpgsqlDataSource("runcoach")` and `UseNpgsqlDataSource()` on Marten (~1 hour); delete `docker-compose.yml` and the Tiltfile, or keep a parallel `compose.yml` for reference (~0 hours); rewrite the migrator to call `Database.MigrateAsync()` gated behind `IsRunMode`, and keep the `dotnet ef migrations bundle` pipeline for production (~2 hours); introduce a Postgres init SQL script for the two-role split and mount via `WithBindMount` (~2 hours); rewire the `[assembly: AssemblyFixture(typeof(RunCoachAppFactory))]` to wrap `DistributedApplicationTestingBuilder` + `WebApplicationFactory<Program>` (~3 hours); **resolve DataProtection `/keys` by either containerizing the API in the AppHost or moving DP keys to Postgres (the dominant cost — 4 to 12 hours depending on path chosen)**; tune OTel for Marten (`TrackConnections`, `TrackEventCounters`) and Wolverine (~1 hour). Total: **15 to 25 hours**. The DataProtection path is the principal variance driver.

## Cost of pivoting later (pre-MVP-1)

Same work as above **minus** the `docker-compose.yml` deletion and **plus** one additional task: the Slice 0 tests have accumulated test count by MVP-1, so the fixture rewrite (item 6) grows proportionally. Net: **18 to 30 hours**. The overhead of continuing to maintain Compose + Tilt through MVP-0 is **near zero** because the commitment is already in place and the tooling is stable. The opportunity cost is the Marten async-daemon observability experience you would have during MVP-0 — mitigable by adding Jaeger and an OTel Collector to the existing compose (estimated half day).

The arithmetic favors defer: pivot-now saves perhaps 3 to 5 hours over pivot-later, but spends that capital before any hosted target is committed — a decision Aspire is specifically meant to inform.

## Aspire lifecycle and version pinning

**Pin `Aspire.AppHost.Sdk` and all `Aspire.Hosting.*` packages to 13.2.2** (the current patch as of April 8, 2026, on NuGet). Avoid 13.2.0 (IDE-execution regressions for Azure Functions and class libraries, fixed in 13.2.1/13.2.2). **Do not pin to 9.x** — it is out of support; the `aspire update` CLI handles the 9→13 migration automatically. Expect to upgrade Aspire minors quarterly — treat this as a tooling cadence, not a framework migration. Keep `net10.0` as the `TargetFramework` because .NET 10 is LTS through November 2028; Aspire's versioning is decoupled, so the LTS stability comes from .NET, not Aspire.

## Single-developer ergonomics

Aspire's pitch is team-and-enterprise-friendly orchestration. For a solo developer the honest math: Aspire adds two projects (AppHost, ServiceDefaults), one opaque binary (DCP), a quarterly minor-version upgrade treadmill with no LTS, and a ~1-week learning curve to confidence. It removes multi-service `up` (already solved by `tilt up`), log aggregation (Tilt UI does this adequately), and — the one genuine and non-trivial removal — **OTel dashboard setup with zero config**. The explicit "Aspire tax" for small projects is acknowledged across practitioner writing: codewithmukesh, Oskar Dudycz, and Microsoft's own roadmap discussions all concede that single-API-plus-DB projects are below the payoff threshold.

## Decision triggers and where RunCoach sits

| Trigger | Aspire-favoring threshold | RunCoach today |
|---|---|---|
| Service count | 4+ services with cross-calls | 1 API + SPA + Postgres — **below** |
| Team size | 3+ devs or frequent onboarding | Solo — **below** |
| Deployment target | Azure Container Apps committed | Undetermined — **neutral** |
| Tracing requirements | Distributed traces across ≥3 hops | Marten daemon + LLM calls — **marginal**, addressable in compose |
| Dev/staging/prod parity | Weakly required | Strongly desired — **slight anti-signal** (Aspire is a dev tool, prod is codegen) |
| Stack composition | Pure .NET | .NET + React + Postgres + (future) Ollama — **mixed** |
| Existing orchestration | Green-field | Committed to Compose + Tilt as of March 2026 — **anti-signal** |

**Seven of seven dimensions point at defer-or-stay.** The marginal cases (tracing, deployment target) do not flip independently; they would need to converge — for example, MVP-1 commits to ACA **and** a second .NET service lands **and** cross-service tracing becomes a real debugging need. Until that convergence, the triggers do not fire.

## Credibility check on Aspire as "the future of .NET"

Aspire is credibly mainstream for new Azure-targeted .NET cloud apps and is Microsoft's first-party recommendation — the November 2025 .NET Conf keynotes, the 13.0 rebrand, and the CLI overhaul in 13.2 signal strong continued investment. The polyglot story (Python, JS, Go, Java) shipping in 13.0 is a serious move away from ".NET-only" framing. But adoption outside Microsoft samples is still **thin**. Real OSS adopters exist and are active — meysamhadeli's booking-microservices (~1.3k stars, .NET 10, Feb 2026), fpindej/netrock (~218 stars, Apr 2026 SvelteKit + .NET starter), neozhu/cleanaspire (~217 stars, Blazor WASM), petabridge/DrawTogether.NET (Akka.NET reference app, Apr 2026), MarcelMichau/fake-survey-generator (full ACA+Bicep example), Depechie/OpenTelemetryGrafana (works around in-memory dashboard), NikiforovAll/dependify (still pinned to 9.4.2 — a concrete example of a real project not yet having crossed the 9→13 bridge), victorfrye/microsoftgraveyard (real deployed SWA product), and SnowBankSDK's FoundationDB integration (third-party vendor publishing first-party-style hosting packages). That is a meaningful adoption signal, but the list is dominated by reference/starter repos and small independent products, not large-scale production .NET codebases. In 2026 Aspire is "mainstream-recommended, sparsely-adopted-in-large-production," with a non-trivial tail of practitioners like JasperFx explicitly waiting it out.

## Conclusion and next actions

Staying on Compose + Tilt for MVP-0 is the low-regret path: it costs nothing, preserves optionality, keeps JasperFx's tools on their maintainer-blessed configuration, and defers the commitment to a moment when better information exists (hosted-target choice, Marten/Wolverine Aspire trajectory, DataProtection pattern decision). Aspire's Compose publisher becoming stable in 13.2 is the key strategic unlock — it means a future pivot does not force a cloud-target choice, which was the core worry in R-046.

Concrete next actions. **Now, during Slice 0**: keep Compose + Tilt; add a `docker-compose.otel.yml` overlay with an OTel Collector + Jaeger container to close the Marten-daemon-observability gap (half day); wire Marten's OTel options (`TrackConnections`, `TrackEventCounters`) and add `"Marten"`, `"Wolverine"`, and `"RunCoach.Llm"` sources/meters to the existing OpenTelemetry configuration — **this work is transferable to Aspire later with zero rework**. **At MVP-1 trigger**: re-evaluate Aspire against the committed deployment target, Marten/Wolverine-Aspire state, and whether a second service exists; if adopting, pin `13.x` (whatever is current at that point), plan 18–30 hours, decide DataProtection strategy first (most likely: persist DP keys to Postgres via Marten/EF). **Standing re-trigger**: reconsider earlier if JasperFx publishes a first-class `Aspire.Hosting.Wolverine` package, if a second RunCoach service is planned, or if an MVP-1 ACA commitment happens before MVP-0 ships. None of those are likely in the Slice 0 window.

The March 2026 DEC-032 commitment was correct. Keep it. Revisit at MVP-1.