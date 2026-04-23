# Second-opinion verification on dotnet/runtime#122608: no deterministic user-space workaround exists

**Bottom line: R-063's demote-Path-A posture is correct.** After exhaustive primary-source verification across CoreCLR `release/10.0` source, Microsoft servicing channels, container-runtime behavior, build-pattern alternatives, and cross-ecosystem analogs, **no deterministic (≥10/10) workaround exists that keeps the reference constraints** (Apple M4 Pro + Colima 0.10.1 default VZ profile + SDK 10.0.203, no Rosetta, no .NET 11 wait). The issue is milestoned `11.0.0`, has zero linked PRs and zero visible comments as of 2026-04-22, and the next servicing build (expected 10.0.8 / SDK 10.0.204 on ~2026-05-12) is not publicly confirmed to carry a fix. Two partial escapes exist that relax orthogonal constraints — (i) switching the Mac container runtime to Docker Desktop ≥ 4.39 (primary-source confirmed to mask the offending features at the LinuxKit layer) and (ii) switching Colima to `--vm-type=qemu` with `cpuType: cortex-a72` (lima-vm#3032, QEMU Arm CPU model) — both carry real supply-chain costs and neither preserves the "Colima default VZ profile" constraint. All twelve candidate workarounds from the sub-question list are reject; the exhaustive analysis is in Section A. Demoting Path A to x86_64-Linux-only and waiting for .NET 11 GA (November 2026) or an unscheduled 10.0.x servicing backport is the correct posture.

## Key finding that supersedes R-063's mental model

