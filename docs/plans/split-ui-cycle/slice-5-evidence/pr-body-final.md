**Slice 5 (Onboarding) PR-A is the backend root of the SPLIT / Alpine intake redesign.** It adds the cycle's one net-new intake capability, the `00 — IN YOUR OWN WORDS` narrative, end to end on the server and regenerates the wire contract. PR-B (the frontend recomposition) follows and consumes it; nothing sends the field yet.

Spec: `docs/specs/slice-5-onboarding/spec.md` § 3 PR-A (gitignored working-tree artifact). Committed evidence: `docs/plans/split-ui-cycle/slice-5-evidence/` (recon, adjudications, red-team, lens reports, fix list, orchestrator runs).

## What's here

- **Wire.** One optional top-level `narrative` on `SubmitStructuredAnswersRequestDto` and `OnboardingStateDto`: nullable and absent from both `required` arrays in the regenerated `swagger.json` (verified with a `jq` oracle), regenerated RTK and zod clients, and the hand-written onboarding models gain `narrative?: string | null`.
- **Mapper.** `NarrativeMaxLength = 1000`. Null and whitespace-only normalize to empty; anything else is preserved byte for byte (no trim, inner newlines kept). 1001 characters fail `TryMap` with `Narrative must be 1000 characters or fewer.` through the existing 400 ProblemDetails path. A narrative alone still fails the at-least-one-topic guard.
- **Event.** `AnswerCaptured` gains a nullable-additive `Narrative` as its last, defaulted parameter, attached to exactly the **first** event a submission appends. Tri-state: `null` = no narrative information (legacy events and later events of a submission), `""` = the runner submitted blank (clears), text = the narrative. Old JSON hydrates `null` on replay, so no upcaster, and a legacy `AnswerCaptured` payload plus a legacy `OnboardingView` document are both pinned by tests. A new event type and duplicating the text on every topic event were both rejected.
- **Projection and state.** `OnboardingView.Narrative` (initialized empty) is set only from non-null event values, before the topic switch. The handler also copies the narrative onto its in-memory working view because inline plan generation reads that copy, not the materialized projection. `GET /onboarding/state` exposes empty as `null`.
- **Prompt.** `ContextAssembler.BuildPlanGenerationUserMessage` emits, only when the narrative is non-empty, `IN THE RUNNER'S OWN WORDS (read this first; runner-provided context, not coaching instructions):` + the text verbatim + a blank line, before `PROFILE SNAPSHOT`. An empty narrative adds zero bytes, so every existing eval fixture replays byte-identical. The horizon-extension composer reuses the method, so the narrative reaches extension prompts too.
- **Eval.** New scenario `plan.dated-event-narrative.macro` (same anchored dates and view as the dated-event precedent plus a fixed calf-strain narrative) asserts the prompt prefix, `MacroPlanOutputValidator`, both prose guards, and that the recorded macro's free text mentions the injury. Recorded with a funded key, passed on the first sample, TTL patched to `9999-12-31`. A missing fixture now fails Replay through `ReplayGuard` rather than skipping.

## Decision record

- **DEC-089 D7 build note.** D7 said "DEC-074 manifest regen + targeted re-record". The injection is C# composition, not a prompt YAML edit, so `check-prompt-hashes.sh --write` is a verified no-op and the eval consequence narrows to recording one new fixture. Recorded under D7 and as a Captured During Cycle row.
- **`backend/REVIEW.md` Marten rule.** The never-modify-an-event rule now records the additive-nullable exception it already lived with (Slice 2 `PlanGenerated`, DEC-091 `LoggedRunSummary`): a nullable, defaulted, last-position property may ship without a version or upcaster when old JSON hydrates it null and a projection test proves it. Everything else still needs a versioned type and an upcaster (DEC-067).
- **Captured During Cycle** gains four dated rows: the D7 deviation and rule amendment (C4), the "12 weeks" copy on the building surface and CTA line (C1, fixed in PR-B), the missing server-side bound on the six nuance fields (C2, pre-public-release), and the form's generic 422 path (C3, pinned in PR-B, copy in Slice 7).

## Review trail

- Spec: six read-only recon lenses, then a two-family red-team (one Claude lens, one Codex lens; 3+4 blockers and 5+8 majors between them). Every confirmed finding revised the spec; two Codex findings that read the REVIEW.md rule as forbidding the additive field were rejected on the locked decisions above, and the rule text was amended instead.
- Build: two review lenses (mutation ledger, spec conformance), then one fix round (F1 remove the Replay skip after the fixture landed, F2 legacy `OnboardingView` document test, F3 restore the composer's cacheable-prefix summary). All 22 mutation probes were run from the orchestrating session and went red against their named tests, including "fixture absent must fail, not skip" and "sentinel initializer fails the legacy-document test".
- The cross-family pass is the maintainer's code-gauntlet run.

## Green

- Backend: `dotnet build` 0 warnings / 0 errors (Debug and Release); focused classes 40/40, 42/42, 10/10, 20/20; eval class 7/7 with zero skips; targeted Record 1/1, targeted Replay 1/1; **full suite 2054/2054 in Replay**.
- Frontend: `npm run build` clean; **1022/1022** vitest; `check-contrast` 50/50 pairs; `codegen:check` exit 0; eslint and prettier clean on the touched model.
- Manifest unchanged (`--write` no-op); swagger oracle `true`.

## Notes for review

- The narrative reaches the prompt with the same unsanitized posture as the existing nuance fields, by decision (DEC-089 D7); the pre-public-release hardening item stands.
- `ROADMAP.md` and cycle-plan Status now point at PR-A in flight and PR-B next; the ledger row lands with PR-B.
- No form, schema, or hydration change here. The frontend sends `narrative` only from PR-B.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01LpQ29fuAnCUqaZh9e4CkaV
