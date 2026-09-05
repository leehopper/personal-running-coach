# Agent instructions (Codex)

Codex CLI agents launched into this repository by the maintainer's orchestrating
session read this file. Claude Code sessions read `CLAUDE.md` instead. The two
audiences differ: a Codex agent runs inside a sandbox as an implementer, a
reviewer, or a recon lens under a per-task brief. This file holds the durable
facts every brief would otherwise repeat. The brief wins where they differ.

## What this repository is

RunCoach, an AI running coach: a .NET 10 API under `backend/` and a React 19 +
Vite SPA under `frontend/`. The house rules live in `CLAUDE.md`,
`backend/CLAUDE.md`, `frontend/CLAUDE.md`, and the three `REVIEW.md` files.
Read the ones that touch your task before writing anything; reviewers judge by
them.

## Your environment

- You are in a throwaway local clone or a detached worktree, never the
  maintainer's main checkout. Your brief names the branch and the exact files
  you may touch. Touch nothing else; if a task forces another file, stop and
  say so in your report.
- Git is blocked for you: `git add`, `commit`, `checkout`, `stash`, `branch`,
  and `worktree` fail because the index lock is denied. Read-only git
  (`status`, `diff`, `log`, `show`) works. Leave changes in the working tree;
  the orchestrator stages the explicit file list from your brief and commits.
- Writes are confined to the workspace (your cwd) and `/tmp`. Measured
  2026-09-04: `$HOME` and `~/.nuget/packages` refuse writes; a write THROUGH
  a symlink whose target is outside the workspace is refused too, so
  `node_modules` in your clone is a real directory, never a symlink.
- Every shell command runs in a fresh shell; an exported variable does not
  survive to the next command. Create `/tmp/codex-agent-<task>` once and set
  `TMPDIR=/tmp/codex-agent-<task>` inline on every command that needs a temp
  dir. If `/tmp` is refused, use `mkdir -p .tmp-agent` in the workspace and
  never stage or mention it.
- There is no network: DNS does not resolve, so nothing can be fetched. The
  Docker socket cannot be reached, so nothing container-backed runs here.
  `git clone` (including `--local`) is refused with the rest of git's writes.
- Backend builds pass only in-process. Measured 2026-09-04: a plain
  `dotnet build` finishes compiling and then hangs on the build-server and
  node-reuse processes until killed; this form passes in about 6 s:
  `TMPDIR=/tmp/codex-agent-<task> DOTNET_CLI_HOME=/tmp/codex-agent-<task>/dotnet
  DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBUILDNODEREUSE=0
  dotnet build RunCoach.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --disable-build-servers`
  (measured as a bundle, not narrowed). `dotnet test` does not run here at
  all, even for a container-free class: the test host cannot create its
  named pipe (SocketException 13). Every backend test is the orchestrator's.
  `npm run build`, `npx vitest run <file>`, `npx eslint`, and
  `npx prettier --check` pass with a real `node_modules`.
- Destructive shell: the command policy rejects every `rm -f` form. Delete
  with `find <one path> -delete`, only under your temp dir unless your brief
  says otherwise. Take a `/bin/cp -f` backup into the temp dir before editing
  a file you may need to restore; in a worktree, restore from
  `git show HEAD:<path>`.
- Never read `.env`, `.env.*`, `appsettings.Local.json`, `secrets.json`,
  `*.pfx`, anything under `~/.codex`, or the .NET user-secrets store. If a
  diff you are shown contains a secret, stop and say so in your report.

## Gates you can run

- Backend, from `backend/`: `dotnet build RunCoach.slnx --no-restore` with the
  in-process flag bundle above (warnings are errors: SonarAnalyzer and the
  CA1848/CA1873 logging rules), and `dotnet format RunCoach.slnx --include
  <files> --no-restore --verify-no-changes`.
- Backend tests never run here (see above). Write them, make them compile,
  and hand the exact command to the orchestrator:
  `dotnet test --project tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj --no-build --filter-class <FullName>`
  (the filter flag is passed directly, with no `--` separator). Record the
  run as NOT RUNNABLE IN SANDBOX with the pipe error as evidence. Never
  loosen a test to make it pass here.
- Frontend, from `frontend/`: `npm run build` (tsc + vite), `npx vitest run
  <file>` or `npm run test`, `npx eslint <files>`, `npx prettier --check
  <files>`. `npm run build` does not run eslint; run it yourself.
- Never `dotnet restore`, `npm install`, or `npm ci`; the orchestrator
  restores before you start, and there is no network to fetch with. In a
  worktree, `dotnet build` first restores nothing either: report a missing
  `obj/project.assets.json` as a deviation instead of restoring.

## Rules that bind your output

- Prompt YAML under `backend/src/RunCoach.Api/Prompts/` is paired with a
  committed eval cache and a hash manifest. A change there needs a re-record
  the orchestrator runs with a funded key; do not edit prompts unless your
  brief says that re-record is planned.
- Never modify or delete an EF Core migration or the model snapshot. New
  migrations are the orchestrator's to add.
- User-facing text never uses the term "VDOT" (a trademark this project does
  not license); write "Daniels-Gilbert zones" or "pace-zone index". This
  covers prompts, UI strings, API responses, docs, and comments.
- Backend logging uses source-generated `[LoggerMessage]` partial methods;
  direct `ILogger` calls fail the build.
- Code comments are self-contained. Never cite a plan, spec, or decision-log
  path from code.
- Frontend path aliases: `@/` is `src/`, `~/` is `src/app/`.
- Generated code (`frontend/src/generated/`, `backend/openapi/swagger.json`)
  is regenerated, never hand-edited. A wire change needs the codegen chain
  your brief names.

## Reporting

- Your final message is the stage report your brief specifies. Every gate
  line quotes the command and its last output line, or says VERIFIED BY
  INSPECTION when you only read. Never claim a red or a green you did not
  run.
- Every finding cites `file:line` in your cwd and is marked MECHANIZED (you
  ran it) or INSPECTED (you read it).
- ASCII only in prose and comments. No em dashes. One idea per sentence.
- When a required step is impossible in this sandbox, say so under
  Deviations with the exact command the orchestrator should run. Do not work
  around it.
