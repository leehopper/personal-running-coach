**Slice 4 (Log & Log Book) PR-A — the backend root.** Ships the two pre-decided DEC-089 D5 wire deltas plus the slice's single codegen regen. Nothing consumes them yet; PR-B (`/log` banner) and PR-C (`/history` ledger) do.

Spec: `docs/specs/slice-4-log-logbook/spec.md` §3 PR-A (gitignored working-tree artifact).

## What's here

- **`GET /api/v1/workouts/logs/prescribed?date=` (D1)** — a thin, read-only wrapper over the *existing* `IWorkoutLogService.ResolveCandidatePrescriptionAsync`. No new resolution logic. Returns 200 + the prescription, or **200 + a literal `null` body** when the date resolves to none (off-plan / rest day / no active plan / malformed stored prescription / in-range week absent from `MicroWorkoutsByWeek`). Absence is an expected data state here, so this follows the `/conversation/turns` 200-on-absence precedent rather than `PlanRenderingController`'s 404, which is reserved for a genuinely missing primary resource. No antiforgery — it's a read, mirroring `POST /query` (DEC-055).
- **`PrescribedWorkoutDto` (Training module)** — a *deliberate* one-record sibling of Coaching's `CandidatePrescriptionDto`, not an accidental duplication. Module boundary favoured over cross-module reuse; the XML doc records why.
- **`WorkoutLogDto` gains `bool IsOnPlan` + `string? PrescribedWorkoutType` (D4)** — computed in `MapToDto` from the already-loaded `Prescription` complex property (table-split onto `WorkoutLog`'s own columns, always materialised, no `.Include()` needed). **Zero extra I/O.** Create path untouched — the create response never returns a `WorkoutLogDto`. Old/off-plan rows degrade to `false`/`null`.
- **Codegen** — `swagger.json` + RTK/zod clients regenerated, `PrescribedWorkoutDto` added to the hand-maintained `generated/index.ts` barrel (codegen doesn't touch that file; `tsc` is the gate, not `codegen:check`), and a hand-written `useGetPrescribedWorkoutQuery` wrapper in `workout-log.api.ts` following the `plan.api.ts`/`conversation.api.ts` precedent. `providesTags: ['Plan']` — the prescribed slot is plan-derived, and both `createWorkoutLog` and `regeneratePlan` already invalidate `Plan`.

## Two defects caught pre-merge

Neither was visible from reading the diff; both were caught by a failing test and by adversarial review.

1. **`Ok(null)` does not return 200 + null.** `ObjectResult`'s content-negotiation path selects the framework-registered `HttpNoContentOutputFormatter` for a null `Value` and silently rewrites the response to **204 No Content** — contradicting both the documented contract and the action's own `[ProducesResponseType(200)]`. The five null-branch integration tests failed on this. Fixed by returning `JsonResult`, which serializes `Value` directly and never enters formatter selection. A comment pins the reasoning so it doesn't get "simplified" back.
2. **An omitted `date` bound silently to `DateOnly.MinValue`.** `SimpleTypeModelBinder` treats an absent key as no-value and never touches `ModelState`, so `date` defaulted to `0001-01-01` → before any plan start → **200 + null**, i.e. a client bug masquerading as an ordinary off-plan day. Meanwhile a *malformed* `date` did 400, but that 400 was undeclared, so the generated clients promised a contract the API didn't honour. `date` is now `[BindRequired]` with the 400 declared as `ValidationProblemDetails` (matching `AuthController.Register`'s precedent for automatic-validation 400s, rather than this file's hand-thrown `Problem(...)` convention). Missing, empty and malformed now all fail honestly — three genuinely distinct ModelState branches, each pinned by a test. The generated arg tightens from `{ date?: string }` to `{ date: string }`.

## Decision record

`DEC-089` gains a one-line note ratifying the **D5(b) title source**: the field carries the **frozen `WorkoutType` enum name**, rendered client-side via the existing `WORKOUT_TYPE_LABELS`. Not the LLM-authored `WorkoutOutput.Title` — `RestructureDiffCalculator` diffs on `Title`, so live re-resolution would let a later adaptation restructure **retroactively rewrite the title on an already-logged ledger row**. `WorkoutPrescriptionSnapshot` has no title at all, and adding one would mean a migration for fidelity the design doesn't need. Point-in-time correct, zero migration, symmetric with the SSE card. This is a source clarification *within* the already-blessed D5(b), not a new wire delta — a full new DEC isn't warranted.

`PrescribedWorkoutType` is a plain `string?` **on purpose**: a nullable enum serializes as a `$ref` and trips the known `RequireNonNullablePropertiesSchemaFilter` bug (#163) that wrongly marks `$ref` props required. Verified in the regenerated swagger: `prescribedWorkoutType` is `nullable: true` and absent from `required`; `isOnPlan` is in `required`.

## Green

- Backend: `dotnet build` clean (0 warnings, TreatWarningsAsErrors), **2006/2006** tests.
- Frontend: **939/939** vitest, `npm run build` clean, `codegen:check` drift gate clean, eslint clean.
- **No LLM/prompt change → no eval re-record.**

Test coverage is net-new where it was absent: `MapToDto`'s field mapping was previously untested entirely (the existing service unit tests stub an empty repository list). The wire test asserts the raw camelCase JSON property names, so a serialization regression can't slip through a deserialize-only assertion.

## Notes for review

- The `ROADMAP.md` / `cycle-plan.md` edits are the previous session's "spec written" status, carried here rather than left dangling. Advancing the pointer to PR-B rides the usual post-merge docs PR.
- The rest-day and missing-micro-week tests deliberately exercise **different** null branches (day-lookup miss vs. week-lookup miss). The latter is a real production state today, not a hypothetical — per DEC-090, only week 1 of any plan currently gets micro workouts.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_011CFoccqV4SqYLLUAHjWvhc


<!-- This is an auto-generated comment: release notes by coderabbit.ai -->

## Summary by CodeRabbit

* **New Features**
  * Added support for retrieving the prescribed workout for a selected date.
  * Workout history now indicates whether an activity was part of the plan and displays its prescribed workout type when available.
  * Prescribed workout details include workout type, distance, duration, and pace ranges.

* **Documentation**
  * Updated Slice 4 roadmap, implementation planning, and design decision documentation.

* **Tests**
  * Added coverage for prescribed workout retrieval, validation, authentication, null results, and workout history mapping.

<!-- end of auto-generated comment: release notes by coderabbit.ai -->
