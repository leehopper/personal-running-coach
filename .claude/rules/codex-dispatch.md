# Agent routing (DEC-092, revised by DEC-093)

Work in this repo runs on two model families on purpose: the Claude session
orchestrates and rules; bulk reads, builds, per-stage reviews, fix rounds,
and the session's big reads and structured drafts run on Codex, driven from
that session with the maintainer's user-level `codex-orchestration` skill
(routing table, model rows, `codex-fleet.py`, `agent-budget.py`, `cx.sh`).
Load that skill before the first dispatch. This file holds only what differs
here. The stage recipe and every template: `.claude/codex/README.md`. Codex
agents read `AGENTS.md`, not this file.

## Routing as applied here

The first matching row wins. Cost decides only when nothing above it does,
and cross-model costs compare in Codex window points, not raw tokens.

- Conversation, adjudication, gates, commits, PRs: this session (tier below).
- Spec or plan red-team, and a standing disagreement between two named
  reviewers on one document: one Claude opus
  lens (Workflow tool) plus one Astra `xhigh` lens (fleet driver, `--model
  gpt-6-astra --effort xhigh`); sol `max` is the fallback when Astra is
  unavailable. Neither Codex verdict is accepted without the Claude side.
- Recon over the codebase for a slice (tracing, extraction, bounded file
  sets per lens), refute lenses: luna `xhigh` fleets with a schema,
  read-only, from the main tree, at any batch size, with no question to the
  user. Cross-document recall (a design source against the code) goes to
  terra `high`; a refute item that needs reasoning depth escalates to Astra
  `xhigh` with a Claude check. Astra's 50x input cost is why not to default up.
- Recon that ends in a judgment over material the session would otherwise
  read inline (a branch's whole diff, a CI or suite log over about 300
  lines, a gauntlet or CodeRabbit report, a research artifact): one Astra
  `xhigh` task with a schema; the session reads the result. One file of
  about 300 lines or fewer goes to luna.
- First drafts that hold a structure over many facts (slice specs,
  cycle-plan sections, decision-log entries, handoffs, rules): one Astra
  `xhigh` task from a brief. Routine drafts with a fixed shape (PR bodies,
  round records, Captured rows): luna `xhigh`. The session edits and decides.
- Build stage: one luna `xhigh` companion task with write access in a local
  clone outside the repo. A refactor of about 20 files or more, or one that
  holds a cross-module invariant: Astra `xhigh` in the clone. A stage that
  fails review twice escalates to Astra `xhigh`.
- Per-stage review: two luna `xhigh` lenses (mutation ledger, spec
  conformance), both with write access, in detached worktrees, then a luna
  fix round in the clone, repeated until zero blocker and zero major. Every
  lens report passes `.claude/codex/check-review.py` before adjudication. The
  cross-family pass on every PR is the maintainer's own code-gauntlet run in
  another session: never run it here; address its findings when asked.

Invariants:

- Author and final refuter are never the same family (Astra, sol, terra,
  luna are one; Fable and opus the other). Never Fable for a subagent.
- Astra never adjudicates alone: every seat it holds keeps a Claude check
  (its chain of thought is less monitorable, it shows evaluation awareness,
  and it hallucinates on half of closed-book claims). Its findings are
  candidates, never verdicts.
- Codex agents work only over material in front of them; a missing fact
  comes back unverified, never filled in.
- `max` and `ultra` are never defaults on any model (`ultra` bills its
  subagents into the same window).
- Past about 60 percent of the Codex weekly window mid-week, Astra drops to
  `high` on non-critical builds and more work goes to luna.

## Session tier, shape and diet

- Tier. Opus 5 is the saved default and drives the mechanical sessions:
  catchup and close-out records, ROADMAP and Captured-row hygiene, CI rot
  and dependabot trains, ops wiring. A session that writes or red-teams a
  spec, adjudicates lenses or a gauntlet run, or orchestrates a slice's
  fleets switches to Fable with `/model` at its start, or in place when a
  design fork appears mid-session.
- Shape (a trial): hand off at 300 turns or 300k context, whichever comes
  first, and note the handoff's cost in the round record so the threshold
  can move.
- Diet: every inline read is a cache write. Never read inline a branch's
  whole diff, a suite or CI log, a fleet log or ledger, or a research
  artifact; route it through the recon rows above or read a bounded excerpt.
  Batch independent tool calls into one turn.
- Budget line at every catchup and before any fan-out:
  `python3 ~/.claude/skills/codex-orchestration/agent-budget.py --live --account`.
  One window point is about 14M luna, 1.5M terra, 0.7M sol, or 0.3M Astra
  tokens; size an Astra job from the ledger before launching it.

## Repo mechanics that differ from the skill

- Clone: `git clone --local <repo> ~/.claude/codex-jobs/clones/<branch>`,
  then in the clone `git checkout -b <branch>`, `npm ci` in `frontend/` (a
  real directory: the sandbox refuses writes through a symlink that points
  outside the workspace), `dotnet restore RunCoach.slnx` in `backend/`, and
  `lefthook install`. One writer per clone. The main tree stays on `main`.
- Sandbox facts (measured 2026-09-04, in `AGENTS.md`): writes only to the
  workspace and `/tmp`, no network, no Docker socket, `git` writes and every
  `rm -f` form refused, `dotnet build` only with the in-process flag bundle,
  `dotnet test` not at all, frontend build/test/lint fine.
- After a build or fix stage: copy the companion result into the clone as
  `.stage-report.md` (untracked); `git status` the clone and confirm the
  changes exist; stage the brief's explicit file list, never `git add -A`;
  commit in the clone so lefthook runs there. `dotnet test`, Playwright, the
  eval re-record, and anything needing the running stack run from this
  session, never inside Codex.
- Reviews: `git fetch <clone-path> <branch>:<branch>` into the main repo and
  compare the SHA with the clone's HEAD (a stale fetch once merged a
  truncated branch elsewhere), then
  `git worktree add --detach ~/.claude/codex-jobs/worktrees/<branch>-r<N>-<lens> <branch>`
  per lens and per round at a fresh path. In each worktree copy
  `frontend/node_modules` from the clone, run `dotnet restore`, and copy in
  `docs/specs/<slice>/` and `.stage-report.md` (both untracked). Remove with
  `git worktree remove --force` and `git worktree prune`; never recreate a
  worktree at a path you removed.
- Evidence (briefs, recon and red-team JSONs, lens JSONs, adjudications,
  fix lists, stage reports, orchestrator-run outputs) is committed under
  `docs/plans/<cycle>/<slice>-evidence/`, written in the clone and committed
  before the build so review worktrees carry it. The driver's logs and
  ledgers stay under the jobs directory. Public repo: grep the evidence for
  home paths and addresses before the push.
- Shipping: `git push -u origin <branch>` from the main repo after the
  fetch, then `gh pr create`. The user merges. A lens's `orchestrator_runs`
  (Docker-bound tests and the like) are run from this session with the
  output attached to the round record; until then the mutation they guard
  counts as unverified, never green.
- Companion-generated `.codex/` and `.agents/` mirrors, `.stage-report.md`,
  and `.tmp-*/` scratch paths are gitignored.
