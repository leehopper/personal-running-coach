# Codex stage recipe

The templates in this directory and the step-by-step for running a slice on
the two-family routing in `.claude/rules/codex-dispatch.md`. Placeholders are
`{name}`; render a template with
`python3 .claude/codex/render.py <template> <vars.json> > <prompt>` (the same
`str.format` the fleet driver applies to `--template`; a literal brace is
doubled). Codex agents read `AGENTS.md` for the sandbox facts and house rules;
the brief carries only the per-task contract.

Paths used below:

- The orchestration kit's commands, always spelled by full path:
  `~/personal/orchestration-kit-stable/bin/codex-fleet --seat <seat>` and
  `~/personal/orchestration-kit-stable/bin/agent-budget`. The seat picks the
  model, effort and default timeout; `--model` and `--effort` are errors.
  Seats and mechanics: the seat table in `~/.claude/CLAUDE.md` and the plugin
  skill `orchestration-kit:codex-orchestration`. A single task is a one-item
  fleet.
- `STATE=~/.claude/orchestration-state` (fleet state under
  `fleets/<name>/`: `prompts/` from a dry run, `results/<id>.json`, logs)
- `JOBS=~/.claude/codex-jobs` (clones under `clones/`, worktrees under
  `worktrees/`)
- `R=$PWD` (this repo's main checkout, always on `main`)
- `B=<branch>`; `C=$JOBS/clones/$B` (the clone, the only writer)
- `EV=docs/plans/<cycle>/<slice>-evidence` (committed evidence, written in the
  clone: briefs, lens JSONs, adjudications, reports; the driver's logs and
  ledgers stay under `$JOBS`)

## 0. Budget and account

    ~/personal/orchestration-kit-stable/bin/agent-budget --live --account --brief

Read the `codex live` line (weekly window used, reset date) and the account
line (it must name the personal plan). Rerun before any fan-out.

## 1. Clone

    git clone --local "$R" "$C" && cd "$C" && git checkout -b "$B"
    (cd frontend && npm ci --prefer-offline --no-audit --no-fund)
    (cd backend && dotnet restore RunCoach.slnx)
    lefthook install
    mkdir -p docs/specs && cp -R "$R/docs/specs/<slice>" docs/specs/
    mkdir -p "$EV/recon" "$EV/redteam" "$EV/round1" "$EV/round2"

Later rounds create their own `$EV/round<N>` before writing into it.

`node_modules` must be a real directory: the sandbox refuses writes through a
symlink that points outside the workspace, and both `tsc` and `vite` write
under `node_modules`. The clone has no `docs/specs` (gitignored), so the spec
is copied in. `$R` stays on `main`.

## 2. Recon: read-only fleet over the main tree

Write `$C/$EV/recon/items.jsonl`, one line per lens (three to six lenses,
each over a bounded file set):

    {"id": "r1-<topic>", "slice": "Slice N PR-A", "slice_summary": "<one paragraph from the cycle plan>", "brief": "<the questions>", "files": "path, path, path"}

    ~/personal/orchestration-kit-stable/bin/codex-fleet --name <slice>-recon --seat recon_large \
      --items "$C/$EV/recon/items.jsonl" \
      --template .claude/codex/recon-prompt.txt --schema .claude/codex/recon-schema.json \
      --workers 3 --cwd "$R" --dry-run
    # read the rendered prompts, then the same command without --dry-run
    ~/personal/orchestration-kit-stable/bin/codex-fleet --name <slice>-recon --seat recon_large \
      --items "$C/$EV/recon/items.jsonl" \
      --template .claude/codex/recon-prompt.txt --schema .claude/codex/recon-schema.json \
      --collect "$C/$EV/recon/out.json"

The `--dry-run` renders the prompts under `$STATE/fleets/<name>/prompts/`;
`--collect` needs the same `--name`, `--items`, `--template` and `--schema`
as the launch. A dry run leaves `{name}` in place for any field the items
line lacks, so read the rendered prompt for leftover braces. The first fleet
on a seat with no ledger history exits `estimate_required`: rerun with
`--estimate-credits` and `--estimate-reason`. The session adjudicates the recommendations into
the spec.

A lens over a design source against the code (cross-document recall) uses
`--seat recall`. A lens over one file of about 300 lines or fewer uses
`--seat recon_small`. A single recon that ends in a judgment over anything
larger the session would otherwise read inline (a branch's whole diff, a CI
or suite log, a gauntlet or CodeRabbit report, a research artifact) is a
one-item `recon_large` fleet with the same template and schema and
`--workers 1`; a task that needs more depth escalates by the seat's own row
(`--escalate on=needs_depth reason=<why>`). The session reads the collected
JSON, never the source.

## 3. Spec red-team: both families

Claude side: a Workflow with one agent on the `red_team` seat's Claude row
(model and effort from
`~/personal/orchestration-kit-stable/bin/seat resolve red_team --engine-mode claude_only`),
given `.claude/codex/redteam-prompt.txt` rendered for the spec and
`redteam-schema.json` as its schema; save its JSON to `$C/$EV/redteam/opus.json`.
Codex side, the same seat:

    printf '%s\n' '{"id":"codex","doc_kind":"spec","doc_path":"docs/specs/<slice>/spec.md","context_paths":"docs/plans/<cycle>/cycle-plan.md, docs/plans/<cycle>/slice-N-<name>.md"}' > "$C/$EV/redteam/items.jsonl"
    ~/personal/orchestration-kit-stable/bin/codex-fleet --name <slice>-redteam --seat red_team \
      --items "$C/$EV/redteam/items.jsonl" \
      --template .claude/codex/redteam-prompt.txt --schema .claude/codex/redteam-schema.json \
      --workers 1 --cwd "$R"

A red-team that needs more depth escalates by the seat's row
(`--escalate on=needs_depth reason=<why>`) and keeps its Claude check; when the
Codex side is unavailable (usage limit), rerun after the reset or name the
substitution in the adjudication. Adjudicate both
into `$C/$EV/redteam/adjudication.md`. A Codex finding stands only when the
Claude side or a repo check confirms it. Treat a Codex list as
candidates, not verdicts. Revise the spec; rerun
on REJECT. Then commit the evidence so far in the clone (`git add "$EV"`,
`git commit`): reviewers' worktrees carry only what HEAD~1 committed.

## 4. Build

Write `$C/$EV/build-items.jsonl`, one line holding `id`, `slice`, `branch`,
`spec_path`, `plan_paths`, `extra_reading`, `file_list`, `extra_constraints`
(the fleet renders `build-brief.txt` with these), then from the clone:

    cd "$C" && ~/personal/orchestration-kit-stable/bin/codex-fleet --name <slice>-build --seat build_bounded \
      --items "$C/$EV/build-items.jsonl" --template .claude/codex/build-brief.txt \
      --schema .claude/codex/build-schema.json --workers 1 --write --cwd "$C" --timeout 5400 --dry-run

Read the rendered prompt, then run the same command without `--dry-run`,
detached (a Bash `run_in_background`, or `nohup ... &`), and wait with one
blocking call; a build takes 30 to 75 minutes. A refactor of about 20 files or
more, or one that holds a cross-module invariant, runs the same command with
`--seat build_refactor`; a stage that failed review twice escalates by the
seat's row (`--escalate on=two_failed_reviews reason=<why>`).

The launcher accepts only a JSON result. The agent writes its Markdown stage
report to `.codex-report.md` in the clone (gitignored) and returns a small
JSON: status, report path and line count, files changed, gates. Read the
result at `$STATE/fleets/<slice>-build/results/<id>.json` (or collect it),
then:

    cp "$C/.codex-report.md" "$C/.stage-report.md"
    cp "$C/.codex-report.md" "$C/$EV/build-report.md"
    cd "$C" && git status --short        # the changes must exist; a builder can report COMPLETE with an empty tree
    git add <the brief's file list> "$EV"   # never git add -A
    git commit                           # lefthook runs here

Run from the session what the sandbox could not, and paste each result into
`$EV/build-orchestrator-runs.md`: every `dotnet test` (the sandbox cannot
create the test host's named pipe), Playwright, and the codegen chain if a
wire changed. Builders build with the in-process flag bundle in `AGENTS.md`.

## 5. Review: two `review_lens` lenses in detached worktrees

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

    ~/personal/orchestration-kit-stable/bin/codex-fleet --name <slice>-r1-mutation --seat review_lens \
      --items "$C/$EV/round1/items-mutation.jsonl" \
      --template .claude/codex/review-context.txt --schema .claude/codex/review-schema.json \
      --workers 1 --timeout 1500 --cwd "$JOBS/worktrees/$B-r1-mutation" --write
    ~/personal/orchestration-kit-stable/bin/codex-fleet --name <slice>-r1-conformance --seat review_lens \
      --items "$C/$EV/round1/items-conformance.jsonl" \
      --template .claude/codex/review-context.txt --schema .claude/codex/review-schema.json \
      --workers 1 --timeout 1500 --cwd "$JOBS/worktrees/$B-r1-conformance" --write

Copy `$STATE/fleets/<name>/results/*.json` into `$C/$EV/round1/`. Then `python3 .claude/codex/check-review.py "$C/$EV/round1/"*.json`: a report that fails this gate is rerun, not adjudicated.
Then the
required step: run every `orchestrator_runs` command from the session and
write each command with its output into `$C/$EV/round1/orchestrator-runs.md`.
A mutation marked NOT_RUNNABLE_IN_SANDBOX stays unverified until that file
records its run.

## 6. Adjudicate and fix

Write `$C/$EV/round1/fix-list.txt`: `F1..Fn`, each with severity, which lens
raised it, and the exact change; mark items closed by an orchestrator run as
ORCHESTRATOR-RAN with the output. Drop what the repo refutes; merge
duplicates. Write `$C/$EV/round1/fix-items.jsonl`, one line with `id`, `slice`, `branch`,
`head`, `spec_path`, `fix_list`, `allowed_files`, and copy the fix list to
`$C/$EV/round1/fix-brief.txt` and `$C/.fix-brief.txt` for the record and
the verify round. Run the fix round in the clone
as a one-item fleet on the seat that matches the work: `fix_mechanical` (test
rows, renames, report items), `fix_mid` (one module's logic) or `fix_hard`
(crash consistency, shell, cross-module):

    cd "$C" && ~/personal/orchestration-kit-stable/bin/codex-fleet --name <slice>-fix1 --seat fix_mid \
      --items "$C/$EV/round1/fix-items.jsonl" --template .claude/codex/fix-brief.txt \
      --schema .claude/codex/fix-schema.json --workers 1 --write --cwd "$C" --timeout 5400 --dry-run

then without `--dry-run`, detached, as in the build. The agent writes its
report to `.codex-report.md` and returns the JSON. Copy the report to
`$C/.stage-report.md` and `$C/$EV/round1/fix-report.md`;
`git status --short` (the changes must exist); stage the file list and `$EV`;
commit.

## 7. Verify round

Remove the round-1 worktrees (`git worktree remove --force`, then
`git worktree prune`), fetch the branch again with the SHA check, add fresh
worktrees at NEW paths (`$B-r2-verify`), copy in the spec, `node_modules`,
a restore, `.stage-report.md`, and `.fix-brief.txt` (the round-1 JSONs ride
the commit under `$EV`). Run one `review_lens` lens with `lens-verify.txt` (with
`--write`). On a disagreement between lenses, add one lens on the seat's Claude row through the
Workflow tool with the same schema, given the worktree's absolute
path and told to `cd` there first (no worktree isolation flag). Repeat 6 and
7 until zero blocker and zero major; two rounds is typical.

## 8. Ship

    grep -rnE '(/Users/|/home/|[A-Za-z]:\\Users\\|[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,})' "$C/$EV"; rc=$?
    [ "$rc" -eq 1 ] || { echo "SCRUB BEFORE PUSH (grep rc=$rc: 0 = matches above, 2 = grep error)"; false; }
    [ "$rc" -eq 1 ] && cd "$R" && git fetch "$C" "$B:$B" && git push -u origin "$B"

The push runs only when the scrub finds nothing (grep exit 1); a match or a grep error stops here. The pattern covers macOS, Linux, and Windows home paths and email addresses.

PR body: a `draft_routine` draft with `draft-prompt.txt` (`artifact_kind` "a pull
request body", `shape_example_path` a recent PR body saved under `$EV`,
`sources` the stage reports) that the session edits; then `gh pr create`.
The same template drafts a slice spec, a cycle-plan section, or a
decision-log entry, but those hold a structure over many facts and run on
`draft_structured`.
The cross-family pass is the headless code-gauntlet run the session launches
itself once the PR is ready (not a draft):
`~/personal/orchestration-kit-stable/bin/gauntlet-review <pr> --repo-dir "$R"`,
with `$R` checked out at the PR head. Address its findings before asking the
maintainer to look. The maintainer merges.

## 9. Clean up

`git worktree remove --force` each worktree, `git worktree prune`,
`/bin/rm -rf "$C"` once the branch is pushed. List leftover fleet processes with
`pgrep -fl codex-fleet` and kill only those whose `--cwd` was this
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

- Seats: `recon_large`/`recon_small`/`recall` for recon, `red_team`,
  `build_bounded`/`build_refactor`, `review_lens`, `fix_mechanical`/`fix_mid`/`fix_hard`,
  `draft_structured`/`draft_routine`. The seat sets model, effort and default
  timeout; escalate only by the seat's own row with a reason. `max` and
  `ultra` are never defaults.
- Timeouts: leave `--timeout` to the seat for recon, red-team and drafts; builds
  and fix rounds run `--timeout 5400`, review lenses `--timeout 1500`. A
  timed-out mutation lens leaves a mutated worktree: `git checkout .` in it
  before any rerun.
- Freeze inputs: finish a fleet before landing the documents it reasons
  about, or its later items read a ruling and go circular.
- ASCII in every prompt and schema string; quotes under 300 characters.
- Never put `.env`, secrets, or anything under `~/.codex` in a prompt or a
  read.
