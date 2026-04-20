# Research Prompt: Batch 16b — R-049

# Production Secrets Management for ASP.NET Core 10 + Docker / Future Container Orchestrator (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For an ASP.NET Core 10 application that needs to handle multiple sensitive secrets across local-dev → CI → production for a single-developer side project on a near-term hosted trajectory, what are the current 2026 patterns and which combination fits RunCoach's stack and constraints?

## Context

I'm building MVP-0 of an AI-powered running coach (RunCoach). Slice 0 (`docs/specs/12-spec-slice-0-foundation/`) just landed and named several secrets the application now needs to handle but did not specify how to handle them in production:

- **DataProtection master key** — signs the ASP.NET Core Identity application cookie (per DEC-044) and the antiforgery tokens. Currently configured to persist to `/keys` mounted as a named Docker volume in dev. Production handling unspecified. Loss of this key invalidates every cookie and every antiforgery token — looks like a flaky bug.
- **JWT signing key** — registered as opt-in for the future iOS shim (HS256 symmetric); not used by the SPA but lives in `Program.cs` config-binding from day one.
- **Postgres role passwords** — Slice 0 documents two roles per R-046's recommendation: `runcoach_migrator` (DDL, used by EF migrations bundle) and `runcoach_app` (DML, used by the API at runtime). Both have passwords.
- **Anthropic API key** — used by `ClaudeCoachingLlm`. Today in user-secrets locally, `ANTHROPIC_API_KEY` env var in CI for eval cache record runs.
- **Future passkey credential signing key, refresh-token storage encryption key, OAuth client secrets** — all on the horizon when those features land.

Existing constraints:

- **No production environment exists yet.** Trajectory: solo-dev → friends/testers (MVP-1, hosted) → public beta. The hosted environment for MVP-1 is not yet chosen — could be a single VPS running Docker Compose, a managed PaaS (Render, Railway, Fly.io, Azure Container Apps), Kubernetes, or anything in between.
- **Single-developer side project.** Operational complexity must be justified. Adding a vault service that requires a separate cluster, a separate authentication layer, and a separate availability story is a real cost.
- **Dev story today:** `.NET user-secrets` for local secrets, `.env` files git-ignored, Docker Compose injects env vars from `.env`. CI uses GitHub Actions secrets.
- The project is public OSS (post-DEC-043). Anything committed must be safe to commit.
- Security rules in `CLAUDE.md`: never read/display/commit secrets; secrets go in env vars or .NET user-secrets locally.

The Slice 0 spec assumed "user-secrets locally, env var in CI/prod" without specifying the production secret-management layer. R-044's recommendation made the DataProtection-key handling load-bearing — without it, every container rebuild logs everyone out — and the bundle-as-Job production migration pattern (R-046) requires the `runcoach_migrator` password to be available at deploy time. The pieces don't compose without an answered "where do production secrets live?" question.

## Research Question

**Primary:** For the constraints above, what is the current 2026 best-practice pattern for managing the lifecycle (storage, rotation, distribution, bootstrap) of the named secrets across local-dev / CI / production, and what concrete combination of tools/services minimizes operational overhead while meeting OWASP / industry baseline security expectations?

**Sub-questions (must be actionable):**

1. **Strategy survey.** Compare each across: setup cost, operational burden, OWASP/baseline alignment, bootstrap-problem solution, rotation story, audit trail, GitHub Actions interop, multi-environment consistency, OSS-suitability:
   - **Plain env vars + GitHub Actions secrets + manual deploy-time injection** — the simplest baseline.
   - **`.NET user-secrets` (dev) + env vars (prod) with manual rotation** — current approximate state.
   - **HashiCorp Vault (self-hosted)** — full vault, full operational weight.
   - **HashiCorp Vault (HCP free tier)** — managed, free for small workloads.
   - **Doppler** — SaaS secret manager, GitHub Actions native, free tier exists.
   - **Infisical** — open-source secret manager, self-hostable or SaaS.
   - **Mozilla SOPS + age (or PGP)** — file-based encrypted secrets in the repo, decrypted at deploy time.
   - **Sealed Secrets (Bitnami)** — Kubernetes-specific, file-based encrypted secrets.
   - **Cloud-provider native** — Azure Key Vault, AWS Secrets Manager, Google Secret Manager. Pick the most likely RunCoach-relevant.
   - **PaaS-bundled** — Render env-var management, Fly.io secrets, Railway variables, Azure Container Apps secrets.

2. **DataProtection key persistence specifically.** The DataProtection key is unusual — it's not "a secret" so much as "an at-rest cryptographic state that must persist across restarts and be readable by every API instance." What's the recommended pattern in 2026 for a single-VPS deploy vs a multi-instance deploy vs a PaaS? Specifically:
   - File-system mount (works for single-instance only).
   - `PersistKeysToAzureBlobStorage` + `ProtectKeysWithAzureKeyVault` — the canonical Azure pattern.
   - `PersistKeysToStackExchangeRedis` — works for any deploy with Redis.
   - SQL-backed via `PersistKeysToDbContext<T>` — uses the same Postgres the app already has (interesting candidate for RunCoach).
   - Other 2026 options.

3. **Bootstrap problem.** A running app fetching a secret from a vault needs a credential to authenticate to the vault. How does that credential get there without recursing? Patterns to compare:
   - Workload identity (cloud-native: Azure Managed Identity, AWS IAM Role, Workload Identity Federation).
   - Token files (Kubernetes service-account tokens).
   - Pre-shared bootstrap secret (just env var injected by the orchestrator).
   - Vault Agent / sidecar that handles auth.

   For a side project that may start on a VPS and migrate to managed PaaS, what's the most-portable bootstrap?

