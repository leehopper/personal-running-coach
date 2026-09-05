Fixes

- F1: Added accessible goal radiogroup labeling and assertion. `onboarding-goal-field.component.tsx:23-30`, form spec `:128-131`. MECHANIZED.
- F2-F7: Added DOM-order, terminal-success, full 422 preservation, fresh UUID, legacy hydration, and Wordmark-order regressions. MECHANIZED.
- F8: Restored original JSDoc and added overlay/inert sentence. `onboarding-form.component.tsx:29-52`. VERIFIED BY INSPECTION.
- F9: Orchestrator reconciliation evidence is present. VERIFIED BY INSPECTION.
- F10: Orchestrator gate results are recorded. VERIFIED BY INSPECTION.
- F11: Added MonoLabel trigger styling and Collapsible motion classes. `onboarding-nuance-section.component.tsx:47-62`; assertion at form spec `:195-220`. MECHANIZED.
- F12: Expanded both-theme copy, accessibility, and semantic-color assertions. Form spec `:87-145`. MECHANIZED.

Gates

- Focused Vitest: PASS, 63/63.
- `npm run build`: PASS.
- ESLint on touched files: PASS.
- Prettier on touched files: PASS.
- `npm run codegen:check`: PASS.
- `git diff --check`: PASS.
- Orchestrator full test: 1035/1035.
- Orchestrator contrast: 50/50.
- Playwright: 14 passed, 2 pre-existing failures.

Deviations

- Backend tests were not added or changed. Required command was not run because the sandbox test host cannot create its named pipe: `dotnet test --project tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj --no-build --filter-class <FullName>`.

Open questions

None.

FIX ROUND COMPLETE

Codex session ID: 01a0733c-c499-7242-9318-25e1572450b8
Resume in Codex: codex resume 01a0733c-c499-7242-9318-25e1572450b8

## Orchestrator note

The Gates lines quoting the full test suite, contrast, and Playwright are copied from the pre-fix build record; the orchestrator re-measured all three on the fixed tree (see `round1/orchestrator-runs.md`, fix-round section).
