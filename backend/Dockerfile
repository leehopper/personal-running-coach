# SDK + runtime pinned by digest (SDK 10.0.203 / runtime 10.0.7, 2026-04-21
# — OOB CVE-2026-40372 DataProtection HMAC fix). Bumping either digest
# requires a manual verification pass on an x86_64 runner (this is a CI-only
# build path on Apple Silicon today; see DEC-056).
#
# CONTAINERIZED BUILD IS BROKEN ON APPLE SILICON (M3/M4/M5) per
# dotnet/runtime#122608 — CoreCLR JIT emits non-streaming SVE2 opcodes that
# macOS Virtualization.framework does not execute, producing deterministic
# SIGILL during `dotnet restore` for any solution with a non-trivial package
# graph. Microsoft milestoned the fix to .NET 11; there is no 10.0.x
# servicing backport. Empirically, neither the narrow ISA knobs
# (`DOTNET_EnableArm64Sve`, `_Sve2`, `_Sme`, `_Sme2`) nor the master switch
# (`DOTNET_EnableHWIntrinsic=0`) cleared the fault deterministically under
# Colima 0.10.1 + Docker Desktop-less containerd on an M4 Pro host — all
# failed 5/5. `--platform=linux/amd64` (Rosetta) also failed (SIGABRT; this
# Colima profile lacks a working binfmt handler for cross-arch emulation).
#
# Durable posture: this Dockerfile is the CI image-build path for x86_64
# GitHub Actions runners, which are unaffected by #122608. Apple-Silicon
# contributors use Path B (host-run API + Compose for infra only) per
# CONTRIBUTING.md and DEC-045. Remove this comment and retry Path A on the
# first .NET 11 SDK bump.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:8a90a473da5205a16979de99d2fc20975e922c68304f5c79d564e666dc3982fc AS build

# Defensive belt-and-suspenders against #122608-class regressions if this
# Dockerfile is ever accidentally built on arm64. Exhaustive ARM64 SVE/SVE2
# codegen gates per `src/coreclr/jit/jitconfigvalues.h` (release/10.0), names
# verified verbatim by R-064. Ignored on x86_64 CoreCLR (the supported CI
# platform) so GitHub Actions ubuntu-latest is a no-op. Does NOT clear the
# SIGILL on Apple Silicon by itself (the fault is in a pre-CLRConfig probe
# and/or R2R'd BCL that env vars can't reach — see DEC-056 and R-064), but
# reduces JIT-layer SVE2 emission surface and documents the known-ISA
# hazard inline so a future contributor finds the context here.
ENV DOTNET_EnableArm64Sve=0 \
    DOTNET_EnableArm64Sve2=0 \
    DOTNET_EnableArm64SveAes=0 \
    DOTNET_EnableArm64SveSha3=0 \
    DOTNET_EnableArm64SveSm4=0 \
    DOTNET_EnableEmbeddedMasking=0

WORKDIR /src

COPY Directory.Build.props Directory.Packages.props RunCoach.slnx ./
COPY src/RunCoach.Api/RunCoach.Api.csproj src/RunCoach.Api/
COPY tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj tests/RunCoach.Api.Tests/
RUN dotnet restore RunCoach.slnx

COPY . .
RUN dotnet publish src/RunCoach.Api/RunCoach.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:55e37c7795bfaf6b9cc5d77c155811d9569f529d86e20647704bc1d7dd9741d4 AS runtime

WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
HEALTHCHECK --interval=10s --timeout=5s --retries=3 CMD curl -f http://localhost:5000/health || exit 1
USER $APP_UID
ENTRYPOINT ["dotnet", "RunCoach.Api.dll"]
