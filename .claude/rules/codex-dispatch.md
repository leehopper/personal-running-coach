# Agent routing (DEC-092)

Work in this repo runs on two model families on purpose: the Claude session
orchestrates and rules; bulk reads, builds, per-stage reviews, and fix rounds
run on Codex, driven from that session with the maintainer's user-level
`codex-orchestration` skill (routing table, `codex-fleet.py`,
`agent-budget.py`, `cx.sh`). Load that skill before the first dispatch. This
file holds only what differs here. The stage recipe and every template:
`.claude/codex/README.md`. Codex agents read `AGENTS.md`, not this file.

## Routing as applied here

- Conversation, adjudication, gates, commits, PRs: this session.
- Recon over the codebase for a slice, refute lenses, extraction: luna
  `xhigh` fleets with a schema, read-only, from the main tree, at any batch
  size, with no question to the user.
- Build stage: one luna `xhigh` companion task with write access in a local
  clone outside the repo; sol `max` for refactors of about 20 files or more.
- Per-stage review: two luna `xhigh` lenses (mutation ledger, spec
  conformance), both with write access, in detached worktrees, then a luna
  fix round in the clone, repeated until zero blocker and zero major. The
  cross-family pass on every PR is the maintainer's own code-gauntlet run in
  another session: never run it here; address its findings when asked.
- Spec or plan red-team, and two reviewers who disagree: one Claude opus lens
  (Workflow tool) plus one sol `max` lens (fleet driver, `--model
  gpt-5.6-sol --effort max`). Sol's verdict is never accepted without the
  Claude side.
- Session diet: first drafts of PR bodies, decision-log entries, cycle-plan
  sections, and big recon reads come from a luna task with a bounded output.
  The session edits and decides.
- Budget line at every catchup and before any fan-out:
  `python3 ~/.claude/skills/codex-orchestration/agent-budget.py --live --account`.

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