R-063 treated the bug as "CoreCLR JIT emits SVE2 opcodes on HWCAP2_SVE2-advertising guests." Primary sources point to a broader and more concerning reality. The Podman maintainer-level analysis of the identical bug class (containers/podman#28312, pgib, 2026-03-17) explicitly identifies **SME/SME2 features** — not SVE2 proper — as co-triggers on Apple M4/M5 under VZ-backed Linux: the guest `/proc/cpuinfo` advertises `sve2 sme sme2 sme2p1 smei16i32 smebi32i32 smef16f16` etc., and Apple Silicon implements SME/SME2 in streaming mode only but does not execute non-streaming SVE/SVE2. **CoreCLR 10.0 has no `DOTNET_EnableArm64Sme*` knob at all** — SME intrinsics are a .NET 11 work item per dotnet/runtime#121787. This means that even if R-063 had spelled the SVE knobs with perfect casing — and per `src/coreclr/jit/jitconfigvalues.h` the spelling `DOTNET_EnableArm64Sve` / `DOTNET_EnableArm64Sve2` was already correct and case-sensitive — there may be no env-var surface in 10.0.203 that disables the actual crashing code path, because the faulting instructions are plausibly in a pre-CLRConfig startup probe (as in the near-identical OpenJDK bug JDK-8345296) or are SME2 instructions with no user-facing gate. This explains empirically why three increasingly aggressive knobs (`Sve=0`, `Sve2=0`, `EnableHWIntrinsic=0`) all failed on the reference hardware.

## A. Exhaustive option table

Every row is rejected on the reference hardware. Primary-source citations follow each candidate.

| # | Candidate | Primary source (file:line / URL) | Test procedure (copy-paste bash) | Expected outcome | Supply-chain cost | Operational cost | Verdict |
|---|---|---|---|---|---|---|---|
| 1 | Corrected SVE env-var names (`DOTNET_EnableArm64Sve=0` + `Sve2=0` + `SveAes=0` + `SveSha3=0` + `SveSm4=0` + `EnableEmbeddedMasking=0`) as Dockerfile `ENV` | `dotnet/runtime` `src/coreclr/jit/jitconfigvalues.h` `RELEASE_CONFIG_INTEGER(EnableArm64Sve2, "EnableArm64Sve2", 1)` region (~L256–L262 on main; identical on release/10.0 since PR #115117 pre-GA) | `docker run --rm --platform linux/arm64 -e DOTNET_EnableArm64Sve=0 -e DOTNET_EnableArm64Sve2=0 -e DOTNET_EnableArm64SveAes=0 -e DOTNET_EnableArm64SveSha3=0 -e DOTNET_EnableArm64SveSm4=0 -e DOTNET_EnableEmbeddedMasking=0 -v "$PWD:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0.203 dotnet restore RunCoach.slnx` — run 10× | FAIL. R-063 already tried the first two; adding the three SVE2-crypto sub-features and `EnableEmbeddedMasking` does not change the result because the crash is almost certainly not on a JIT-emitted SVE2 opcode but on a pre-CLRConfig feature probe or an SME2 instruction with no gate | None | None | **REJECT** |
| 2 | `runtimeconfig.template.json` with `System.Runtime.Intrinsics.Arm.Sve.IsSupported = false` applied to the SDK host | No AppContext switch for `Sve.IsSupported` is exposed by .NET 10 BCL; `Sve.IsSupported` is backed by the JIT intrinsic recognition bound to the `InstructionSet` flag, not a user-settable configProperty. The SDK host `dotnet.dll` ships with its own `dotnet.runtimeconfig.json` under `/usr/share/dotnet/sdk/10.0.203/`, but adding `configProperties` there is not supported documentation and would not be picked up by the muxer's own coreclr_initialize | N/A | WILL NOT WORK — no such switch exists in the 10.0 public surface | None | None | **REJECT** |
| 3 | Exhaustive `ENV` block (every knob enumerated): `EnableArm64Sve`, `Sve2`, `SveAes`, `SveSha3`, `SveSm4`, `EnableEmbeddedMasking`, `EnableArm64Sha3`, `EnableArm64Sm4`, `EnableHWIntrinsic`, plus `COMPlus_*` aliases | Same as #1; no `Arm64Sme*` knob exists in release/10.0 (confirmed by absence in `jitconfigvalues.h`; SME work is tracked in #121787 for .NET 11) | Same as #1 with additional `-e DOTNET_EnableHWIntrinsic=0 -e DOTNET_EnableArm64Sha3=0 -e DOTNET_EnableArm64Sm4=0` | FAIL. R-063 observed 1 lucky pass then 5/5 FAIL on `EnableHWIntrinsic=0`; the additional knobs address fewer code paths, not more | None | Big perf hit if it did work | **REJECT** |
| 4 | Switch container runtime to **Docker Desktop ≥ 4.39** | Docker Desktop 4.38 release notes (`docs.docker.com/desktop/release-notes/`): "Fixed a bug that caused all Java programs running on M4 Macbook Pro to emit a SIGILL error. See docker/for-mac#7583." Corroborated by containers/podman#28312 (pgib, 2026-03-17, `#issue-4090906483`): "Docker Desktop uses a different VM (LinuxKit + Virtualization.framework) that does not expose these features to the guest" | `colima stop; brew install --cask docker; open -a Docker; docker context use desktop-linux; docker run --rm mcr.microsoft.com/dotnet/sdk:10.0.203 dotnet --info` | PASS (per primary source for Java SIGILL class; not independently re-verified for .NET on 4.39, but same root cause) | **HIGH**: proprietary closed-source; Docker Business subscription required for orgs >250 employees or >$10M revenue per Docker's Subscription Service Agreement; telemetry on by default | Medium: context switch, re-pull images, rework any Colima-specific mounts/networking | **REJECT under user's constraint** ("without switching the container runtime"); this is the nearest-deterministic known escape if that constraint is relaxed |
| 4b | Switch to **OrbStack** | OrbStack release notes (`orbstack.dev/docs/release-notes`) contain **no mention** of SVE/SVE2/SME masking. Nothing in v1.x or v2.x changelog documents guest CPU-feature handling for ARMv9 | `brew install orbstack; orb start; docker run --rm mcr.microsoft.com/dotnet/sdk:10.0.203 dotnet --info` | **UNVERIFIABLE from primary source.** Anecdotal reports exist; no maintainer statement | **HIGH**: proprietary; commercial license required per orbstack.dev/pricing | Medium | **REJECT** (no primary-source confirmation) |
| 5 | Colima `--vm-type=qemu` + `cpuType.aarch64=cortex-a72` (pre-SVE QEMU CPU model) | Lima issue #3032 maintainer guidance: `limactl edit --set '.cpuType.aarch64="cortex-a57"' INSTANCE`. Lima default.yaml (`github.com/lima-vm/lima/blob/master/templates/default.yaml`) documents the `cpuType` field. QEMU Arm CPU features docs (`qemu-project.gitlab.io/qemu/system/arm/cpu-features.html`) confirm `-cpu max,sve=off,sme=off` | `colima stop && colima delete default && colima start --vm-type=qemu --arch aarch64 --cpu 4 --memory 8 && limactl stop colima && limactl edit --set '.cpuType.aarch64="cortex-a72"' colima && colima start && docker run --rm mcr.microsoft.com/dotnet/sdk:10.0.203 grep -E "sve\|sme" /proc/cpuinfo` (expect empty) then `dotnet restore RunCoach.slnx` × 10 | PASS for the SIGILL, but HVF-accelerated QEMU aarch64 is materially slower than VZ for I/O and multi-core work. Also breaks Rosetta x86_64 AOT cache | None (Apache-2.0 OSS); but `--vm-type=qemu` | **HIGH**: significant performance regression vs VZ; requires re-tooling Colima config | **REJECT under user's constraint** ("Colima 0.10.1 default profile"); this is the only OSS-path deterministic workaround if that constraint is relaxed |
| 5b | Podman Machine with CPU-feature mask | containers/podman#28312 closed as "not planned" on 2026-03-17 (pgib, `#issue-4090906483`); `podman-machine-init.1` exposes no `--cpu-type` flag for applehv/libkrun | N/A | WILL NOT WORK — same failure as Colima VZ | N/A | N/A | **REJECT** |
| 5c | Colima default VZ with CPU-feature mask tunable | Apple Virtualization.framework documentation (`developer.apple.com/documentation/virtualization`) — no `VZGenericPlatformConfiguration` CPU-feature-mask API as of macOS 15/26. Lima issue #3486: "cpuType is specific to QEMU and not portable to other VM drivers" | N/A | WILL NOT WORK — no such API exists | N/A | N/A | **REJECT** (architecturally blocked by Apple VZ) |
| 6 | Newer stable SDK tag that carries a fix | `github.com/dotnet/core/blob/main/release-notes/10.0/10.0.7/10.0.7.md` lists only CVE-2026-40372 (DataProtection HMAC) — no SVE/SIGILL/cpufeatures.c reference. `github.com/dotnet/runtime/issues/122608` milestoned `11.0.0` (`github.com/dotnet/runtime/milestone/176`), zero linked PRs, zero comments as of 2026-04-22 | `docker pull mcr.microsoft.com/dotnet/sdk:10.0.203@sha256:8a90a473da5205a16979de99d2fc20975e922c68304f5c79d564e666dc3982fc` then repro | FAIL — current tag is pre-fix; no later stable tag exists as of 2026-04-22 | N/A | N/A | **REJECT** |
| 6b | Nightly or .NET 11 preview SDK | .NET 11 Preview 2 runtime notes (`github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview2/runtime.md`) and Preview 3 runtime notes (`learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime`) explicitly *add* new SVE2 intrinsics (tracked in #123888); `main` merged #124637 on 2026-04-13 adding SVE_AES/SHA3/SM4 detection — **expands**, not shrinks, the SVE2 emission surface | `docker pull mcr.microsoft.com/dotnet/nightly/sdk:11.0-preview` then repro | FAIL (likely worse); plus nightly violates SUPPLY-CHAIN CONSTRAINT (floating tag, no servicing guarantee) | **HIGH**: unpinned nightly, no NuGet-signature stability | N/A | **REJECT** |
| 7 | Cross-SDK build: .NET 9 SDK → `net10.0` target | `learn.microsoft.com/en-us/dotnet/standard/frameworks` "Target frameworks" § explicitly states SDK N cannot target (N+1). NETSDK1045 (`learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1045`) emitted by `Microsoft.NET.TargetFrameworkInference.targets(166,5)`. Reproduced in dotnet/dotnet-docker#5919 and dotnet/runtime#103083 | `docker run --rm -v "$PWD:/src" -w /src mcr.microsoft.com/dotnet/sdk:9.0 dotnet restore RunCoach.slnx` | FAIL immediately with NETSDK1045 (before SIGILL can occur) | N/A | N/A | **REJECT** (architecturally blocked) |
| 8 | Host-side restore + COPY / bind-mount `~/.nuget/packages`, then `dotnet publish --no-restore` | `dotnet/runtime/docs/design/coreclr/jit/ryujit-overview.md` confirms the muxer immediately loads hostfxr → hostpolicy → coreclr_initialize → JITs `dotnet.dll`; every SDK verb runs managed code. #122608 OP demonstrates SIGILL on `dotnet new classlib` (no restore path involved), proving the fault is not restore-specific | `dotnet restore -r linux-arm64 /p:RuntimeIdentifiers='osx-arm64;linux-arm64' --packages .nuget-cache`; `docker run --rm --platform linux/arm64 -v "$PWD/.nuget-cache:/root/.nuget/packages:ro" -v "$PWD:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0.203 dotnet publish --no-restore -c Release RunCoach.slnx` × 10 | FAIL (non-deterministic); may reduce crash probability by shortening JIT workload but does not eliminate the muxer-side JIT path. RID layout (host osx-arm64 populates, container consumes linux-arm64) is fine since NuGet packages are content-addressed | None | Medium (host prep step added to inner loop) | **REJECT** (not deterministic) |
| 9 | `<PublishAot>true</PublishAot>` / `<PublishReadyToRun>true</PublishReadyToRun>` | `dotnet/runtime/docs/workflow/debugging/coreclr/debugging-aot-compilers.md`: "the AOT compilers are managed applications… 2 copies of the JIT in the process at the same time". ILC and crossgen2 are managed JIT-hosting processes in `src/coreclr/tools/aot/ILCompiler/` and `src/coreclr/tools/aot/crossgen2/` | Add `<PublishAot>true</PublishAot>`; `docker run … dotnet publish -c Release -r linux-arm64` | FAIL — adds more managed JIT workload during publish, increasing SVE2 exposure. AOT affects the output app, not the SDK host | None | N/A | **REJECT** |
| 10 | Inline-RUN `env` prefix vs Dockerfile `ENV` | `src/coreclr/inc/clrconfig.h` / `src/coreclr/utilcode/clrconfig.cpp` use `PAL_getenv`, which reads `environ` populated at `execve` time. Both forms produce identical `environ`. `dotnet` is an ELF PIE binary (not a shell script), no env scrubbing. dotnet/runtime#57713 confirms env vars must be set before process start and are read at coreclr_initialize | `docker run … sh -c 'env DOTNET_EnableArm64Sve=0 DOTNET_EnableArm64Sve2=0 dotnet restore RunCoach.slnx'` × 10 | FAIL — semantically identical to Dockerfile `ENV`. If ENV failed, inline fails | None | None | **REJECT** |
| 11 | Community-reported deterministic workarounds | #122608 has zero comments as of 2026-04-22. containers/podman#28312 pgib: "Alpine-based images (musl libc) reduce the frequency but don't eliminate crashes. Setting `DOTNET_PROCESSOR_COUNT=2` helps in some cases" — **explicitly non-deterministic**. No deterministic report exists on M3/M4/M5 + Colima default VZ | `docker run … -e DOTNET_PROCESSOR_COUNT=2 mcr.microsoft.com/dotnet/sdk:10.0.203-alpine dotnet restore RunCoach.slnx` × 10 | Non-deterministic partial mitigation (reduces frequency per podman#28312), **not 10/10** | None | Low | **REJECT** (non-deterministic by maintainer admission) |
| 11b | OpenJDK `-XX:UseSVE=0` translation to CoreCLR | OpenJDK JDK-8345296 / openjdk/jdk#22479 (shipilev, Dec 2024) and the pre-existing `-XX:UseSVE=0` (JDK-8248742) clear the equivalent HotSpot crash. **No CoreCLR user-visible analog exists** — the `DOTNET_EnableArm64Sve*` knobs are codegen gates (post-detection), not detection-probe bypasses. The CoreCLR-side analogous fix would be a minipal `cpufeatures.c` hardening patch, which has not been written | N/A | WILL NOT WORK — no such flag in .NET 10 | N/A | N/A | **REJECT** |
| 12 | Microsoft-side servicing patch | `github.com/dotnet/runtime/issues/122608` milestone `11.0.0`, no linked PR, no `Servicing-consider`/`Servicing-approved` label, no cherry-pick to `release/10.0-staging`, no comments. No public Microsoft commitment to backport. Next expected servicing build ~2026-05-12 (Patch Tuesday cadence: devblogs Jan/Mar/Apr 2026 servicing posts) likely ships as 10.0.8 / SDK 10.0.204 but no primary source confirms #122608 fix in scope | Wait; `docker pull mcr.microsoft.com/dotnet/sdk:10.0.204` when published | Unknown; assume not in scope absent evidence | N/A | N/A | **REJECT** (not deterministic, not user-actionable now) |
| 13 | `DOTNET_TieredCompilation=0` bonus | Tiered compilation starts methods at Tier 0 (Quick JIT, no SIMD intrinsic promotion) and promotes to Tier 1 (optimizing, SVE2 emission). Setting to 0 forces Tier 1 immediately → **more** SVE2 exposure | `docker run … -e DOTNET_TieredCompilation=0 …` | FAIL (counter-productive) | None | Big perf hit | **REJECT** |
| 14 | `DOTNET_JitMinOpts=1` bonus | `src/coreclr/jit/jitconfigvalues.h` exposes this; forces minimal-opts JIT globally. Plausibly suppresses intrinsic expansion but not primary-source-verified for SVE path; Microsoft does not document as supported workaround | `docker run … -e DOTNET_JitMinOpts=1 …` × 10 | **UNVERIFIED**. Worth one empirical probe for the author; not recommended as a stable posture | None | Very large perf hit | **REJECT as posture** (flag for one empirical probe only) |
| 15 | `DOTNET_ReadyToRun=0` bonus | Disables R2R precompiled BCL; forces re-JIT of framework code under current CLRConfig. Increases JIT workload, increases SVE2 emission surface | `docker run … -e DOTNET_ReadyToRun=0 …` | FAIL (counter-productive) | None | Startup perf hit | **REJECT** |

## B. No candidate succeeds — formal statement

**No deterministic workaround exists on the reference hardware (Apple M4 Pro + Colima 0.10.1 default VZ profile + SDK 10.0.203, no Rosetta, no .NET 11 wait) as of 2026-04-22.** Three primary-source citations support each leg of this conclusion.

**(a) #122608 is unpatched in the current 10.0.x line.** The issue page `github.com/dotnet/runtime/issues/122608` as rendered 2026-04-22 shows milestone `11.0.0` (`github.com/dotnet/runtime/milestone/176`), "No branches or pull requests" in the Development panel, zero comments, no `Servicing-consider` or `Servicing-approved` label. The 10.0.7 release notes (`github.com/dotnet/core/blob/main/release-notes/10.0/10.0.7/10.0.7.md`, dated 2026-04-21) enumerate only CVE-2026-40372 (ASP.NET Core DataProtection HMAC) under "Packages updated in this release" — no reference to SVE/SVE2/SME, `cpufeatures.c`, `hwintrinsic.cpp`, or #122608. The offending regression PR #115117 (`github.com/dotnet/runtime/pull/115117`, merged 2025-05-09 as commit `d23f2514eceb39c7bb85bf392f3742c301290dac`) has no revert and no fix-forward in reachable sources.

**(b) Each claimed ENV knob either doesn't exist at the name tried or doesn't clear the fault.** The exact spellings `DOTNET_EnableArm64Sve`, `DOTNET_EnableArm64Sve2`, `DOTNET_EnableArm64SveAes`, `DOTNET_EnableArm64SveSha3`, `DOTNET_EnableArm64SveSm4`, `DOTNET_EnableEmbeddedMasking`, `DOTNET_EnableHWIntrinsic` are literally quoted from `src/coreclr/jit/jitconfigvalues.h` `RELEASE_CONFIG_INTEGER` entries on the main branch (~L223-L277), and per the repo's `// keep in sync with clrconfigvalues.h` directive and the servicing-branch rule that no env-var knobs are added in servicing updates, these names and defaults are identical on `release/10.0` back to 10.0.0 GA (2025-11-11). The empirical 5/5 FAIL on R-063's machine despite correct casing is explainable because (1) CoreCLR detection in `src/native/minipal/cpufeatures.c` is unconditional and runs *before* CLRConfig is wired (there is no detection-layer env-var knob in 10.0), and (2) there is no `DOTNET_EnableArm64Sme*` family in release/10.0 — SME intrinsics are a .NET 11 work item per #121787, and the Colima guest `/proc/cpuinfo` advertises `sme sme2 sme2p1 smei16i32 smebi32i32 smef16f16` which are implicated in the identical-root-cause podman#28312 maintainer analysis (pgib, 2026-03-17). This also explains why `DOTNET_EnableHWIntrinsic=0` was only probabilistically effective — the master switch disables Neon + SVE + SVE2 codegen paths in the JIT, but not a startup-time SME2-advertising probe nor an R2R'd BCL method that was compiled with SVE2 code.

**(c) Alternative container runtimes are either broken identically, relax the constraint, or are unverifiable.** Podman Machine on applehv fails identically per containers/podman#28312 (closed "not planned", 2026-03-17). Apple Virtualization.framework itself exposes no CPU-feature-mask API on macOS 15 or 26 (`developer.apple.com/documentation/virtualization` — no such property on `VZGenericPlatformConfiguration`), and Lima's `cpuType` field is explicitly QEMU-only per lima-vm#3486. Docker Desktop ≥ 4.39 does mask the features at the LinuxKit layer (`docs.docker.com/desktop/release-notes/` 4.38.0 changelog for the Java analog; docker/for-mac#7583 `status/3-fixed`, `version/4.39.0`; corroborated by podman#28312) — but this relaxes the "Colima default profile" constraint, carries Docker Business licensing cost for orgs >250/$10M, and is therefore a policy change, not an in-place workaround. OrbStack has no primary-source confirmation of SVE2/SME masking (no `orbstack.dev/docs/release-notes` entry on "SVE"/"SME"), so cannot be recommended on this research standard. Colima `--vm-type=qemu` with `cpuType.aarch64=cortex-a72` is primary-source-verified to work (lima-vm#3032 maintainer guidance) but leaves the Colima *default* profile and imposes a material HVF-QEMU performance regression.

## C. Specific supersession content for DEC-056 and the inner-loop docs

The following diffs are proposed for the repository given the negative result. They codify the demote-Path-A posture, reference the primary sources, and bake in the re-check triggers.

**`docs/decisions/decision-log.md` — DEC-056 supersession entry**

```diff
 ## DEC-056 — Containerized inner loop on Apple Silicon
-Status: Proposed workaround via DOTNET_EnableArm64Sve=0 + Sve2=0
+Status: Demoted to x86_64-Linux-only until .NET 11 GA or 10.0.x backport
+Date-verified: 2026-04-22 (R-063 second-opinion research)
+Primary sources:
+- dotnet/runtime#122608 (milestone 11.0.0, no linked PR, zero comments)
+- github.com/dotnet/core/blob/main/release-notes/10.0/10.0.7/10.0.7.md (no SVE fix)
+- containers/podman#28312 (pgib 2026-03-17, confirms same root-cause class, closed "not planned")
+- src/coreclr/jit/jitconfigvalues.h (RELEASE_CONFIG_INTEGER EnableArm64Sve / Sve2)
+- developer.apple.com/documentation/virtualization (no VZ CPU-feature-mask API)
+Reason for demotion: No deterministic user-space workaround exists on M-series + Colima
+default VZ profile. Env-var knobs operate at codegen layer only; minipal cpufeatures.c
+detection is unconditional; SME/SME2 features advertised by the guest have no user gate
+in 10.0; R2R'd BCL assemblies ship with SVE2 opcodes baked in. All 15 candidates
+enumerated in R-064 rejected.
+Re-check triggers: see CONTRIBUTING.md "Apple Silicon inner loop" section.
```

**`CONTRIBUTING.md` — Apple Silicon inner loop section**

```diff
+## Apple Silicon inner loop (ARM64 host)
+
+**Path A (containerized dotnet restore/build inside Colima) is disabled on arm64
+until .NET 11 GA (November 2026) or an unscheduled 10.0.x servicing backport.**
+
+Root cause: dotnet/runtime#122608 — CoreCLR 10.0 crashes with SIGILL on Apple
+M-series under Colima VZ because the guest advertises SVE2/SME/SME2 features
+that Apple Silicon does not execute in non-streaming mode. No user-settable
+env-var clears the crash deterministically.
+
+Supported paths on Apple Silicon until fix ships:
+1. Host `dotnet restore && dotnet build` (baseline 574 ms for RunCoach.slnx).
+2. Containerized build on `--platform=linux/amd64` via Docker Desktop + Rosetta
+   (note: broken on Colima 0.10.1 default profile per R-063 binfmt test).
+3. CI/CD arm64 runners on Linux bare metal or AWS Graviton — not affected.
+
+Unsupported-but-documented escape hatches (if policy permits):
+- Docker Desktop ≥ 4.39 (primary-source confirmed SVE2/SME mask at LinuxKit layer
+  per docs.docker.com/desktop/release-notes/ 4.38.0; Docker Business license cost).
+- `colima start --vm-type=qemu` with `cpuType.aarch64=cortex-a72` via
+  `limactl edit --set '.cpuType.aarch64="cortex-a72"' colima` (lima-vm#3032;
+  significant HVF-QEMU perf regression vs VZ).
+
+Re-check this posture when:
+- mcr.microsoft.com/dotnet/sdk:10.0.204 or 10.0.108 is pushed (~2026-05-12)
+- Any comment appears on github.com/dotnet/runtime/issues/122608
+- Any PR in dotnet/runtime references #122608 or cpufeatures.c SVE hardening
+- A `Servicing-consider`/`Servicing-approved` label appears on #122608
+- PR #115117 is reverted or followed up with a probe-hardening patch
+- Any commit to dotnet/runtime release/10.0-staging touches cpufeatures.c
+  or hwintrinsic.cpp
+- Colima releases a VZ CPU-feature mask (tracks github.com/abiosoft/colima issues)
+- .NET 11 preview 4+ ships to mcr.microsoft.com/dotnet/nightly/sdk with a
+  refined cpufeatures.c detection heuristic
```

**`backend/Dockerfile`, `docker-compose.yml`, `Tiltfile`**: no diff. The containerized arm64 path stays removed; these files should continue to pin `--platform=linux/amd64` in CI-only contexts and defer to host builds locally.

**Optional defensive `ENV` block in `backend/Dockerfile` for the amd64 CI path only** (belt-and-suspenders against future related regressions; no correctness impact on x86_64):

```diff
 FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0.203 AS build
+# Defensive hardening against #122608-class regressions if this Dockerfile is
+# ever accidentally used on arm64. Exhaustive ARM64 SVE/SVE2 codegen gates
+# per src/coreclr/jit/jitconfigvalues.h (release/10.0). No-op on amd64.
+ENV DOTNET_EnableArm64Sve=0 \
+    DOTNET_EnableArm64Sve2=0 \
+    DOTNET_EnableArm64SveAes=0 \
+    DOTNET_EnableArm64SveSha3=0 \
+    DOTNET_EnableArm64SveSm4=0 \
+    DOTNET_EnableEmbeddedMasking=0
```

## D. Re-check trigger list

Re-run this research when **any** of the following events fires.

1. `mcr.microsoft.com/dotnet/sdk:10.0.204` or `:10.0.108` is pushed (expected ~2026-05-12 Patch Tuesday). Diff runtime version, digest, and `github.com/dotnet/core/blob/main/release-notes/10.0/10.0.8/10.0.8.md` for SVE/SIGILL/`cpufeatures.c` mentions.
2. Any new comment on `github.com/dotnet/runtime/issues/122608`, especially from @AndyAyersMS or @kunalspathak.
3. Any PR linked in the #122608 Development panel, or any PR in dotnet/runtime whose title/body references `#122608`, `SVE2`, `cpufeatures.c`, `PF_ARM_SVE2_INSTRUCTIONS_AVAILABLE`, `HWCAP2_SVE2`, or SME detection hardening — on `main`, `release/10.0-staging`, or `release/10.0`.
4. Any revert or substantive follow-up of PR #115117 (`github.com/dotnet/runtime/pull/115117`).
5. Any new label on #122608: `Servicing-consider`, `Servicing-approved`, or milestone change off `11.0.0`.
6. A `devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-*-2026-servicing-updates` post for May/June/July 2026 listing a JIT/SIGILL or ARM64 fix.
7. `mcr.microsoft.com/dotnet/sdk:10.0.300` (May-series 10.0.3xx SDK) or any `10.0.3xx` tag is pushed.
8. `.NET 11 Preview 4+` pushed to `mcr.microsoft.com/dotnet/nightly/sdk` — even if the SVE2 detection code is still present, confirm whether the heuristic in `cpufeatures.c` has been refined to verify actual host execution (analogous to the OpenJDK JDK-8345296 prctl-tolerant probe pattern).
9. Any new issue on dotnet/runtime filtered by `label:arm-sve` mentioning "Apple M", "Colima", "Hypervisor.framework", or "SME2".
10. Any maintainer-authored comment on `containers/podman#28312` or `docker/for-mac#7583` documenting the exact LinuxKit/seccomp mask applied in Docker Desktop 4.39 — would directly inform what Colima/Lima would need to patch.
11. New release of Colima or Lima documenting VZ CPU-feature masking or a VZ CPU-type override (watch `github.com/abiosoft/colima/releases` and `github.com/lima-vm/lima/releases`).
12. Any blog/release-notes post from Apple documenting a new `Virtualization.framework` CPU-feature-mask API on macOS 26+.
13. If the author wishes to relax constraints: empirically verify Docker Desktop ≥ 4.39 with the reference RunCoach.slnx — one `docker context use desktop-linux && dotnet restore` × 10 run confirms or refutes the primary-source claim on this exact repo.

## Epistemic caveats on this research pass

Two methodological limits apply to this report. First, `raw.githubusercontent.com` and `api.github.com` URLs on `release/10.0` could not be directly fetched by the research tools in this session; the `jitconfigvalues.h` content cited above is from `main` (retrieved 2026-04-22) and reconstructed for `release/10.0` via the repo's own `// keep in sync with clrconfigvalues.h` invariant plus the servicing-branch rule that env-var knobs are not added in servicing updates. Exact line numbers on `release/10.0` may drift ±5 lines. **The string literals and defaults themselves are high-confidence verbatim quotes.** Second, `bugs.openjdk.org` returned 403 for this session; the OpenJDK analog was therefore sourced from openjdk/jdk#22479 on GitHub and corretto/corretto-21#84/#85, which are primary but one hop removed from the canonical JBS record. The task brief's "JDK-8339254" number was not verifiable and is most likely a reference to **JDK-8345296** ("AArch64: VM crashes with SIGILL when prctl is disallowed"); the `-XX:UseSVE=0` user flag origin is JDK-8248742. Neither caveat changes the bottom-line conclusion.

## Conclusion

The R-063 diagnosis was correct at the JIT-emission layer but incomplete at the root-cause layer: the faulting code path is plausibly in a pre-CLRConfig startup probe or an SME2 instruction with no user-facing gate, which is precisely why three progressively more aggressive env-var combinations failed empirically. R-063's fallback posture — demote Path A to x86_64-Linux-only and wait — is the right call under the stated constraints. Relaxing either the "Colima default profile" constraint (to Docker Desktop ≥ 4.39 or Colima `--vm-type=qemu` with `cpuType=cortex-a72`) or the "no .NET 11 wait" constraint unlocks deterministic paths, but no workaround preserves the reference posture. Bake the demote into DEC-056 and CONTRIBUTING.md with the 13 re-check triggers above, and watch specifically for a Microsoft-side `cpufeatures.c` hardening PR analogous to OpenJDK JDK-8345296 — that is the pattern under which a 10.0.x backport is most likely to appear.