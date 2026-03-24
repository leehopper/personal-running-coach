# 06-spec-poc1-productionize

## Introduction/Overview

Strip POC scaffolding from the `feature/poc1-context-injection-v2` branch so that only production-quality code merges to `main`. POC 1 validated that context injection produces good coaching plans (290 tests, 5 review rounds). The automated eval suite now fully supersedes the interactive console app. This spec removes POC-only artifacts, relocates test fixtures to the test project, and cleans up project references so the branch is merge-ready.

## Goals

1. Remove all POC-only artifacts (console app, experiment prompt YAMLs, POC spec/proof directories) from the branch
2. Relocate `TestProfiles` from production source to the test project
3. Clean up project references (`InternalsVisibleTo`, solution file) so no orphaned references remain
4. Ensure `dotnet build` and `dotnet test` pass with 0 warnings after all changes
5. Tag the pre-cleanup commit as `poc1-complete` for historical reference before any removals

## User Stories

- As a developer, I want the `main` branch to contain only production-quality code so that I can build MVP-0 features on a clean foundation.
- As a developer, I want test fixtures in the test project (not production source) so that production assemblies don't ship test data.
- As a developer, I want the POC branch preserved as a tagged reference so I can revisit original experiments and findings if needed.

## Demoable Units of Work

### Unit 1: Tag and remove POC console app

**Purpose:** Preserve POC history, then remove the console app project and all its references.

**Functional Requirements:**
- The system shall create a git tag `poc1-complete` on the current HEAD before any deletions
- The system shall delete the entire `backend/src/RunCoach.Poc1.Console/` directory (4 files: `Program.cs`, `AssemblyMarker.cs`, `RunCoach.Poc1.Console.csproj`, `appsettings.json`)
- The system shall remove the `RunCoach.Poc1.Console` project entry from `backend/RunCoach.slnx`
- The system shall remove the `<InternalsVisibleTo Include="RunCoach.Poc1.Console" />` line from `backend/src/RunCoach.Api/RunCoach.Api.csproj`
- The system shall verify that `dotnet build` succeeds with 0 errors and 0 warnings after these changes

**Proof Artifacts:**
- CLI: `git tag -l poc1-complete` returns `poc1-complete` — demonstrates tag was created
- CLI: `ls backend/src/RunCoach.Poc1.Console/` returns "No such file or directory" — demonstrates deletion
- CLI: `dotnet build backend/RunCoach.slnx` returns success with 0 warnings — demonstrates clean build

### Unit 2: Relocate TestProfiles to test project

**Purpose:** Move hardcoded test fixtures from production source to the test project where they belong.

**Functional Requirements:**
- The system shall move `backend/src/RunCoach.Api/Modules/Training/Profiles/TestProfiles.cs` to `backend/tests/RunCoach.Api.Tests/Modules/Training/Profiles/TestProfiles.cs`
- The system shall move `backend/src/RunCoach.Api/Modules/Training/Profiles/TestProfile.cs` to `backend/tests/RunCoach.Api.Tests/Modules/Training/Profiles/TestProfile.cs`
- The system shall update the namespace in both moved files from `RunCoach.Api.Modules.Training.Profiles` to `RunCoach.Api.Tests.Modules.Training.Profiles`
- The system shall update all `using` statements in consuming test files (4 files: `EvalTestBase.cs`, `ContextAssemblerTests.cs`, `EvalTestBaseTests.cs`, `TestProfilesTests.cs`) to reference the new namespace
- The system shall verify no source files under `backend/src/` reference the `TestProfiles` or `TestProfile` types
- The system shall verify that `dotnet build` and `dotnet test` both pass with 0 errors and 0 warnings

**Proof Artifacts:**
- File: `backend/tests/RunCoach.Api.Tests/Modules/Training/Profiles/TestProfiles.cs` exists with namespace `RunCoach.Api.Tests.Modules.Training.Profiles`
- CLI: `grep -r "TestProfiles\|TestProfile" backend/src/` returns no matches — demonstrates no production references remain
- CLI: `dotnet test backend/RunCoach.slnx` passes all tests — demonstrates no regressions

### Unit 3: Remove POC experiment artifacts

**Purpose:** Remove experiment prompt YAMLs and POC spec/proof directories that should not merge to main.

**Functional Requirements:**
- The system shall delete the following experiment prompt YAML files from `backend/src/RunCoach.Api/Prompts/`:
  - `context-injection-8k.yaml`
  - `context-injection-12k.yaml`
  - `context-injection-profile-end.yaml`
  - `context-injection-profile-middle.yaml`
