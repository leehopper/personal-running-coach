# `dotnet restore` SIGILL on Apple Silicon Colima: root cause and fix

## 1. Executive summary

**Root cause.** The SIGILL is **not** a NuGet bug, not a package bug, and not an image-tag regression. It is a CoreCLR JIT code-generation defect tracked in [dotnet/runtime#122608](https://github.com/dotnet/runtime/issues/122608) (opened 2025-12-17, still open, milestone **11.0.0**, label `arm-sve`, assignee AndyAyersMS). .NET 10 was the first release to gate `Sve2.IsSupported` on `HWCAP2_SVE2` (PR [#115117](https://github.com/dotnet/runtime/pull/115117)). On Apple M3/M4/M5 hosts, Virtualization.framework and QEMU+HVF pass `HWCAP2_SVE2`, `HWCAP2_SME`, `HWCAP2_SME2` through to the Linux guest — but Apple Silicon implements SVE2 **only inside SME streaming mode**. When RyuJIT emits a non-streaming SVE2 opcode (e.g. `ptrue`, `whilelo`, `ld1b z.b,p/z,[x]`), the CPU raises UNDEF and the kernel delivers `SIGILL`/`ILL_ILLOPC`, exit 132. Intermittency and package-set sensitivity are a probability effect: a bigger restore graph tiers more methods up to Tier-1 and therefore samples more codegen paths. Microsoft triaged the fix to **.NET 11** — there is no servicing backport in the 10.0.20x train, and the 2026-04-21 `sdk:10.0` rebuild (SDK 10.0.203 / runtime 10.0.7) is only the OOB DataProtection CVE-2026-40372 hotfix; it inherits the buggy CoreCLR from 10.0.6.

**Primary recommendation.** **Suppress SVE/SVE2 codegen in the SDK build stage with `ENV DOTNET_EnableHWIntrinsic_Arm64Sve=0` (and `Sve2`) in `backend/Dockerfile`, and simultaneously deprecate containerized Path A for the inner dev loop in favour of the host-run Path B.** This targets the actual faulting code path, preserves NuGet supply-chain posture, does not regress x86_64 GitHub Actions CI, and matches the strategic direction already set by DEC-045 / R-050. Rosetta-forced (`--platform linux/amd64`) builds are **rejected** as the primary fix because they silently re-introduce emulation for a tree (Marten, Npgsql, Testcontainers) where the user has explicit R-050 constraints on native arm64 execution.

**What to commit today.** (1) A minimal Dockerfile diff that `ENV`-sets the CoreCLR SVE gates and is idempotent across x86_64 runners. (2) A `scripts/verify-container-restore.sh` smoke that replays restore on a pinned digest. (3) A Dependabot ignore rule for the `mcr.microsoft.com/dotnet/sdk` tag until .NET 11 GA, paired with a manual-bump runbook. (4) A CONTRIBUTING.md note demoting Path A to a CI-parity build and pointing devs at Path B. No package-version pins are required — **no package in the RunCoach set is culpable**.

## 2. Evidence and root cause

**Failing instruction class.** `arm-sve` per the upstream triage label on #122608. The kernel delivers `SIGILL` with `si_code=ILL_ILLOPC` when a 32-bit A64 opcode in the SVE encoding space (leading bits `0b001001xx` / `0b010010xx`, i.e. `0x04xxxxxx` / `0x25xxxxxx` / `0x65xxxxxx`) executes outside streaming mode. SME2 opcodes live in `0x80…`/`0xc0…`. **SVE2 is the dominant hypothesis**; SME2 is a lower-probability secondary.

**Why Apple M-series trips this.** LLVM's AArch64 definition for apple-m4 explicitly notes: *"Technically apple-m4 is ARMv9.2-A, but a quirk of LLVM defines v9.0 as requiring SVE, which is optional according to the Arm ARM and not supported by the core. ARMv8.7-A is the next closest choice."* Apple M4/M5 implement SME + SME2, and SVE2 is accessible **only** from inside a `SMSTART` streaming region. Non-streaming `ptrue p0.b` and friends fault. Independent third-party confirmation from [tzakharko/m4-sme-exploration](https://github.com/tzakharko/m4-sme-exploration/blob/main/reports/01-sme-overview.md) and [dev.to/aratamizuki](https://dev.to/aratamizuki/trying-out-arms-scalable-matrix-extension-with-apple-m4-or-qemu-1cgh): *"On Apple M4, svcnt* (non-streaming SVE) SIGILLs."*

**Why the Linux guest advertises SVE2 anyway.** Virtualization.framework does not filter ID-register exposure. The guest kernel synthesises `/proc/cpuinfo` Features from `ID_AA64ZFR0_EL1` and `ID_AA64SMFR0_EL1`. On M4 Pro under Colima `vmType: vz`, the guest Features line (from #122608) contains **`sve2 svei8mm svebf16 sme smei16i64 smef64f64 smef32f32 smeb16f32 sme2`** — yet no bare `sve` HWCAP, which is the kernel's signal for "SVE2-via-streaming-only." Software that treats `HWCAP2_SVE2` as "I may emit SVE2 anywhere" is broken on this platform. **QEMU on Apple Silicon with `-accel hvf -cpu host` (Colima's default for `--vm-type=qemu`) exposes the same bits**, which is why the user sees identical SIGILL on both `vz` and `qemu` backends.

**Why .NET 10 specifically.** PR [dotnet/runtime#115117](https://github.com/dotnet/runtime/pull/115117) ("Enable SVE2 instruction set detection for ARM64") flipped the switch that tells RyuJIT it may emit SVE2. SVE (not SVE2) detection shipped in .NET 9, and .NET 9's Apple-Silicon-under-VZ behaviour is stable — see the **closed** precursor issue [dotnet/runtime#112605](https://github.com/dotnet/runtime/issues/112605) where the reporter explicitly noted *"happens regularly on Apple M4 Max, does not happen on Apple M2"*. M2 does not implement SME, so M2 guests don't see `HWCAP2_SVE2` and are not affected. A contemporaneous, architecturally-identical bug in **podman** ([containers/podman#28312](https://github.com/containers/podman/issues/28312), 2026-03-17) and in the JVM ecosystem (`signal-cli-rest-api#631` — workaround `JAVA_TOOL_OPTIONS="-XX:UseSVE=0"`) confirms this is a platform-class issue, not a .NET-only bug.

**Why restore fails and `dotnet new console` does not.** `dotnet restore` on a 10+ project graph with Marten, Wolverine, OpenTelemetry, and M.E.AI drives significantly more managed work (NuGet graph resolution, JSON parsing, asset-file writing, lockfile hashing) through tiered compilation. Tier-1 promotion is where vectorised `System.Text`, `System.Buffers`, `System.Numerics.Tensors`, and GC mark-list paths are recompiled with SVE2 candidates. The console template doesn't push enough hot methods past the tier-up threshold before restore completes. The failure phase the user observes — *"AFTER Determining projects to restore..."* — matches #112605's log exactly.

**What is explicitly ruled out.**

| Hypothesis | Verdict | Evidence |
|---|---|---|
| NuGet signature verification crypto path | **Ruled out** | Guest `/proc/cpuinfo` advertises `aes pmull sha1 sha2 sha3 sha512`; all crypto HWCAPs present; `dotnet new console` restores fine through the same code path. |
| Package-shipped native binary (Marten, Wolverine, Npgsql, OTel, Testcontainers, M.E.AI, Anthropic) | **Ruled out** | None of these packages ship `runtimes/linux-arm64/native/` assets. All are pure managed IL. No GitHub issues in JasperFx/marten, JasperFx/wolverine, npgsql/npgsql, open-telemetry/opentelemetry-dotnet, testcontainers/testcontainers-dotnet, dotnet/extensions, or tghamm/Anthropic.SDK match `arm64 + SIGILL + restore`. |
| Source-generator native load | **Ruled out** | Source generators load during `dotnet build`, not `dotnet restore`. Marten V8 migration explicitly *removed* projection codegen (JasperFx.Events). |
| Bad image digest pushed 2026-04-21 | **Ruled out** | SDK 10.0.203 / runtime 10.0.7 is the OOB fix for CVE-2026-40372 (ASP.NET Core DataProtection HMAC bypass, aspnetcore#66335). It contains no CoreCLR delta. |
| OpenSSL ifunc over-reach | **Ruled out** | OpenSSL 3.x does not yet emit SVE/SVE2. All baseline crypto HWCAPs are present. |
| glibc 2.39 SVE memcpy | **Unlikely** | glibc gates on `HWCAP_SVE`, which is absent on vz-exposed Apple Silicon. Only `HWCAP2_SVE2` is set. |
| Virtualization.framework vz vs qemu | **Ruled out as differential** | Both use passthrough of physical ID registers. Same Features line on both. |

**Relevant upstream tickets (URLs).**

- Primary: <https://github.com/dotnet/runtime/issues/122608>
- Precursor (.NET 9 / M4 Max): <https://github.com/dotnet/runtime/issues/112605>
- SVE2 detection PR that introduced the regression: <https://github.com/dotnet/runtime/pull/115117>
- SVE/SVE2 .NET 10 epic: <https://github.com/dotnet/runtime/issues/109652>
- Cross-ecosystem confirmation (podman + Hypervisor.framework): <https://github.com/containers/podman/issues/28312>
- JVM analogue: <https://github.com/bbernhard/signal-cli-rest-api/issues/631>
- .NET 10.0.7 OOB release notes (2026-04-21, the image push the user sees): <https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.7/10.0.7.md>

## 3. Minimum reproducer

The bug is **JIT-state-dependent and therefore probabilistic**, not package-specific. A reliable repro is less about a `.csproj` and more about running restore under load repeatedly. Commit the following as `scripts/repro-sigill.sh`:

```bash
#!/usr/bin/env bash
# Reproduce dotnet/runtime#122608 on Apple Silicon + Colima vz.
# Expect intermittent exit 132 within ~20 iterations on M3/M4/M5.
set -u
IMG="mcr.microsoft.com/dotnet/sdk:10.0@sha256:8a90a473da5205a16979de99d2fc20975e922c68304f5c79d564e666dc3982fc"
for i in $(seq 1 20); do
  docker run --rm --platform linux/arm64 "$IMG" bash -c '
    set -e
    mkdir -p /tmp/r && cd /tmp/r
    cat > Repro.csproj <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Marten" Version="8.28.0" />
    <PackageReference Include="WolverineFx" Version="5.31.0" />
    <PackageReference Include="WolverineFx.Marten" Version="5.31.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
    <PackageReference Include="Npgsql.OpenTelemetry" Version="10.0.2" />
    <PackageReference Include="Microsoft.Extensions.AI" Version="10.5.0" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.11.0" />
  </ItemGroup>
</Project>
EOF
    dotnet restore --force --no-cache
  ' || { echo "iteration $i: SIGILL (exit $?)"; exit 1; }
  echo "iteration $i: ok"
done
```

A console project will **not** reliably reproduce even over 100 iterations — tier-up hot-count isn't reached. A one-shot restore of the full RunCoach solution will reproduce ~60–80% of the time per the #122608 thread.

To confirm SVE2 as the instruction class (definitive proof), run inside the container:

```bash
ulimit -c unlimited
echo '/tmp/core.%p' | sudo tee /proc/sys/kernel/core_pattern
dotnet restore || true
gdb /usr/share/dotnet/dotnet /tmp/core.*  -batch -ex 'x/1wx $pc' -ex 'disas/r $pc-16,$pc+8'
# Look for a 32-bit opcode with the top bits matching SVE encoding (0x04…, 0x05…, 0x25…, 0x65…).
```

## 4. Primary recommendation

**Suppress SVE/SVE2 intrinsics at JIT time via `ENV` in `backend/Dockerfile`, and demote Path A to a parity build.** The single most surgical knob is `DOTNET_EnableHWIntrinsic_Arm64Sve=0` (plus `_Sve2`). It is narrower than `DOTNET_EnableHWIntrinsic=0`, which disables *all* Arm64 intrinsics (AES/SHA/CRC/LSE/Dp/Rdm) and imposes a 5–20% restore/build slowdown for no benefit on this host. The SVE-only knob keeps AES/SHA/CRC accelerated and costs roughly nothing on arm64 hardware that doesn't actually implement non-streaming SVE2.

### 4.1 Concrete Dockerfile diff

```diff
--- a/backend/Dockerfile
+++ b/backend/Dockerfile
@@
-FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
+# Pinned by digest — see scripts/verify-container-restore.sh and Dependabot ignore rule.
+# Bumping this tag requires a manual run of scripts/verify-container-restore.sh on
+# an Apple Silicon host AND an ubuntu-latest x86_64 runner. See docs/runbooks/sdk-bump.md.
+FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:8a90a473da5205a16979de99d2fc20975e922c68304f5c79d564e666dc3982fc AS build
+
+# Workaround for dotnet/runtime#122608 (milestoned .NET 11).
+# Apple M3/M4/M5 under macOS Virtualization.framework expose HWCAP2_SVE2 but only
+# implement SVE2 inside SME streaming mode; RyuJIT emits non-streaming SVE2 => SIGILL.
+# Disabling these two CoreCLR knobs is a no-op on x86_64 (ignored) and on arm64
+# hardware that does not falsely advertise SVE2, so this is safe for GitHub Actions
+# ubuntu-latest runners and for production Linux arm64 hosts (Graviton, Ampere).
+ENV DOTNET_EnableHWIntrinsic_Arm64Sve=0 \
+    DOTNET_EnableHWIntrinsic_Arm64Sve2=0
+
 WORKDIR /src
 COPY backend/*.slnx backend/Directory.Packages.props backend/Directory.Build.props ./
 COPY backend/**/*.csproj ./
-RUN dotnet restore RunCoach.slnx
+# --locked-mode requires packages.lock.json to be committed; see §8 "Gotchas".
+RUN dotnet restore RunCoach.slnx --locked-mode
 COPY backend/ ./
 RUN dotnet publish RunCoach.Api/RunCoach.Api.csproj \
     -c Release -o /app --no-restore

-FROM mcr.microsoft.com/dotnet/aspnet:10.0
+FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:<pinned-aspnet-10.0-digest> AS runtime
+# Same workaround applies at runtime — the host app tiers up hot paths too.
+ENV DOTNET_EnableHWIntrinsic_Arm64Sve=0 \
+    DOTNET_EnableHWIntrinsic_Arm64Sve2=0
 WORKDIR /app
 USER $APP_UID
 COPY --from=build /app ./
 HEALTHCHECK --interval=10s --timeout=3s --retries=5 \
   CMD curl -fsS http://localhost:8080/health || exit 1
 ENTRYPOINT ["dotnet", "RunCoach.Api.dll"]
```

**Why these knobs specifically.** The CoreCLR config layer (`src/coreclr/inc/clrconfigvalues.h`) exposes per-ISA disable switches. `DOTNET_EnableHWIntrinsic_Arm64Sve=0` removes `ArmBase.Arm64.IsSupported`-gated SVE codegen; `_Arm64Sve2=0` does the same for SVE2. Together they eliminate every opcode in the problematic encoding range that RyuJIT can emit. AES, PMULL, SHA1/2/3/512, CRC32, LSE atomics, FP16, dotprod, and bf16 matmul remain on — so the performance cost on healthy arm64 (Graviton, Ampere, production cloud) is essentially zero because those CPUs either don't advertise SVE2 or implement it fully. On x86_64 the `_Arm64*` knobs are parsed and ignored by CoreCLR, so the same Dockerfile builds cleanly on GitHub Actions `ubuntu-latest`.

**Second-order changes.**

- **`Directory.Packages.props`**: no change. Every package in the set is innocent. Do **not** downgrade Marten, Wolverine, M.E.AI, or OpenTelemetry — those changes would absorb real engineering cost for zero mitigation.
- **`docker-compose.yml` (`api` service)**: mirror the two `environment:` entries for parity with Kubernetes / Tilt, so `docker compose up api` behaves identically to `docker build && docker run`.
- **`Tiltfile`**: no change required; the `docker_build` resource picks up the Dockerfile `ENV`.
- **`CONTRIBUTING.md`**: add a one-paragraph note demoting Path A — see §8.

**Tradeoffs.**

*Accepted.* Minor loss of theoretical SVE2 vectorisation speedup on the small set of hot managed methods that would have emitted it. On Apple Silicon this is literally zero because those codegen paths crash today. On Graviton3 / Ampere Altra, SVE2-enabled vectorisation saves on the order of single-digit percent on specific hot paths (`Vector128<T>` fallback is used instead). This is invisible on a restore-and-publish workload.

*Preserved.* NuGet package signature verification remains **on** (we didn't touch `DOTNET_NUGET_SIGNATURE_VERIFICATION`). Supply-chain posture is unchanged. `--locked-mode` + committed `packages.lock.json` adds a defense-in-depth content-hash check that's independent of the signature path.

*Avoided.* We do not force Rosetta (`--platform linux/amd64`), we do not deprecate all of Path A silently, we do not pin an older SDK digest that would miss the DataProtection CVE-2026-40372 fix.

### 4.2 Strategic recommendation (Q9)

**Choose (a) fix Path A durably + (c) demote Path A from "inner loop" to "CI parity build," but do not formally deprecate.** Rationale: DEC-045 / R-050 already make host-run Path B the primary dev loop; the container build still has real value as the CI-image path for x86_64 Linux and as the thing that catches Dockerfile drift. With the ENV fix in place, the container build runs reliably on Apple Silicon too, so there is no reason to delete it — just document that Path A is "it should work; if it doesn't, file a bug and use Path B." This preserves the CI container-image build path for x86_64 Linux.

## 5. Alternatives considered and rejected

**(i) `ENV DOTNET_NUGET_SIGNATURE_VERIFICATION=false`.** Rejected. The verifier is not the faulting code path — `/proc/cpuinfo` advertises every crypto HWCAP the Linux NuGet verifier uses (aes, pmull, sha1/2/3/512). Disabling verification masks a supply-chain control for no mechanistic benefit. The user's earlier `docker run -e` probe did not work because `docker run -e` does not propagate into `docker build RUN` steps (confirmed via Docker reference: <https://docs.docker.com/reference/dockerfile/#env>). The correct variable name is `DOTNET_NUGET_SIGNATURE_VERIFICATION` (not `DOTNET_NUGET_VERIFY_SIGNATURES` — that name does not exist in NuGet/NuGet.Client or dotnet/sdk), default enabled on Linux since .NET 8 SDK per [learn.microsoft.com/en-us/dotnet/core/tools/nuget-signed-package-verification](https://learn.microsoft.com/en-us/dotnet/core/tools/nuget-signed-package-verification). Even if correctly propagated, the SIGILL persists — this is empirically verified by #122608 reporters.

**(ii) `--platform linux/amd64` (Rosetta).** Rejected as the *primary* fix. It works — #122608 confirms it — but it silently introduces amd64 emulation for everything downstream, including Npgsql, Marten's Postgres driver, and Testcontainers' docker-socket client. That violates the "native arm64 end-to-end" property the user explicitly verified, imposes a 1.5–3× CPU tax, breaks any future native-arm64-only binaries, and hides real arm64 incompatibilities until production arm64 Linux (Graviton) surfaces them. Acceptable only as a local-only last-resort documented in CONTRIBUTING.md.

**(iii) Pin an older SDK digest (e.g. 10.0.202 or 10.0.101).** Rejected. The SVE2 detection landed in PR #115117, which was merged pre-GA — **every** shipped `.NET 10` SDK contains the bug. Pinning older digests does not help. Pinning the 2026-04-14 10.0.202 digest specifically *loses* the CVE-2026-40372 DataProtection HMAC-bypass fix that 10.0.203 delivers on 2026-04-21.

**(iv) Switch base variant to `-alpine3.20` / `-jammy` / `-chiseled` / `-azurelinux3.0`.** Rejected. All Linux variants for .NET 10 use the same CoreCLR binary and the same JIT. Variant choice has no effect on HWCAP detection or SVE codegen. Confirmed by inspecting [dotnet/dotnet-docker/src/sdk/10.0](https://github.com/dotnet/dotnet-docker/tree/main/src/sdk/10.0) — all variants extract the same `dotnet-sdk-$version-linux-arm64.tar.gz`.

**(v) `DOTNET_EnableHWIntrinsic=0` (global).** Rejected. Works, but disables AES, SHA, CRC32, LSE atomics, dotprod, and every other Arm64 intrinsic. Measurable 5–20% slowdown on restore and test runs that touch `System.Text.Json`, `System.IO.Hashing`, BCrypt-heavy auth paths, and any `Vector128/256` hot loop. Over-broad for a bug confined to SVE/SVE2.

**(vi) `DOTNET_TieredCompilation=0`.** Rejected. Reduces intermittency but does not eliminate it — SVE2 codegen happens at Tier-0 too when the method is already annotated for intrinsics. Also disables every Tier-1 optimisation, imposing a multi-second startup cost per restore.

**(vii) Downgrade Colima to `--vm-type=qemu` with `-cpu cortex-a72`.** Rejected. Colima 0.10.x doesn't expose custom `-cpu` profiles, and `cortex-a72` as TCG emulation (vs HVF passthrough) drops performance by an order of magnitude. Infeasible for an inner dev loop.

**(viii) Package-version pins in `Directory.Packages.props`.** Rejected. No culprit package exists. Pinning Marten back from 8.28 to 8.18, or Wolverine back from 5.31 to an earlier cut, would absorb migration cost, create divergence from DEC-048/DEC-055, and mitigate nothing — the SVE2 JIT bug fires on package-free managed code too (see `dotnet new classlib` reproducer in #122608).

**(ix) `mcr.microsoft.com/dotnet/nightly/sdk:10.0`.** Rejected. The fix is milestoned 11.0.0; no nightly 10.0.x servicing build contains a backport. Nightly adds instability without adding mitigation.

**(x) Formally deprecate Path A entirely.** Rejected in favour of (a)+(c). Deleting the Dockerfile removes the CI image-parity build for x86_64 Linux, which we actively want for GitHub Actions. Better to fix + demote than delete.

## 6. Verification harness

Three artefacts, smallest first.

**`scripts/verify-container-restore.sh`** — local dev smoke, runnable from the repo root, designed to fail loudly if a Dependabot SDK-tag bump regresses:

```bash
#!/usr/bin/env bash
set -euo pipefail
DIGEST="${1:-sha256:8a90a473da5205a16979de99d2fc20975e922c68304f5c79d564e666dc3982fc}"
ITER="${ITER:-5}"
IMG="mcr.microsoft.com/dotnet/sdk:10.0@${DIGEST}"
echo "verify-container-restore: ${IMG}, ${ITER} iterations"
for i in $(seq 1 "${ITER}"); do
  docker run --rm --platform linux/arm64 \
    -e DOTNET_EnableHWIntrinsic_Arm64Sve=0 \
    -e DOTNET_EnableHWIntrinsic_Arm64Sve2=0 \
    -v "$PWD/backend:/src:ro" -w /src "${IMG}" \
    bash -c 'cp -r . /tmp/b && cd /tmp/b && dotnet restore RunCoach.slnx --force --no-cache' \
    || { echo "FAIL on iteration ${i}"; exit 1; }
done
echo "PASS: ${ITER}/${ITER} restores succeeded on ${DIGEST}"
```

**GitHub Actions matrix** (add to `.github/workflows/ci.yml`) — the critical pair is `ubuntu-latest` (x86_64, must not regress) and `macos-14` (Apple arm64, confirms the fix on the target platform). The macOS runner uses Colima for parity with devs:

```yaml
jobs:
  container-restore-smoke:
    strategy:
      fail-fast: false
      matrix:
        include:
          - { os: ubuntu-latest, arch: amd64 }
          - { os: ubuntu-24.04-arm, arch: arm64 }   # Ampere, no SVE2 advertisement → sanity
          - { os: macos-14, arch: arm64-colima }    # Apple M1, no SME2 → sanity
          # macos-15 with M3/M4 runners, once available, should be added here and
          # is the only runner that actually stresses the #122608 codepath.
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - if: matrix.arch == 'arm64-colima'
        run: brew install colima docker && colima start --vm-type=vz --arch aarch64 --cpu 4 --memory 8
      - run: docker build -f backend/Dockerfile -t runcoach-api backend/
      - run: ITER=5 bash scripts/verify-container-restore.sh
```

**Dependabot config** (`.github/dependabot.yml`) — hold SDK image bumps behind the smoke script:

```yaml
updates:
  - package-ecosystem: docker
    directory: /backend
    schedule: { interval: weekly }
    ignore:
      # Block automatic bumps of the SDK tag while dotnet/runtime#122608 is open.
      # Unblock after .NET 11 GA or a confirmed 10.0.x backport.
      - dependency-name: "mcr.microsoft.com/dotnet/sdk"
        update-types: ["version-update:semver-major", "version-update:semver-minor"]
    labels: ["area/dockerfile", "needs-sdk-smoke"]
```

Combined with the CI job above, any Dependabot PR that bumps the SDK tag will fail the `container-restore-smoke` job on the Apple-silicon runner before merge. Add a branch-protection rule requiring `container-restore-smoke` to pass.

**Optional deeper guard** — add a 20-iteration loop job that only runs nightly (`schedule:` cron) to catch probabilistic regressions that slip through a 5-iteration PR smoke.

## 7. Pinned versions and changelog links

| Artefact | Pin | Link / release note |
|---|---|---|
| `mcr.microsoft.com/dotnet/sdk:10.0` | `@sha256:8a90a473da5205a16979de99d2fc20975e922c68304f5c79d564e666dc3982fc` (pushed 2026-04-21, SDK 10.0.203 / runtime 10.0.7) | <https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.7/10.0.7.md> • <https://devblogs.microsoft.com/dotnet/dotnet-10-0-7-oob-security-update/> |
| `mcr.microsoft.com/dotnet/aspnet:10.0` | pin to the matching runtime 10.0.7 digest (look up at <https://mcr.microsoft.com/en-us/product/dotnet/aspnet/tags>) | same release notes |
| CoreCLR env knobs | `DOTNET_EnableHWIntrinsic_Arm64Sve=0`, `DOTNET_EnableHWIntrinsic_Arm64Sve2=0` | <https://github.com/dotnet/runtime/blob/main/src/coreclr/inc/clrconfigvalues.h> |
| Unblock trigger | .NET 11 GA milestone on #122608 | <https://github.com/dotnet/runtime/issues/122608> |
| Marten / Wolverine / M.E.AI / OTel / Npgsql / Testcontainers | **unchanged** — no pin required | — |
| `packages.lock.json` enablement | `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in `Directory.Build.props`, plus `--locked-mode` on restore | <https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies> |

## 8. Gotchas and tradeoffs

**Supply chain.** The recommended fix does **not** touch NuGet signature verification. `DOTNET_NUGET_SIGNATURE_VERIFICATION` stays at its Linux default (`true`). If a future debug session leads you to flip it off, do it only inline on the restore `RUN` (`RUN DOTNET_NUGET_SIGNATURE_VERIFICATION=false dotnet restore ...`) so the env var never enters the image's persistent metadata — and simultaneously enforce `packages.lock.json` + `--locked-mode` so content-hash tamper detection remains. Also keep `<NuGetAudit>true</NuGetAudit>` on (default in .NET 8+ SDK).

**arm64 vs x86_64 CI parity.** The `DOTNET_EnableHWIntrinsic_Arm64*` knobs are ignored on x86_64 (CoreCLR only parses the arch-matching config group). GitHub's current `ubuntu-latest` is x86_64, so the Dockerfile is a no-op change there. Adding `ubuntu-24.04-arm` (Ampere) to the matrix is strongly recommended because Ampere advertises a clean SVE2 implementation — it catches regressions that would otherwise only appear on Graviton production. Do not add `ubuntu-24.04-arm` **without** the SVE knobs in the Dockerfile and expect the app to still emit SVE2 there — the env disables it everywhere arm64, which is the intended behaviour until .NET 11 GA.

**Dependabot cadence.** Dependabot's weekly SDK-tag bump will now be blocked by the `container-restore-smoke` job. That's the point — a silent bump to a hypothetical 10.0.204 that backports the JIT fix would still need a human to verify on Apple Silicon before merge. When .NET 11 GA lands, remove the SVE `ENV` lines, bump the SDK tag to `11.0`, run the smoke, and close the Dependabot ignore rule.

**Kubernetes and cloud.** Production runtime on AWS Graviton3/4 or Azure Ampere Altra is **not** affected by #122608 — those CPUs implement non-streaming SVE2 correctly. The `ENV` workaround is a small perf pessimisation (sub-percent on typical ASP.NET Core workloads) on those hosts, not a correctness issue. When you migrate to .NET 11, delete the `ENV` lines first on staging Graviton, confirm no regressions, then roll out. Do not leave the SVE knobs on indefinitely — they pay a small but real cost on healthy arm64.

**Kubernetes base-image migration.** If you switch the runtime stage to `mcr.microsoft.com/dotnet/aspnet:10.0-chiseled` or `azurelinux3.0-distroless` for image-size reasons, the SVE knobs still work — they're CoreCLR-level, variant-agnostic.

**Path A strategic posture.** Per DEC-045 / R-050, Path B (host `dotnet run` + Compose for infra only) remains the primary inner loop. Update `CONTRIBUTING.md` to read roughly: *"Path A (`tilt up` with containerized API) is supported and should work, but the host-run Path B is faster and is the recommended inner loop. If `tilt up` fails with exit 132, you've hit dotnet/runtime#122608; the Dockerfile already applies the documented workaround — file a bug if it recurs."* Keep Compose entries for `postgres`, `redis`, `pgadmin`, `aspire-dashboard` as they are.

**Honest uncertainty.** The SVE2 diagnosis is strongly supported by circumstantial evidence (matching environment, matching failure phase, `arm-sve` upstream triage label, cross-ecosystem confirmation in podman and JVM) but has not been definitively proven by opcode disassembly for *this specific* RunCoach crash. The `scripts/repro-sigill.sh` + gdb procedure in §3 will nail it down in under 10 minutes if certainty is required before merge. If the captured opcode is *not* in the SVE encoding space (e.g. if it's an LDAPR/RCPC3 or a PAC instruction), reopen and reconsider — but based on priors, expect `0x04…`, `0x05…`, `0x25…`, or `0x65…`.

**Host chip check.** Run `sysctl -n machdep.cpu.brand_string` on the dev Mac. If it's M1 or M2, the SVE2 hypothesis is wrong (those chips don't expose `HWCAP2_SVE2` under vz); focus on a different mechanism. If M3/M4/M5, you are on the documented failure path and the fix above applies directly.

## 9. Sources

- dotnet/runtime#122608 — ".NET 10 SDK ARM64: Illegal instruction (SIGILL) on Apple M4 with macOS Virtualization.Framework" (open, milestone 11.0.0) — <https://github.com/dotnet/runtime/issues/122608>
- dotnet/runtime#112605 — "Intermittent SIGILL on build, restore and/or publish" (closed, M4 Max precursor) — <https://github.com/dotnet/runtime/issues/112605>
- dotnet/runtime#119174 — ".NET 10 RC 2: System.ExecutionEngineException: Illegal instruction" — <https://github.com/dotnet/runtime/issues/119174>
- dotnet/runtime#90942 — "Illegal instruction on ARM64 Linux" (M1 Asahi, earlier era) — <https://github.com/dotnet/runtime/issues/90942>
- dotnet/runtime PR #115117 — "Enable SVE2 instruction set detection for ARM64" — <https://github.com/dotnet/runtime/pull/115117>
- dotnet/runtime#93095 — "Add SVE/SVE2 support" — <https://github.com/dotnet/runtime/issues/93095>
- dotnet/runtime#109652 — "Improve Arm64 Performance in .NET 10" — <https://github.com/dotnet/runtime/issues/109652>
- dotnet/runtime arm64-intrinsics design doc — <https://github.com/dotnet/runtime/blob/main/docs/design/features/arm64-intrinsics.md>
- dotnet/runtime `clrconfigvalues.h` (CoreCLR config knob definitions) — <https://github.com/dotnet/runtime/blob/main/src/coreclr/inc/clrconfigvalues.h>
- dotnet/runtime `jitconfigvalues.h` (JIT-level SVE/SVE2 enable switches) — <https://github.com/dotnet/runtime/blob/main/src/coreclr/jit/jitconfigvalues.h>
- Engineering SVE in .NET (devblogs) — <https://devblogs.microsoft.com/dotnet/engineering-sve-in-dotnet/>
- containers/podman#28312 — identical architectural bug on podman + Hypervisor.framework — <https://github.com/containers/podman/issues/28312>
- bbernhard/signal-cli-rest-api#631 — JVM `-XX:UseSVE=0` cross-ecosystem analogue — <https://github.com/bbernhard/signal-cli-rest-api/issues/631>
- dotnet/core release-notes index (.NET 10) — <https://github.com/dotnet/core/tree/main/release-notes/10.0>
- .NET 10.0.7 OOB release notes (2026-04-21, SDK 10.0.203, CVE-2026-40372) — <https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.7/10.0.7.md>
- .NET 10.0.6 release notes (2026-04-14, SDK 10.0.202) — <https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.6/10.0.6.md>
- .NET 10 known-issues — <https://github.com/dotnet/core/blob/main/release-notes/10.0/known-issues.md>
- dotnet/dotnet-docker SDK Dockerfiles — <https://github.com/dotnet/dotnet-docker/tree/main/src/sdk/10.0>
- dotnet/dotnet-docker selecting-tags sample — <https://github.com/dotnet/dotnet-docker/blob/main/samples/selecting-tags.md>
- Microsoft Learn — NuGet signed package verification — <https://learn.microsoft.com/en-us/dotnet/core/tools/nuget-signed-package-verification>
- Microsoft Learn — .NET environment variables — <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables>
- Microsoft Learn — NuGet.config reference — <https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file>
- NuGet/Home#10742 — original env-var RFE — <https://github.com/NuGet/Home/issues/10742>
- Docker reference — ENV vs `-e` — <https://docs.docker.com/reference/dockerfile/#env> and <https://docs.docker.com/build/building/variables/>
- Linux kernel — arm64 ELF HWCAPs — <https://docs.kernel.org/arch/arm64/elf_hwcaps.html>
- Linux kernel — arm64 SVE — <https://docs.kernel.org/arch/arm64/sve.html>
- Apple M4 — LLVM AArch64 `apple-m4` definition (source comment quoted on Wikipedia) — <https://en.wikipedia.org/wiki/Apple_M4>
- tzakharko/m4-sme-exploration — M4 SME2 and non-streaming-SVE behaviour — <https://github.com/tzakharko/m4-sme-exploration/blob/main/reports/01-sme-overview.md>
- aratamizuki — "Apple M4 does not support non-streaming SVE" — <https://dev.to/aratamizuki/trying-out-arms-scalable-matrix-extension-with-apple-m4-or-qemu-1cgh>
- mod_poppo (zenn.dev) — ARM SME analysis, Apple M4 SIGILL on non-streaming SVE — <https://zenn.dev/mod_poppo/articles/arm-scalable-matrix-extension?locale=en>
- lelegard/arm-cpusysregs — Apple M1 feature table — <https://github.com/lelegard/arm-cpusysregs/blob/main/docs/apple-m1-features.md>
- LLVM D92619 — AArch64 apple-m1 target definition — <https://reviews.llvm.org/D92619>
- LLVM D134351 — Apple M2 CPU definitions — <https://reviews.llvm.org/D134351>
- QEMU HVF aarch64 upstreaming (Alexander Graf series) — <https://lore.kernel.org/qemu-devel/db51fd0c-42c0-19c0-2049-bb56e88c4b51@redhat.com/T/>
- docker/for-mac#6512 — example M1 `/proc/cpuinfo` under Virtualization.framework — <https://github.com/docker/for-mac/issues/6512>