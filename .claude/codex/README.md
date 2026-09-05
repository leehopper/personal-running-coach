# Codex stage recipe

The templates in this directory and the step-by-step for running a slice on
the two-family routing in `.claude/rules/codex-dispatch.md`. Placeholders are
`{name}`; render a template with
`python3 .claude/codex/render.py <template> <vars.json> > <prompt>` (the same
`str.format` the fleet driver applies to `--template`; a literal brace is
doubled). Codex agents read `AGENTS.md` for the sandbox facts and house rules;
the brief carries only the per-task contract.

Paths used below:

- `SKILL=~/.claude/skills/codex-orchestration` (the maintainer's user-level
  skill: driver `codex-fleet.py`, budget `agent-budget.py`, companion helper
  `cx.sh`)
- `JOBS=~/.claude/codex-jobs` (fleet state under `fleets/<name>/`, companion
  jobs under `jobs/`, clones under `clones/`, worktrees under `worktrees/`)
- `R=$PWD` (this repo's main checkout, always on `main`)
- `B=<branch>`; `C=$JOBS/clones/$B` (the clone, the only writer)
- `EV=docs/plans/<cycle>/<slice>-evidence` (committed evidence, written in the
  clone: briefs, lens JSONs, adjudications, reports; the driver's logs and
  ledgers stay under `$JOBS`)

## 0. Budget and account

    python3 $SKILL/agent-budget.py --live --account

Read the `codex live` line (weekly window used, reset date) and the account
line (it must name the personal plan). Rerun before any fan-out.

## 1. Clone

    git clone --local "$R" "$C" && cd "$C" && git checkout -b "$B"
    (cd frontend && npm ci --prefer-offline --no-audit --no-fund)
    (cd backend && dotnet restore RunCoach.slnx)
    lefthook install
    mkdir -p docs/specs && cp -R "$R/docs/specs/<slice>" docs/specs/
    mkdir -p "$EV"

`node_modules` must be a real directory: the sandbox refuses writes through a
symlink that points outside the workspace, and both `tsc` and `vite` write
under `node_modules`. The clone has no `docs/specs` (gitignored), so the spec
is copied in. `$R` stays on `main`.

## 2. Recon: read-only fleet over the main tree

Write `$C/$EV/recon/items.jsonl`, one line per lens (three to six lenses,
each over a bounded file set):

    {"id": "r1-<topic>", "slice": "Slice N PR-A", "slice_summary": "<one paragraph from the cycle plan>", "brief": "<the questions>", "files": "path, path, path"}

    python3 $SKILL/codex-fleet.py --name <slice>-recon --items "$C/$EV/recon/items.jsonl" \
      --template .claude/codex/recon-prompt.txt --schema .claude/codex/recon-schema.json \
      --workers 3 --timeout 900 --cwd "$R"
    python3 $SKILL/codex-fleet.py --name <slice>-recon --collect "$C/$EV/recon/out.json"

`--dry-run` first renders the prompts under `$JOBS/fleets/<name>/prompts/` for
a read-through. The session adjudicates the recommendations into the spec.

## 3. Spec red-team: both families

Claude side: a Workflow with one opus agent at `xhigh`, given
`.claude/codex/redteam-prompt.txt` rendered for the spec and
`redteam-schema.json` as its schema; save its JSON to `$C/$EV/redteam/opus.json`.
Codex side, sol at `max`:

    printf '%s\n' '{"id":"sol","doc_kind":"spec","doc_path":"docs/specs/<slice>/spec.md","context_paths":"docs/plans/<cycle>/cycle-plan.md, docs/plans/<cycle>/slice-N-<name>.md"}' > "$C/$EV/redteam/items.jsonl"
    python3 $SKILL/codex-fleet.py --name <slice>-redteam --items "$C/$EV/redteam/items.jsonl" \
      --template .claude/codex/redteam-prompt.txt --schema .claude/codex/redteam-schema.json \
      --model gpt-5.6-sol --effort max --workers 1 --timeout 1500 --cwd "$R"

Adjudicate both into `$C/$EV/redteam/adjudication.md`. A sol finding stands
only when the Claude side or a repo check confirms it. Revise the spec; rerun
on REJECT. Then commit the evidence so far in the clone (`git add "$EV"`,
`git commit`): reviewers' worktrees carry only what HEAD~1 committed.

## 4. Build

Write `$C/$EV/build-vars.json` with `slice`, `branch`, `spec_path`,
`plan_paths`, `extra_reading`, `file_list`, `extra_constraints`, then:

    python3 .claude/codex/render.py .claude/codex/build-brief.txt "$C/$EV/build-vars.json" > "$C/$EV/build-brief.txt"
    bash $SKILL/cx.sh run <slice>-build "$C" gpt-5.6-luna xhigh "$C/$EV/build-brief.txt"
    bash $SKILL/cx.sh wait <slice>-build 60

`cx.sh run` exits 1 on success: never chain it with `&&`. Run `wait` in a
background Bash; a build takes 30 to 75 minutes. Then:

    cp $JOBS/jobs/<slice>-build.result.md "$C/.stage-report.md"
    cp $JOBS/jobs/<slice>-build.result.md "$C/$EV/build-report.md"
    cd "$C" && git status --short        # the changes must exist; a builder can report COMPLETE with an empty tree
    git add <the brief's file list> "$EV"   # never git add -A
    git commit                           # lefthook runs here

Run from the session what the sandbox could not, and paste each result into
`$EV/build-orchestrator-runs.md`: every `dotnet test` (the sandbox cannot
create the test host's named pipe), Playwright, and the codegen chain if a
wire changed. Builders build with the in-process flag bundle in `AGENTS.md`.

## 5. Review: two luna lenses in detached worktrees

    cd "$R" && git fetch "$C" "$B:$B"
    [ "$(git rev-parse "$B")" = "$(git -C "$C" rev-parse HEAD)" ] || echo "STALE FETCH"
    for L in mutation conformance; do
      W="$JOBS/worktrees/$B-r1-$L"
      git worktree add --detach "$W" "$B"
      cp -R "$C/frontend/node_modules" "$W/frontend/"
      (cd "$W/backend" && dotnet restore RunCoach.slnx)
      mkdir -p "$W/docs/specs" && cp -R "docs/specs/<slice>" "$W/docs/specs/"
      cp "$C/.stage-report.md" "$W/"
    done

A rewritten clone branch needs `"+$B:$B"`; the SHA check catches a stale
fetch either way. One items line per lens (the snippet at the end builds
it), then one fleet per lens so `--cwd` points at its worktree. Both lenses
get `--write`: the mutation lens mutates and restores, and the conformance
lens needs a writable temp to run anything.

    python3 $SKILL/codex-fleet.py --name <slice>-r1-mutation --items "$C/$EV/round1/items-mutation.jsonl" \
      --template .claude/codex/review-context.txt --schema .claude/codex/review-schema.json \
      --workers 1 --timeout 1500 --cwd "$JOBS/worktrees/$B-r1-mutation" --write
    python3 $SKILL/codex-fleet.py --name <slice>-r1-conformance --items "$C/$EV/round1/items-conformance.jsonl" \
      --template .claude/codex/review-context.txt --schema .claude/codex/review-schema.json \
      --workers 1 --timeout 1500 --cwd "$JOBS/worktrees/$B-r1-conformance" --write

Copy `$JOBS/fleets/<name>/results/*.json` into `$C/$EV/round1/`. Then the
required step: run every `orchestrator_runs` command from the session and
write each command with its output into `$C/$EV/round1/orchestrator-runs.md`.
A mutation marked NOT_RUNNABLE_IN_SANDBOX stays unverified until that file
records its run.

## 6. Adjudicate and fix

Write `$C/$EV/round1/fix-list.txt`: `F1..Fn`, each with severity, which lens
raised it, and the exact change; mark items closed by an orchestrator run as
ORCHESTRATOR-RAN with the output. Drop what the repo refutes; merge
duplicates. Render `fix-brief.txt` with `slice`, `branch`, `head`,
`spec_path`, `fix_list`, `allowed_files` to `$C/$EV/round1/fix-brief.txt` and
copy it to `$C/.fix-brief.txt`; `cx.sh run <slice>-fix1` in the clone and
`wait`; copy the result to `$C/.stage-report.md` and `$C/$EV/round1/fix-report.md`;
`git status --short` (the changes must exist); stage the file list and `$EV`;
commit.

## 7. Verify round

Remove the round-1 worktrees (`git worktree remove --force`, then
`git worktree prune`), fetch the branch again with the SHA check, add fresh
worktrees at NEW paths (`$B-r2-verify`), copy in the spec, `node_modules`,
a restore, `.stage-report.md`, and `.fix-brief.txt` (the round-1 JSONs ride
the commit under `$EV`). Run one luna lens with `lens-verify.txt` (with
`--write`). On a disagreement between lenses, add one opus lens through the
Workflow tool at `xhigh` with the same schema, given the worktree's absolute
path and told to `cd` there first (no worktree isolation flag). Repeat 6 and
7 until zero blocker and zero major; two rounds is typical.

## 8. Ship

    grep -rn '/Users/\|@' "$C/$EV" && echo "SCRUB BEFORE COMMIT"   # public repo: no home paths, no addresses
    cd "$R" && git fetch "$C" "$B:$B" && git push -u origin "$B"

PR body: a luna draft (`draft-prompt.txt` with `artifact_kind` "a pull
request body", `shape_example_path` a recent PR body saved under `$EV`,
`sources` the stage reports) that the session edits; then `gh pr create`.
The maintainer's code-gauntlet run is the cross-family pass; address its
findings when asked. The maintainer merges.

## 9. Clean up

`git worktree remove --force` each worktree, `git worktree prune`,
`/bin/rm -rf "$C"` once the branch is pushed. List broker processes with
`pgrep -fl app-server-broker.mjs` and kill only those whose `--cwd` was this
repo's clone or worktrees; other sessions own the rest.

## Items-file snippet

    python3 - <<'PY'
    import json
    lens = open('.claude/codex/lens-mutation.txt').read().format(diff_base='HEAD~1')
    print(json.dumps({
        "id": "mutation", "slice": "Slice 5 PR-A",
        "spec_path": "docs/specs/slice-5-onboarding/spec.md", "diff_base": "HEAD~1",
        "allowed_commands": "dotnet build --no-restore; dotnet format --verify-no-changes; dotnet test --no-build --filter-class on container-free classes; npm run build; npx vitest run; npx eslint; npx prettier --check",
        "lens_task": lens}))
    PY

## Conventions

- Effort: luna `xhigh` for every build, review, fix, and recon lens; sol
  `max` only on the red-team row; never `max` on luna as a default.
- Timeouts: 900 s per recon lens, 1500 s per review lens; builds and fixes go
  through the companion, which `cx.sh wait` polls without a timeout.
- Freeze inputs: finish a fleet before landing the documents it reasons
  about, or its later items read a ruling and go circular.
- ASCII in every prompt and schema string; quotes under 300 characters.
- Never put `.env`, secrets, or anything under `~/.codex` in a prompt or a
  read.