- The system shall delete the entire `docs/specs/` directory (contains specs 01-05 with POC validation artifacts — all preserved on the tagged `poc1-complete` reference)
- The system shall verify that remaining prompt YAML files (`coaching-system.v1.yaml`, `coaching-system.v2.yaml`, `coaching-v1.yaml`, `coaching-v2.yaml`, `context-injection-v1.yaml`) are still present and unchanged
- The system shall verify that `dotnet build` succeeds after prompt file removals (no broken file references)

**Proof Artifacts:**
- CLI: `ls backend/src/RunCoach.Api/Prompts/` shows exactly 5 production prompt files
- CLI: `ls docs/specs/` returns "No such file or directory" — demonstrates POC specs removed
- CLI: `dotnet build backend/RunCoach.slnx` returns success — demonstrates no broken references

### Unit 4: Final verification and ROADMAP update

**Purpose:** Verify the complete cleanup, update documentation, and confirm merge-readiness.

**Functional Requirements:**
- The system shall run the full test suite (`dotnet test`) and confirm all tests pass with 0 failures and 0 warnings
- The system shall verify no files reference `RunCoach.Poc1.Console` anywhere in the repository
- The system shall verify no source files under `backend/src/` contain `TestProfiles` or `TestProfile` references
- The system shall update `ROADMAP.md` to reflect that POC 1 has been productionized and merged (not "ready for review")
- The system shall update `README.md` to remove any POC-specific run instructions (e.g., console app usage) that no longer apply
- The system shall create a single commit with all cleanup changes using conventional commit format

**Proof Artifacts:**
- CLI: `dotnet test backend/RunCoach.slnx` passes all tests with 0 failures
- CLI: `grep -r "Poc1.Console" backend/` returns no matches — demonstrates complete removal
- File: `ROADMAP.md` reflects productionized/merged status

## Non-Goals (Out of Scope)

- **DEC-040 (Daniels pace table fix):** Separate post-merge PR on a new branch
- **DEC-041 (unit system value objects):** Separate post-merge PR per existing design doc
- **TimeProvider injection for ContextAssembler:** Known cleanup debt, tracked in ROADMAP deferred items
- **EvalTestBase relative path fragility:** Known cleanup debt, tracked in ROADMAP deferred items
- **Any new features or refactoring:** This is strictly removal and relocation of existing code
- **Frontend changes:** No frontend code is affected by POC 1 cleanup

## Design Considerations

No specific design requirements identified. This is a structural cleanup with no UI or API surface changes.

## Repository Standards

- **Commit messages:** Conventional Commits — `refactor:` for relocations, `chore:` for deletions
- **Namespace convention:** Test files use `RunCoach.Api.Tests.Modules.{Domain}.{Subdomain}` (mirrors src with `.Tests` prefix)
- **Build verification:** `dotnet build` must pass with 0 warnings (TreatWarningsAsErrors is enabled in Directory.Build.props)
- **Test execution:** `dotnet test` must pass all tests including eval tests in Replay mode

## Technical Considerations

- **TestProfiles relocation changes the namespace.** All 4 consuming test files need their `using` statements updated. No production code references these types (confirmed — only the console app did, which is being deleted).
- **Eval cache fixtures reference prompt content by hash.** Removing experiment prompt YAMLs does NOT affect eval cache keys because the eval tests use `context-injection-v1.yaml` (which is kept), not the experiment variations.
- **`docs/specs/` deletion removes the spec for THIS task.** The spec should be committed before the deletion step in Unit 3, or Unit 3 should skip deleting `06-spec-poc1-productionize/` if it exists at that point. Pragmatically: delete specs 01-05 only, keep 06 if present.
- **The `poc1-complete` tag must be created BEFORE any deletions** so the full POC history is recoverable.

## Security Considerations

- Verify no secrets were introduced in any of the files being modified
- The console app's `appsettings.json` has already been audited (password was removed in PR #18 review round 2)
- No auth, API key, or credential changes involved

## Success Metrics

- `dotnet build` passes with 0 warnings
- `dotnet test` passes all tests (expected: ~290 tests, 0 failures)
- Zero references to `RunCoach.Poc1.Console` remain in the repository
- Zero references to `TestProfiles` remain under `backend/src/`
- The `poc1-complete` git tag exists and points to the pre-cleanup commit
- Branch is clean and ready for merge to `main`

## Open Questions

No open questions at this time. All decisions were resolved in the clarifying questions round.
