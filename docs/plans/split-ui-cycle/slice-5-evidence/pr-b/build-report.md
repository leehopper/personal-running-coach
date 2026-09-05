## Files

New:

- `frontend/src/app/modules/onboarding/components/onboarding-narrative-field.component.tsx`
- `frontend/src/app/modules/onboarding/components/onboarding-fine-print-section.component.tsx`
- `frontend/src/app/modules/onboarding/components/onboarding-switch-field.component.tsx`

Changed: all 19 paths listed in the task, including onboarding components, schemas, specs, E2E, page surface, and surface spec.

Deleted:

- `frontend/src/app/modules/onboarding/components/onboarding-checkbox-field.component.tsx`
- `frontend/src/app/modules/onboarding/components/onboarding-injury-section.component.tsx`
- `frontend/src/app/modules/onboarding/components/onboarding-preferences-section.component.tsx`

Deleted files are recoverable from git history and scratch backups.

## Gates

- `npx vitest run <four touched spec files>`: PASS. Last line: `Duration 2.60s ...`
- `npm run build`: PASS. Last line: `Adjust chunk size limit for this warning via build.chunkSizeWarningLimit.`
- `npx eslint <all touched TypeScript files>`: PASS. Last output: no output, exit code 0.
- `npx prettier --check <all touched files>`: PASS. Last line: `All matched files use Prettier code style!`
- `npm run codegen:check`: PASS. Generated files unchanged.
- `dotnet build RunCoach.slnx --no-restore ...`: PASS. Last line: `Time Elapsed: 00:00:08.08`
- `git diff --check`: PASS. Last output: no output, exit code 0.
- `npm run check-contrast`: DEVIATION. IPC pipe creation failed with `EPERM`.
- `npm run e2e`: DEVIATION. Web server could not bind to port 5173.

## Acceptance table

| Criterion | Result | Evidence |
|---|---|---|
| Units 1-5, PR-A narrative backend contracts | DEVIATION | PR-A remains on HEAD. Backend tests were not rerun because no backend files were in scope and the sandbox test host is unavailable. |
| Unit 6, generated nullable shape | PASS | `npm run codegen:check`; generated output unchanged. |
| Unit 7, Alpine intake and dual themes | PASS | `onboarding-form.component.spec.tsx:87`, focused Vitest run passed. |
| Unit 8, conditional sections and detail placement | PASS | `onboarding-form.component.spec.tsx:135`, focused Vitest run passed. |
| Unit 9, chips, switches, units, and km-native mapping | PASS | `onboarding-day-toggle-field.component.tsx:33`, `onboarding-form.component.spec.tsx:173`, focused Vitest run passed. |
| Unit 10, building lifecycle | PASS | `onboarding-form.component.tsx:118`, `building-plan-surface.component.tsx:37`, focused Vitest run passed. |
| Unit 11, 422 values and same key | PASS | `onboarding-form.component.spec.tsx:206`, focused Vitest run passed. |
| Unit 12, resume and reseed | PASS | `onboarding-form.schema.ts:417`, `onboarding.page.spec.tsx:162`, focused Vitest run passed. |
| Section 8 non-negotiables | PASS by inspection | `onboarding-form.component.tsx:118`, `onboarding-day-toggle-field.component.tsx:43`, static scans found no raw colors, restricted term, or non-ASCII text. |
| File and generated scope | PASS | `git status --short` contains only authorized paths plus pre-existing evidence files. |

## Mutations

New tests were first run against the old implementation and produced the expected red baseline: 3 files failed, 13 tests failed, 42 passed.

Added or realigned tests cover:

- Alpine layout, section order, narrative copy, and both themes at `onboarding-form.component.spec.tsx:87`.
- Narrative bounds and normalization at `onboarding-form.schema.spec.ts:160`.
- Narrative mapping and hydration at `onboarding-form.schema.spec.ts:274` and `:391`.
- Race and injury reveals at `onboarding-form.component.spec.tsx:135`.
- Detail trigger placement at `onboarding-form.component.spec.tsx:150`.
- Day chip and switch semantics at `onboarding-form.component.spec.tsx:173`.
- Fixed building overlay and form inertness at `onboarding-form.component.spec.tsx:192`.
- Same-key 422 retry at `onboarding-form.component.spec.tsx:206`.
- Page identity and narrative resume at `onboarding.page.spec.tsx:150`.
- Exact four-field reseed protection at `onboarding-form.schema.spec.ts:284`.
- Building status copy at `building-plan-surface.component.spec.tsx:17`.
- E2E narrative request payload at `frontend/e2e/onboarding.spec.ts:132`.

## Deviations

- `npm run check-contrast` must be rerun by the orchestrator.
- `npm run e2e` must be rerun by the orchestrator with the application stack available.
- `npm run test` was not run because the sandbox `tsx` runner cannot create its IPC pipe. The orchestrator should run `npm run test`.
- `dotnet format RunCoach.slnx --no-restore --verify-no-changes` did not return within the sandbox timeout and produced no output. No changed C# files exist, so the scoped format command has no applicable file.
- No backend tests were added or changed in this stage. PR-A backend verification remains orchestrator-owned.

## Open questions

None.

STAGE COMPLETE

Codex session ID: 01a072f4-1cc7-75b0-abc4-2082c25bc2ac
Resume in Codex: codex resume 01a072f4-1cc7-75b0-abc4-2082c25bc2ac
