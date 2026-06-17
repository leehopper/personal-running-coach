# Slice 4A Design: Coaching Voice Re-tune

> **Design + requirements — not an implementation plan.** Captures the "what" and the locked design decisions for the gruff-direct coaching-voice re-tune. The per-piece "how" (exact prompt prose, guard code, task breakdown) is written as a spec in a fresh session at build time. Parent: `docs/plans/mvp-0-cycle/cycle-plan.md`. Sibling slices: `slice-4-conversation.md` (4B), onboarding redesign + units (4C, not yet written).

## Origin: the Slice 4 decomposition (2026-06-16)

"Slice 4 (Open Conversation)" as carried in the cycle plan bundled four independent bodies of work. A `/catchup` brainstorm split it into three sub-slices, sequenced **voice-first**:

- **4A — Coaching-voice re-tune (this doc).** Cross-cutting LLM-output register change. Spec'd first because 4B adds a *new* open-conversation prompt; locking the voice first means that prompt is written gruff-direct from the start and the eval cache is re-recorded once, not twice.
- **4B — Conversation core.** The streaming interactive chat (R-082 / `batch-30a`) plus the three Slice-1 carry-forwards. Requirements live in `slice-4-conversation.md`.
- **4C — Onboarding redesign.** Form-first hybrid + slot-merge fix + km/miles unit-flexibility affordance. Most separable; may defer. Not yet written.

This doc covers **4A only**.

## Purpose

Re-tune every LLM-produced coaching voice from the current cheery/validating register to a **gruff, direct, no-nonsense** one. Warmth is shown through competence and straight talk, not enthusiasm, praise, or emotional validation. The voice spec is already **locked** (cycle plan § Captured During Cycle, 2026-06-13, builder Q&A) — 4A *executes* that lock; it does not re-decide the register.

**Scope note: this is LLM *output*, not the UI.** Distinct from (and to be planned alongside) the builder's pre-MVP visual UI refactor (ROADMAP § Deferred Items).

## What the voice change is (locked 2026-06-13)

- **Register = gruff and direct.** Blunt, short sentences. Warmth via straight talk + competence. No praise, no validation/feelings opener.
- **Keep non-negotiable, verbatim:** all safety / medical / crisis boundaries; the anti-toxic-culture clichés ("no days off", "pain is temporary", "push through"); the body / weight / shape / food-labeling guardrails (retained under the **safety** cluster — eating-disorder safety, *not* a warmth mandate).
- **Accountability allowed:** the builder did not require keeping "no guilt / no miss-counting", so pointed, factual accountability (naming a missed session as data) is permitted — as accountability, never as shaming.
- **MI scaffolding — keep the spine, drop the warmth mandates:** keep "always give a rationale", "offer a real choice when one exists", "show the forward path". **Delete** mandatory OARS *Affirmation*, process praise, and "acknowledge feelings first".
- **Output:** tighter. Cut filler and validation. **Keep the physiological "why"** — the rationale is product value.
- **Style rules:** ban em dashes; ban filler enthusiasm / exclamation / sycophancy. The prompts are themselves em-dash-heavy (the model mirrors them), so they are rewritten em-dash-free too.

## The three active prompts targeted

Verified against the codebase (2026-06-16):

| Prompt file | Loaded by | Drives |
|---|---|---|
| `coaching-system.v1.yaml` | `ActiveVersions` (`coaching-system: v1`) | **Both** plan-generation narrative **and** open-conversation responses |
| `adaptation.v1.yaml` | `ActiveVersions` (`adaptation: v1`) | Slice 3 Level-2 restructure rationale |
| `onboarding-v1.yaml` | loaded by filename (byte-stable Pattern-B; not in `ActiveVersions`) | Six-topic onboarding turns |

One edit to `coaching-system.v1` re-tunes plan-gen and conversation together.

**Not in 4A's critical path:** `coaching-v1.yaml`, `coaching-v2.yaml`, `coaching-system.v2.yaml` are legacy/draft files still listed in the DEC-074 hash manifest. (`"coaching-v1"` survives only as a historical event-metadata label string.) Deleting them is reasonable cleanup but is *not required* to re-tune the active prompts, and removing `coaching-system.v2.yaml` would break a `YamlPromptStore` test. Tracked as an **optional follow-up**, out of 4A scope.

## Design decisions

### D1 — Persona doc changes first

`docs/planning/coaching-persona.md` is the source of truth the prompts implement, so it changes before the prompts. Required edits:

- Flip Core Principle / Persona Calibration to directness-led; retire the 80/20 warmth-to-directness dial.
- Make OARS *Affirmation* non-mandatory in the Three-Layer Communication section.
- Drop **process praise** and **acknowledge-feelings-first** from "Always Do" rules.
- Add a **STYLE** section: no em dashes; no exclamation / filler enthusiasm / sycophancy; short sentences.
- **Keep verbatim:** all SAFETY rules, the "Never Do" toxic-culture clichés, the body/weight/shape/food-labeling lines, the crisis-response protocol, and the population-specific sensitive-disclosure guidelines.

### D2 — Per-prompt rewrite (gruff-direct, em-dash-free)

- **`coaching-system.v1`** — flip the 80/20 dial; delete "Use process praise" + "Acknowledge feelings before correcting behavior"; make OARS Affirmation non-mandatory; add the STYLE block. Keep the MI spine and every NEVER / SAFETY / VOCABULARY line verbatim.
- **`onboarding-v1`** — same dial flip + drop the acknowledge-feelings line + add the STYLE block (this is what kills "Love it!" / "Great foundation!"). Keep the topic schema, ambiguity rules, numerical bounds, Pattern-B invariant, data-handling directive, and safety + body/food lines verbatim. The byte-stable system block is simply re-frozen once; turn-2-onward prefix-cache hits resume as before.
- **`adaptation.v1`** — rewrite the RATIONALE shape: **drop step-1 "Validate what happened"**; new shape = *name the data pattern → state the change → the physiological why → the path back*. Flip the voice rule. **Leave the CURRENT-WEEK CONSISTENCY / GATE-BEFORE-INCREASE / programming guardrails (the Slice 3B F4 logic) untouched.**

