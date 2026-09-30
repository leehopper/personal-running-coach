# Agent routing (DEC-092, revised by DEC-093)

Work in this repo runs on two model families on purpose. Routing is by seat and
the orchestration kit owns it: the seat table in the maintainer's
`~/.claude/CLAUDE.md` is the routing of record, and the plugin skill
`orchestration-kit:codex-orchestration` holds the launch, budget, brief and
review mechanics (`~/personal/orchestration-kit-stable/bin/codex-fleet --seat
<seat>`, `~/personal/orchestration-kit-stable/bin/agent-budget --live --account
--brief`). Load that skill before the first dispatch. This file holds only what
differs here. The stage recipe and every template: `.claude/codex/README.md`.
Codex agents read `AGENTS.md`, not this file.

## Seats as applied here

- Conversation, adjudication, gates, commits, PRs: this session.
- Recon over the codebase for a slice (tracing, extraction, bounded file sets
  per lens), refute lenses: `recon_large` / `refute_lens` fleets with a
  schema, read-only, from the main tree, with no question to the user. One
  file of about 300 lines or fewer: `recon_small`. A design source against
  the code: `recall`. Recon that ends in a judgment over material the session
  would otherwise read inline (a branch's whole diff, a CI or suite log over
  about 300 lines, a gauntlet or CodeRabbit report, a research artifact):
  one `recon_large` task with a schema; the session reads the result.
- Spec or plan red-team, and a standing disagreement between two named
  reviewers on one document: the `red_team` seat, both families (a Claude
  Workflow lens plus the Codex fleet). Neither Codex verdict is accepted
  without the Claude side.
- First drafts that hold a structure over many facts (slice specs,
  cycle-plan sections, decision-log entries, handoffs, rules):
  `draft_structured`. Routine drafts with a fixed shape (PR bodies, round
  records, Captured rows): `draft_routine`. The session edits and decides.
- Build stage: `build_bounded` in a local clone outside the repo; about 20
  files or more, or a cross-module invariant: `build_refactor`.
- Per-stage review: two `review_lens` lenses (mutation ledger, spec
  conformance), both with write access, in detached worktrees, then fix
  rounds on `fix_mechanical` / `fix_mid` / `fix_hard` in the clone, repeated
  until zero blocker and zero major. Every lens report passes
  `.claude/codex/check-review.py` before adjudication. The cross-family pass
  on every PR is the maintainer's own code-gauntlet run in another session:
  never run it here; address its findings when asked.

Escalation, the cross-family rules, budget, session tier and handoff are the
kit's and the maintainer's global rules; do not restate them here.

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
- After a build or fix stage: the agent writes its Markdown report to
  `.codex-report.md` in the clone and returns a small JSON result (the
  launcher accepts only JSON); copy the report to `.stage-report.md`
  (untracked); `git status` the clone and confirm the
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
