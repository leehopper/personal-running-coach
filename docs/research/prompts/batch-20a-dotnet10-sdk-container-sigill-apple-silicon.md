# Research Prompt: Batch 20a — R-063

# `dotnet restore` inside `mcr.microsoft.com/dotnet/sdk:10.0` SIGILLs on Apple Silicon (Colima / aarch64) for a .NET 10 solution with Marten + Wolverine + M.E.AI + Anthropic + OTel, while the same restore succeeds on the host and a trivial project restores cleanly in the same container (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

**Research Topic:** What is the canonical, durable 2026 setup for containerized `dotnet restore` of a realistic .NET 10 solution (Marten 8.x, Wolverine 5.x, Microsoft.Extensions.AI preview, Anthropic SDK, OpenTelemetry, Testcontainers) running on Apple Silicon under Colima — such that `tilt up` completes reliably, fast, and without SIGILL? Isolate the faulting code path, prescribe a fix that doesn't require every contributor to rediscover the workaround, and pin a known-good SDK image digest the repo can trust.

## Context

RunCoach is an ASP.NET Core 10 app with a solo-dev Docker Compose + Tilt loop. Tilt's `api` resource builds `backend/Dockerfile` which starts `FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build` and runs `dotnet restore RunCoach.slnx`. On a developer's macOS arm64 box the restore fails with:

```
Step 6/17 : RUN dotnet restore RunCoach.slnx
  Determining projects to restore...
Illegal instruction (core dumped)
The command '/bin/sh -c dotnet restore RunCoach.slnx' returned a non-zero code: 132
```

The failure is deterministic and blocks both `tilt up` (Path A in `CONTRIBUTING.md`) and any `docker compose build api`. It does NOT block Path B (host-run `dotnet run`), which is the primary inner loop per DEC-045 / R-050 — so the immediate PR (T02.6) can be validated via Path B, but the project's stated Tilt path is currently broken on Apple Silicon and the prompt author wants it "set up correctly so it is all durable and fast."

**What is ruled out:**