4. **Local-dev parity.** The single biggest cost of secrets-management adoption is the dev workflow getting harder. What are the 2026 patterns for keeping the dev story as low-friction as `dotnet user-secrets set` while production uses a vault? Doppler + `doppler run`? Vault Agent? SOPS + decrypt-on-clone? Direnv? Something else?

5. **GitHub Actions secret consumption.** CI needs the Anthropic API key (for eval cache record runs), needs Postgres credentials (for integration tests against Testcontainers — these are random), and needs the future deployment credentials. What's the recommended GitHub Actions pattern for pulling from each candidate vault? OIDC federation? Action-specific plugins? Plain env injection?

6. **Rotation story.** The DataProtection key has its own rotation built in (`SetDefaultKeyLifetime`). The Anthropic API key needs human-driven rotation. Postgres role passwords need periodic rotation. What's the 2026 best-practice rotation cadence for each, and which candidate vaults make rotation a one-command operation vs a multi-step procedure?

7. **Single-developer overhead audit.** For each candidate strategy, what's the realistic ongoing maintenance cost (hours/month) for a one-person team? "Free tier of a SaaS vault" with one extra tool to learn vs "self-hosted Vault" with cluster maintenance is a 100× operational delta.

8. **OSS-public-repo posture.** RunCoach's repo is public. Are there secret-management patterns that are subtly broken in public-repo contexts? (e.g., GitHub Actions secret-leak prevention, fork PR access to secrets, `pull_request_target` foot-guns.)

9. **OWASP / regulatory baseline.** What does OWASP ASVS v5 actually require for secrets handling at the level of a small product handling personal training data? Does the answer change at the FTC HBNR boundary (DEC-020, pre-public-release)?

10. **Migration map.** If we adopt strategy X for MVP-0 and need to migrate to strategy Y at MVP-1 (because we've outgrown X) or pre-public-release (because compliance demands more), what's the cost? Highlight strategies that are "good enough now, no rewrite later."

11. **Marten / Wolverine interaction.** Wolverine 5+ has its own envelope-storage and may eventually carry secret data through messages. Does the chosen secret-management strategy interact with Wolverine's outbox? Does Marten have any secret-handling primitives worth using?

12. **Concrete recommendation given the trajectory.** RunCoach is starting on Docker Compose + Tilt locally with no hosted env. MVP-1 will likely deploy to either a single VPS, Render, Fly.io, or Azure Container Apps. Pick the secret-management combination that satisfies (a) MVP-0 dev today, (b) MVP-1 hosted in any of those targets without rework, (c) the FTC HBNR / pre-public-release escalation later.

## Why It Matters

- **Slice 0's `Program.cs` config-binding shape is decided now** — adding a vault binding later is rework.
- **The bundle-as-Job migration pattern (R-046) is unimplementable without an answered "where does the runcoach_migrator password come from at deploy time" question.**
- **DataProtection key loss = total session loss.** Cookies are RunCoach's whole auth substrate post-DEC-044. Mismanaged DataProtection persistence in production looks like a flaky-auth bug forever.
- **Public-repo posture creates secret-leak risk.** A misconfigured `pull_request_target` workflow is the standard public-repo secrets-disclosure vector.
- **Side-project overhead matters.** Choosing a vault that requires its own ops cluster is operationally expensive — but choosing "no vault" leaves the future pre-public-release compliance gap unfilled.
- **Pre-public-release compliance horizon (DEC-020 FTC HBNR + future ToS/privacy work) requires defensible secrets handling.** Cheaper to lay this foundation now.

## Deliverables

- **A concrete recommended combination** for (a) local-dev, (b) CI, (c) MVP-1 hosted on any of the likely targets, with rationale and an explicit operational-overhead estimate.
- **A capability matrix** comparing the candidate strategies across the eight axes in sub-question 1.
- **A DataProtection-key persistence recommendation** with the specific provider package (file-system / Azure Blob / Redis / SQL-backed via Postgres / other) the recommendation maps to, and the wiring snippet.
- **A bootstrap-credential pattern** appropriate for the recommended deploy targets.
- **A local-dev workflow sketch** that keeps the inner loop near-`dotnet user-secrets`-ergonomic.
- **A GitHub Actions snippet** showing how the recommended strategy plugs into both the eval-cache record workflow and the future deploy workflow.
- **A rotation procedure** for each named secret (DataProtection key, JWT signing key, Postgres role passwords, Anthropic API key) covering who/what/when.
- **A compliance escalation note** — what changes need to happen pre-public-release to meet the FTC HBNR / personal-health-data baseline.
- **A migration map** — if the recommended strategy is adopted now and a different one is needed later (more scale, more compliance), what changes?
- **Citations** — Microsoft Learn docs for ASP.NET Core 10 DataProtection, OWASP ASVS v5 secrets-handling section, Vault / Doppler / Infisical / SOPS official docs, and 2025–2026 community sources for the recommended pattern.

## Out of Scope

- Identity provider choice — RunCoach uses ASP.NET Core Identity directly per DEC-044; not switching to Auth0 / Keycloak / etc.
- Hardware Security Module / FIPS 140 certification — overkill for the trajectory.
- Multi-tenant secrets — RunCoach is multi-tenant by user (per Marten Conjoined), not multi-tenant by customer organization. Per-user secret encryption is not a concern at this level.
- Secret-scanning tools (TruffleHog, Gitleaks) — already covered by the dependency-review-action workflow per DEC-043; this prompt is about secrets that exist legitimately.
- The Anthropic API key rotation impact on eval cache fixtures — that's an LLM-eval ops concern, separate from the strategic secrets-management question.