Net effect: **kept** = MI spine + all safety; **deleted** = mandatory OARS affirmation, process praise, feelings-first opener; **newly allowed** = pointed factual accountability.

### D3 — Enforcement: deterministic guards hard-gate, Haiku judge advises

- **Deterministic prose guards (hard CI gate).** Clone the existing `TrademarkProseGuard` (Slice 3B F2) pattern into output-side guards over the cached eval fixtures:
  - **em-dash guard** — fails if `—` or `–` appears in any prose field of any fixture;
  - **exclamation guard** — fails on `!` in prose fields;
  - **banned-phrase guard** — fails on the toxic-culture clichés and a curated sycophancy list ("Love it", "Love that", "Great foundation", and similar openers).
  These are mechanical and reliable, so they block the build. They complement (do not replace) the existing trademark and safety guards, which must keep passing.
- **Haiku restraint rubric (advisory).** A new anti-sycophancy / directness rubric reusing the `SafetyRubricEvaluator` LLM-as-judge harness — scores directness, absence of validation/filler, and presence of rationale + forward path. **Decision: advisory (reported, not hard-gated) in the structural PR.** A fuzzy judge should not flake the build, and the builder's own eye is the real subjective gate during the tuning rounds. The rubric can be promoted to a threshold gate once its scores are calibrated against builder-approved output.

> **No runtime scrubber.** Unlike the F2 `TrademarkScrubber`, 4A does **not** add a runtime em-dash scrubber. The prompt STYLE rule plus the deterministic eval guard are the chosen enforcement. (Em-dash → comma/period rewriting is not always clean; revisit only if live tuning shows the model still emitting them despite the prompt rule.)

### D4 — Build/merge model: structural PR first, then tune

The voice is subjective and a single rewrite will not land it. The work is structured as:

1. **One structural PR** — the persona-doc edits, the three prompt rewrites, the deterministic prose guards, the advisory Haiku rubric, the regenerated DEC-074 hash manifest, and a **single** re-record of the affected fixtures. Reviewable and gateable: it has a clean definition of done independent of the subjective register landing perfectly.
2. **Tuning rounds** — prompt-only follow-up PRs. Each round: adjust the prompt prose → regenerate the manifest → targeted re-record of the affected fixtures → Replay-verify → builder reads the live output against a fresh account. Repeat until the gruff-direct register reads right to the builder.

### D5 — Eval re-record + manifest mechanics

Rewriting the three prompts busts `.prompt-hashes.sha256` and every fixture those prompts produced (onboarding eval, plan-generation eval, adaptation Level-2 restructure + the Haiku rationale judge for the `lee` and `priya` profiles). Each affected-fixture re-record:

- regenerates the hash manifest **before** the Record run (the existing script ordering);
- re-records only the affected fixtures with a funded key via the established targeted-re-record recipe (not a blanket whole-cache re-record);
- Replay-verifies the full backend suite green afterward.

This applies to both the structural PR (once) and every tuning round.

## Quality requirements / definition of done (structural PR)

- `coaching-persona.md` updated per D1.
- The three active prompts rewritten per D2: gruff-direct, em-dash-free, MI spine retained, all safety / body-food / anti-toxic lines verbatim.
- Deterministic em-dash / exclamation / banned-phrase prose guards added and **green**.
- Advisory Haiku restraint rubric added and reporting.
- DEC-074 hash manifest regenerated; affected fixtures re-recorded once.
- **Full backend suite green in Replay**, including the **existing trademark and safety evals** (the cheapest guard against over-trimming the safety content).

## Open items for the spec-writing session

- Whether the voice re-tune warrants its own decision-log entry (DEC-08x) recording the register change + the advisory-judge posture, or whether the 2026-06-13 cycle-plan lock is sufficient record. (Lean: a short DEC, since it changes a load-bearing product behavior and supersedes parts of DEC-027's tone mapping.)
- The exact curated sycophancy banned-phrase list for the deterministic guard (derive from the live-pass examples: "Love it", "Love that target", "Great foundation", plus the validation openers).
- The relationship to DEC-027 (proactive/conversational tone mapping) and DEC-030 (safety taxonomy) — confirm which tone clauses the re-tune supersedes versus leaves intact (the escalation-ladder ↔ communication-tone mapping likely needs a directness-led restatement).
- Whether the Haiku restraint rubric reuses the existing safety-judge cache plumbing or gets its own fixture set.

## How this feeds the spec

1. Read this doc + the cycle plan § Captured During Cycle (2026-06-13 voice lock) + `coaching-persona.md` + the three target prompts + the existing eval suite (`SafetyRubricEvaluator`, `TrademarkProseGuard`, `Dec074PromptHashSentinelTests`).
2. Write the spec for the structural PR (house format, working-tree-only under `docs/specs/`).
3. Builder reviews before implementation.
4. Implement the structural PR; then iterate the tuning rounds.

## Relationship to the cycle plan

The cycle plan's Status block and § Captured During Cycle (2026-06-13) carry the locked voice spec and the "several test → tune → re-record rounds" directive. This doc elaborates the design without crossing into implementation. If they conflict, the cycle plan wins — update this doc to match. On 4A completion, the cycle plan and ROADMAP Status blocks get updated and a Cycle History row added.