| Hypothesis | Evidence against |
|---|---|
| Host / project is broken | Host `dotnet restore RunCoach.slnx` with SDK 10.0.203 on `mise`-managed .NET (at `/Users/lee/.local/share/mise/dotnet-root/sdk`) succeeds in **574 ms** from `backend/`. |
| Arch emulation (QEMU / Rosetta) | `uname -m` = arm64; `colima status` = `arch: aarch64`; `docker info` = `linux/aarch64`; `docker image inspect mcr.microsoft.com/dotnet/sdk:10.0` = `arm64/linux`. End-to-end native. |
| Low Colima resources | Initial report was on 2 CPU / 2 GiB. Bumped to 4 CPU / 8 GiB via `colima start --cpu 4 --memory 8 --vm-type=vz`. SIGILL persists. |
| VZ vs QEMU VM driver mismatch | Tested both `--vm-type=vz` and `--vm-type=qemu` after `colima stop`. SIGILL identical on both. |
| SDK image version drift from host | Container `dotnet --version` = **10.0.203**, matching the host. Only one SDK installed in the image. Tag pushed 26 hours before the failure (`Created: 2026-04-21T14:58:33.140442884Z`, digest `sha256:8a90a473da5205a16979de99d2fc20975e922c68304f5c79d564e666dc3982fc`). |
| SDK image is broken universally | Running a fresh `dotnet new console` inside the **same container image and same Colima VM** (`docker run --rm mcr.microsoft.com/dotnet/sdk:10.0 sh -c 'cd /t && dotnet new console -o hi && cd hi && dotnet restore'`) succeeds in **28 ms** with exit 0. |
| NuGet signature verification | Probed by re-running the failing restore with `-e DOTNET_NUGET_SIGNATURE_VERIFICATION=false` set on the `docker run` invocation. SIGILL still fires after the "Determining projects to restore..." line. (Signature verification may still be a *contributing* factor if it's toggled differently than expected — but the env-var alone does not clear the failure.) |

**What remains:** the SIGILL is specific to the intersection of (containerized `dotnet restore`) × (this specific solution's package set) × (aarch64 Linux). The shortlist of packages RunCoach pulls that are plausible SIGILL triggers on arm64 Linux restore:

- **Marten 8.x** — `runcoach_events` schema + `IntegrateWithWolverine`; has native Postgres-adjacent dependencies.
- **WolverineFx 5.x** — Postgres outbox bound to the shared data source.
- **Microsoft.Extensions.AI** preview packages — bridges `IChatClient` to Anthropic; preview packages have historically shipped incomplete arm64 matrices.
- **Anthropic SDK 12.16.0** — bundles native System.Net.Http-adjacent dependencies.
- **OpenTelemetry.* 1.x** — including `Npgsql` instrumentation.
- **Testcontainers.PostgreSql** — references docker-client-adjacent native binaries; restores even in non-test projects if referenced transitively.
- **M.E.AI.Evaluation** — test-project-only but part of `RunCoach.slnx`.

The faulting instruction has not been captured via `strace`/`ltrace` yet; the container exits before the shell can wrap it. The restore fails *after* "Determining projects to restore..." prints, which places the SIGILL in NuGet's package resolution / signature verification / asset-writing phase, not project-file parsing.

Hostile-package-name hypothesis: the difference between the trivial-console-works path and the RunCoach-solution-fails path is the package set. A specific NuGet `.nupkg` whose package signature carries an instruction sequence the VM's arm64 feature set can't execute (e.g. PMULL2 / SHA3 / SVE on a VZ/QEMU VM without those bits exposed) would explain the SIGILL being reproducible but package-set-specific.

**What the caller wants:** not "try these three things and see." A durable setup: a specific SDK image digest or ENV or package-set adjustment that has been verified against this stack and will keep working across team machines for the duration of MVP-0. Plus, understanding of what caused the SIGILL so future Docker-SDK-bumps don't re-introduce the regression quietly.

## Research Question

**Primary:** For a .NET 10 solution with this exact package shape (Marten 8.x + WolverineFx 5.x + M.E.AI preview + Anthropic SDK + OpenTelemetry + Testcontainers) running inside `mcr.microsoft.com/dotnet/sdk:10.0` on an Apple Silicon Colima VM, what is the precise root cause of the `dotnet restore`-phase SIGILL, and what is the durable fix — Dockerfile change, SDK image-tag pin, package-level mitigation, or some combination — that makes `tilt up` both reliable and fast?

**Sub-questions:**

1. **Root cause isolation.** Is the SIGILL (a) a NuGet client code path (signature verification, package extraction, asset-file writer), (b) native code inside a specific package's `build/` targets that runs during restore (e.g. package-level MSBuild targets, `restore` hooks, source-generator load), (c) a glibc / icu / openssl binary inside the SDK image whose arm64 build targets CPU features that macOS Virtualization.framework or QEMU-on-Apple-Silicon doesn't expose, or (d) something else? Identify the specific instruction (or class of instructions) and the specific binary executing it.
2. **Identify the culprit package, if any.** Given the trivial `dotnet new console && dotnet restore` works in the same container but RunCoach's restore doesn't, binary-search the package set: which specific `PackageReference` (or transitive dependency) introduces the SIGILL? Propose a procedure (a minimal `.csproj` that reproduces the failure) and, if possible, name the package.
3. **Durable Dockerfile shape.** Given the root cause, what is the minimum Dockerfile change? Candidates include: pin a specific SDK digest known to work (`FROM mcr.microsoft.com/dotnet/sdk:10.0.203-bookworm-arm64v8@sha256:...`), switch to a different base image tag (`8.0.x`-style alpine, `jammy` rather than default debian, `-chiseled` variants), set build-time env vars (`DOTNET_NUGET_SIGNATURE_VERIFICATION`, `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT`, `DOTNET_RUNNING_IN_CONTAINER`, `NUGET_XMLDOC_MODE`), disable specific NuGet feed plugins, or pre-populate `/root/.nuget/packages` via a `dotnet restore --no-http-cache` in a multi-stage preamble. Recommend one; explain rejected alternatives.
4. **`DOTNET_NUGET_SIGNATURE_VERIFICATION=false` efficacy.** Our probe suggested it does not clear the SIGILL when set via `docker run -e`. Is it being set correctly? Does it need to be set via Dockerfile `ENV` rather than `docker run -e` to take effect for the `RUN dotnet restore` step's subprocess? Or is NuGet signature verification genuinely not the cause, and the probe correctly rules it out?
5. **Package-level mitigation.** If a specific package is culpable (sub-Q 2), can it be pinned to an earlier version that doesn't trip the issue, or swapped for a feature-equivalent alternative, without breaking RunCoach's current architecture (DEC-048, DEC-049, DEC-055)? What is the version range that works?
6. **Registry and variant choice.** Are there regression-resistant variants of the SDK image for arm64 Linux that the team should prefer — `-chiseled-extra`, `-alpine3.20-arm64v8`, `-jammy-chiseled-extra`, `mcr.microsoft.com/dotnet/nightly/sdk:10.0`, or the `ghcr.io/dotnet/dotnet:10.0.x` mirror? Any community signal (GitHub issues, dotnet/dotnet-docker issues, .NET release notes) about this specific SIGILL on arm64?
7. **Compatibility with the current Compose topology.** The recommended fix must not break: (a) the dev-cert PFX bind-mount at `/https/aspnetapp.pfx` (`USER $APP_UID` posture on the `runtime` stage), (b) the `HEALTHCHECK` that curls `/health`, (c) the `dotnet publish` step in the same Dockerfile, (d) the runtime image (`mcr.microsoft.com/dotnet/aspnet:10.0`) not having the same SIGILL on startup, (e) CI using the same Dockerfile on GitHub Actions' `ubuntu-latest` x86_64 runners (must not regress x86_64 path).
8. **Verification harness.** What is the minimum reproducer the team should keep under `docs/research/artifacts/` or in CI so a future SDK-image bump (we auto-bump `dotnet/sdk` via Dependabot) that silently re-introduces the SIGILL fails loudly before a contributor hits it on `tilt up`?
9. **Is Path A worth fixing at all, vs. deprecating?** Given Path B is the DEC-045 / R-050 primary inner loop and Tilt is the secondary "everything-in-Compose" convenience, is the right call to (a) fix Path A durably per sub-Q 3, (b) pin a known-good SDK digest as a temporary measure and document the investigation, or (c) formally deprecate the containerized-api Path A and remove it from `CONTRIBUTING.md`, keeping Compose only for infra (postgres, redis, pgadmin, aspire-dashboard)? If (c), preserve the CI container-image build path which still needs to work on x86_64 GitHub runners. Recommend one with rationale.

## Why It Matters

- **Developer onboarding.** A new contributor pulling the repo on a MacBook and running `tilt up` per `CONTRIBUTING.md` gets an opaque "Illegal instruction" error. They will not know to check Colima arch, VM driver, SDK digest, signature verification, or package versions. Every contributor hitting this costs an afternoon of triage. The documented workaround (Path B) works but contradicts the repo's primary `tilt up` story — the contradiction itself is the friction.
- **Dependabot surface.** Dependabot bumps the `mcr.microsoft.com/dotnet/sdk` tag on a schedule. Without a pinned-digest + verification-harness combination, a bad upstream push silently breaks the build for every contributor until someone investigates. This has already happened once (the 2026-04-21 push is the current state). It will happen again.
- **MVP-0 velocity.** Slice 0 has Unit 3 (frontend) and Slices 1–4 ahead. Every day the primary dev-loop is "broken on macOS arm64" is a day of cognitive tax on every session-start — even when the workaround (Path B) functions, the awareness that Tilt is broken leaks into every decision about adding new infra (do we wire it into Tilt? do we leave it Compose-only? is it worth it?). The prompt author wants the question settled once.
- **Blast radius of the wrong fix.** Pinning an SDK image digest stops Dependabot from moving forward — fine short-term, bad mid-term. Disabling signature verification is a real (if modest) supply-chain posture weakening. Switching to alpine or chiseled changes the glibc environment in ways that could bite a later workload. The research must surface these tradeoffs, not just recommend the first flag that clears the SIGILL.

## Deliverables

- **Root-cause diagnosis** — which code path faults, which instruction (if identifiable), which package (if a specific package is culpable), with primary-source citations (.NET runtime / NuGet-client GitHub issues, dotnet-docker issues, Marten / Wolverine release notes, Colima / VZ release notes, Apple Silicon arm64 feature documentation).
- **Minimum reproducer** — the smallest `.csproj` with `PackageReference`s that reproduces the SIGILL inside `mcr.microsoft.com/dotnet/sdk:10.0` on Apple Silicon Colima, for future regression guard.
- **Primary recommendation** — concrete diff for `backend/Dockerfile` (and / or `backend/Directory.Packages.props`, `docker-compose.yml`, `Tiltfile`, `CONTRIBUTING.md`) that makes `tilt up` reliable and fast. Show the diff, explain why it works, name the tradeoffs it introduces.
- **Alternatives considered and rejected** — what was tried in isolation, why it was not chosen: alpine/chiseled/jammy variants, ENV flags, image pinning, package downgrades, registry switches, deprecating Path A entirely.
- **Verification harness** — a CI job or smoke script that catches a future regression of this specific SIGILL on Apple Silicon before it merges.
- **Library / tool version pins** — SDK image digest + any adjusted package pins, with links to the changelog / release notes that justify each pin.
- **Gotchas, security implications, version compatibility notes** — in particular: (a) supply-chain posture impact of any NuGet-signature-verification change, (b) arm64 vs x86_64 CI parity (GitHub runners are x86_64 — the fix must not regress them), (c) how this interacts with Dependabot's weekly SDK-tag bump cadence, (d) what changes if the team later adopts Kubernetes or a cloud runtime that uses a different base image.

---

## Current Repo State (for the research agent to inspect if useful)

- `backend/Dockerfile` — multi-stage build from `mcr.microsoft.com/dotnet/sdk:10.0` → `mcr.microsoft.com/dotnet/aspnet:10.0`.
- `docker-compose.yml` — the `api` service wires HTTPS on 5001 with `ASPNETCORE_ENVIRONMENT=Development` + dev-cert PFX bind-mount.
- `Tiltfile` — the single `docker_compose('docker-compose.yml')` + `dc_resource` calls.
- `backend/RunCoach.slnx` + `backend/Directory.Packages.props` — the full package pin set (`PackageVersion Include=`) that currently SIGILLs on restore.
- `CONTRIBUTING.md` — documents Path A (`tilt up`) and Path B (host-run) as the two supported dev loops.
- `docs/decisions/decision-log.md` — DEC-045 (Aspire deferred to MVP-1; stay on Compose + Tilt), DEC-046 (SOPS + Postgres DataProtection), DEC-048 (Marten envelope storage), DEC-049 (host-config reload disabled on macOS arm64 — **relevant: an adjacent macOS-arm64-specific .NET 10 runtime issue already landed in this codebase**).
- Colima context: 0.10.1, `default` profile aarch64, 4 CPU / 8 GiB, Docker server 27.4.0.
- Host context: macOS Darwin 25.4.0, arm64 Apple Silicon, SDKs `/Users/lee/.local/share/mise/dotnet-root/sdk/10.0.203` and `10.0.202` (mise-managed).
- Empirical baselines: trivial in-container `dotnet new console && dotnet restore` = 28 ms success; host restore of `RunCoach.slnx` = 574 ms success; containerized restore of `RunCoach.slnx` = SIGILL / exit 132 / deterministic.
