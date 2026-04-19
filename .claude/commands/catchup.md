Read the following in order to understand current project state, then provide a brief summary of where things stand and what the next task is.

## Files to read

1. **`ROADMAP.md`** — Status block at top: current cycle, active slice, next step, blockers. Grab the pointer to the active cycle plan.
2. **Active cycle plan** (path from `ROADMAP.md` Status block, e.g., `docs/plans/{cycle-name}/cycle-plan.md`) — read the Status section, the "Captured During Cycle" follow-ups table, and the active slice's acceptance criteria.
3. **Active slice plan** (path from the cycle plan's Status under "Active Slice", if one exists, e.g., `docs/plans/{cycle-name}/slice-N-{name}.md`) — step-by-step implementation plan and in-progress tasks. Skip if no active slice yet.

## Git state

4. `git log --oneline -10` — last 10 commits on current branch.
5. `git status` + `git diff --stat main...HEAD` if on a feature branch — working-tree changes and branch divergence.

## Summary format

3-5 sentences covering:

- Current cycle and active slice.
- What was shipped most recently.
- The next concrete action.
- Any visible blockers or in-flight changes.

Point the user at the exact file they should open next if one is obvious (active slice plan, next slice's acceptance criteria, the follow-up they flagged last session, etc.).
