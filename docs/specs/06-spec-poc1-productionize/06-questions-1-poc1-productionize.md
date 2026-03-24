# Questions Round 1 — POC 1 Productionize

## Q1: Experiment prompt YAMLs (context-injection-8k, -12k, -profile-end, -profile-middle)
**Answer:** Delete from branch. Findings are preserved in poc1-findings.md on the POC branch, which stays tagged as historical reference.

## Q2: TestProfiles relocation
**Answer:** Mirror src path in tests/ — move to `tests/RunCoach.Api.Tests/Modules/Training/Profiles/`. Follows existing convention (tests mirror src), and TestProfilesTests.cs already lives there. Shared test infrastructure project is premature since frontend uses TypeScript fixtures (different language boundary) and no additional .NET test projects exist yet.

## Q3: docs/specs/ POC artifacts (01 through 05)
**Answer:** Keep on POC branch only. These are POC process artifacts, not production docs. Tag the branch and they're preserved as history.

## Q4: Branch strategy
**Answer:** Clean up on the current branch (feature/poc1-context-injection-v2), then merge to main.
