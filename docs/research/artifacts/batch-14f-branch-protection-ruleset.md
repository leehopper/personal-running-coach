# GitHub rulesets configuration for a solo-maintained OSS repository

**Use repository rulesets, not legacy branch protection.** Rulesets are GitHub's actively developed governance system with granular bypass controls, rule layering, and auditability that legacy branch protection lacks. While branch protection is not formally deprecated as of April 2026, every new governance feature since 2023 has shipped exclusively in rulesets. This guide provides a complete click-by-click configuration, a phased rollout plan to avoid blocking merges during tool onboarding, an emergency bypass procedure, and guidance on signed commits and Dependabot — all grounded in current GitHub documentation.

One critical compatibility constraint shapes this entire configuration: **"Require signed commits" and "Rebase and merge" via the web UI are mutually incompatible.** GitHub's web interface cannot sign rebased commits. Since the project also requires linear history, the solution is straightforward: use squash-and-merge exclusively, which GitHub signs automatically with its web-flow GPG key.

---

## Why rulesets win over legacy branch protection

Legacy branch protection rules remain functional but stagnant. GitHub has not added a single new feature to branch protection since rulesets went GA. Meanwhile, rulesets received **12+ feature additions in 2025 alone**: merge method restrictions (March 2025), org-level rulesets for Team plans (June 2025), silent exemptions (September 2025), required team reviewers with file patterns (November 2025), and Copilot coding agent bypass support (November 2025).

The decisive advantages for this project are bypass granularity and auditability. Branch protection offers a binary "Do not allow bypassing the above settings" checkbox — either all admins can bypass or none can. Rulesets let you add specific roles as bypass actors with two modes: **"Always allow"** (direct push, full bypass) or **"For pull requests only"** (must open a PR but can bypass protections on merge). This distinction is the foundation of the emergency bypass procedure described later.

Other ruleset-only features relevant here include rule layering (multiple rulesets can target `main` simultaneously, with the most restrictive version of each rule winning), Rule Insights for monitoring bypass usage, and JSON import/export for backup. The only features branch protection has that rulesets lack — merge queues and branch locking — are irrelevant for a solo-maintainer workflow.

**Recommendation: Create a single repository ruleset.** A solo maintainer does not need the complexity of multiple layered rulesets. One ruleset targeting `main` covers everything.

---

## Complete UI configuration: click-by-click

Navigate to **Settings → Rules → Rulesets → New ruleset → New branch ruleset**. Configure every field as follows:

### Ruleset metadata

| Field | Value | Notes |
|---|---|---|
| **Ruleset name** | `main-protection` | Descriptive, lowercase, used in audit logs |
| **Enforcement status** | **Disabled** initially → **Active** after soft-launch | Start Disabled; switch to Active after Phase 3 of rollout |
| **Target branches** | Add target → Include by pattern: `main` | Exact match on the trunk branch |

### Bypass list

| Actor | Bypass mode | Rationale |
|---|---|---|
| **Repository admins** | **For pull requests only** | The solo maintainer can bypass rules in emergencies but must open a PR, creating an audit trail. Cannot push directly to `main`. |

Do **not** add Dependabot as a bypass actor. Dependabot PRs should pass the same checks as human PRs. Do not add any other actors.

### Branch rules (enable each toggle)

**Restrict creations** — ✅ Enabled
Only bypass actors can create branches matching `main`. Prevents accidental recreation of `main` if deleted.

**Restrict updates** — ❌ Leave disabled
This would prevent all non-bypass pushes to `main`, but since "Require a pull request before merging" already handles this, enabling both is redundant and could interfere with merge operations.

**Restrict deletions** — ✅ Enabled (default)
Prevents deletion of `main`.

**Require linear history** — ✅ Enabled
Enforces squash or rebase merges only. No merge commits allowed. Combined with the repo-level merge settings (see below), this ensures a clean linear history.

**Require signed commits** — ✅ Enabled (see signed commits section for friction analysis)

**Require a pull request before merging** — ✅ Enabled, with these sub-settings:

| Sub-setting | Value | Notes |
|---|---|---|
| Required approvals | **0** | Solo dev; change to **1** when first external contributor joins |
| Dismiss stale pull request approvals when new commits are pushed | ✅ Yes | Future-proofing for when approvals > 0 |
| Require review from Code Owners | ❌ No | No `CODEOWNERS` file yet; enable when module ownership stabilizes |
| Require approval of the most recent reviewable push | ❌ No | Not meaningful with 0 approvals; enable at the same time as setting approvals to 1 |
| Require conversation resolution before merging | ✅ Yes | Prevents merging with unresolved review threads |

**Require status checks to pass before merging** — ✅ Enabled

| Sub-setting | Value |
|---|---|
| Require branches to be up to date before merging | ✅ Yes |

**Status checks to add** (add each individually in the "Add checks" dialog):

| Check name (exact string) | Source | When to add |
|---|---|---|
| `gate` | Your CI workflow | Phase 1 (immediate) |
| `CodeQL / Analyze (csharp)` | GitHub CodeQL | Phase 2 (after first CodeQL run on `main`) |
| `CodeQL / Analyze (javascript-typescript)` | GitHub CodeQL | Phase 2 |
| `SonarCloud Code Analysis` | SonarQube Cloud app | Phase 3 (after first SonarQube run on a PR) |

**Important check name note:** SonarQube Cloud (the rebranded SonarCloud) currently reports its check as **`SonarCloud Code Analysis`** in most configurations. Verify the exact string by opening any PR where SonarQube has run and checking the status checks section. For monorepo setups, the name changes to `[project_name] SonarCloud Analysis`. Do **not** add CodeRabbit's check as required — it is advisory only and should remain informational.

**Block force pushes** — ✅ Enabled (default)

**Require deployments to succeed** — Leave unchecked (N/A per requirements)

Click **Create** to save the ruleset in Disabled state.

### Repository-level merge settings (separate from the ruleset)

Navigate to **Settings → General → Pull Requests** and configure:

| Setting | Value | Rationale |
|---|---|---|
| Allow merge commits | ❌ Uncheck | Linear history requires squash or rebase only |
| Allow squash merging | ✅ Check | Primary merge method; GitHub signs squash commits |
| Default to pull request title and description | ✅ Select | Clean commit messages |
| Allow rebase merging | ❌ **Uncheck** | GitHub **cannot sign** rebase-merged commits via the web UI, which conflicts with "Require signed commits" |
| Allow auto-merge | ✅ Check | Required for Dependabot auto-merge workflow |
| Automatically delete head branches | ✅ Check | Keeps the repo clean |

Disabling rebase merge is the key decision that makes signed commits friction-free. Squash merge produces identical linear history while being fully compatible with GitHub's web-flow commit signing.

---

## Signed commits are friction-free with one constraint

GitHub automatically signs all commits created through its web interface — including squash merges, merge commits, and file edits — using its **web-flow GPG key** (RSA-4096, rotated January 2024, public key at `github.com/web-flow.gpg`). These commits display as "Verified" in the GitHub UI. The sole exception is **rebase-and-merge**: GitHub cannot sign rebased commits because it would need the original committer's private key. This is the documented reason to disable rebase merge when requiring signed commits.

For local commits, the lowest-friction signing setup in 2026 uses **1Password SSH signing**. The setup takes approximately 5 minutes: enable the 1Password SSH Agent, generate an SSH key in 1Password, select "Configure Commit Signing" from the key's context menu (which auto-populates `~/.gitconfig`), and upload the public key to GitHub as a **Signing Key** (not Authentication Key) at `github.com/settings/keys`. Every subsequent `git commit` is signed via Touch ID or Windows Hello biometric prompt. Git 2.34+ is required.

The resulting `~/.gitconfig` entries:

```ini
[gpg]
    format = ssh
[gpg "ssh"]
    program = /Applications/1Password.app/Contents/MacOS/op-ssh-sign
[commit]
    gpgsign = true
[user]
    signingkey = ssh-ed25519 AAAA...
```

**Dependabot commits are signed by default.** GitHub enabled bot commit signing for all bots, and Dependabot commits display as "Verified." Since Dependabot PRs will be squash-merged (not rebased), the signing requirement introduces zero friction for automated dependency updates.

**Vigilant mode** is an informational complement worth enabling. Under Settings → SSH and GPG keys → "Flag unsigned commits as unverified," this makes unsigned commits attributed to your account display a yellow "Unverified" badge rather than no badge at all. It does not enforce anything but provides visual confirmation that your signing pipeline is working.

---

## Soft-launch sequencing: four phases over two weeks

The core challenge is that requiring a status check that has never run produces a permanent **"Waiting for status to be reported"** state that blocks all merges indefinitely. The solution is to onboard each check tool before making it required.

**Important plan limitation:** Evaluate mode (audit-only enforcement that logs violations without blocking) requires a **GitHub Team or Enterprise Cloud plan**. On GitHub Free with a public repo, rulesets only support Active and Disabled states. The phased plan below works on all plans by using the Disabled → Active toggle and incremental check addition.

### Phase 0 — Tool onboarding (Days 1–3)

Run each tool on `main` at least once to establish a baseline:

1. **Enable CodeQL default setup**: Settings → Code security → Code scanning → enable "Default setup." This triggers an initial analysis on `main` via push. Wait for the `CodeQL / Analyze (csharp)` and `CodeQL / Analyze (javascript-typescript)` checks to complete successfully.

2. **Configure SonarQube Cloud**: Connect the repository to SonarQube Cloud. Open a test PR (even a whitespace change) and confirm the `SonarCloud Code Analysis` check appears and passes. Merge it.

3. **Verify `gate` check**: Confirm your existing CI workflow's `gate` job runs and reports status on PRs. It should already have history on `main`.

4. **Verify check names**: On any PR where all tools have run, go to the Checks tab and note the **exact check name strings**. A single-character mismatch in the ruleset will cause permanent blocking.

### Phase 1 — Create ruleset with `gate` only (Day 4)

1. Create the ruleset as documented above, with enforcement set to **Disabled**.
2. Add only the `gate` status check (the one with existing history).
3. Switch enforcement to **Active**.
4. Open a test PR, confirm `gate` runs and the PR becomes mergeable. Merge it.

This validates the ruleset mechanics with a check you trust.

### Phase 2 — Add CodeQL checks (Days 5–7)

1. Edit the ruleset. Add `CodeQL / Analyze (csharp)` and `CodeQL / Analyze (javascript-typescript)` to the required status checks.
2. Open a PR and confirm all three checks (gate + two CodeQL) report status and pass.
3. If a CodeQL check stays "Pending," verify the check name matches exactly and that CodeQL is configured to run on PRs (not just scheduled scans).

### Phase 3 — Add SonarQube Cloud check (Days 8–10)

1. Edit the ruleset. Add `SonarCloud Code Analysis` to the required status checks.
2. Open a PR and confirm all four checks report and pass.
3. If the SonarQube check is missing, verify the SonarQube Cloud GitHub App is installed and the project is bound to the repository.

### Phase 4 — Enable signed commits and remaining rules (Days 11–14)

1. Ensure 1Password SSH signing is configured and your signing key is uploaded to GitHub.
2. Make a local signed commit and push to a test branch to verify the "Verified" badge appears.
3. Edit the ruleset and enable "Require signed commits."
4. Confirm that the next PR merge (via squash) produces a "Verified" commit on `main`.

**Rollback at any phase:** If any phase causes blocking, edit the ruleset and switch enforcement to **Disabled**. This immediately unblocks all PRs. Fix the issue, then re-enable. The toggle is logged in your personal security log under `repository_ruleset` events.

### If you have a GitHub Team plan

Replace the Disabled → Active toggle approach with Evaluate mode: create the full ruleset with all checks in **Evaluate** enforcement, monitor the **Rule Insights** page (Settings → Rules → Insights) for a week to confirm everything passes, then switch to **Active**. This is the ideal approach because you can see exactly which PRs would have been blocked without actually blocking them.

---

## Emergency bypass procedure

The bypass mechanism is designed around a principle: **the solo maintainer is both governed by and able to override the rules, but every override leaves a visible trail.**

### Normal state

The ruleset's bypass list contains "Repository admins" with **"For pull requests only"** mode. This means the maintainer:

- Cannot push directly to `main` (must open a PR)
- Can merge a PR even if required checks haven't passed, by choosing to bypass the rules
- Every bypass is recorded in Rule Insights and the personal security log

### Emergency merge procedure (e.g., CI outage blocking a critical hotfix)

1. **Create a branch** and push the hotfix commit (signed, as usual).
2. **Open a PR** to `main`. The failing/pending checks will block the normal merge button.
3. **Click the merge button's bypass option.** Because "Repository admins" is in the bypass list with "For pull requests only," GitHub presents the option to merge despite failing checks. The UI shows a warning banner indicating which rules are being bypassed.
4. **Select "Squash and merge"** to maintain the signed-commit requirement (GitHub signs the squash commit).
5. **Document the bypass.** The Rule Insights page will show this event with the actor, timestamp, and which rules were bypassed. Add a comment to the PR explaining the emergency.

### Nuclear option (ruleset toggle)

If the bypass actor approach fails for any reason:

1. Navigate to Settings → Rules → Rulesets → `main-protection`.
2. Change enforcement from **Active** to **Disabled**.
3. Perform the emergency operation.
4. Change enforcement back to **Active** immediately.
5. This produces a `repository_ruleset` event in your security log with the timestamp and change details.

### What gets audit-logged

| Event | Where logged | Retention |
|---|---|---|
| Bypass actor merges despite failing checks | **Rule Insights** (repo-level) + personal security log | Rule Insights: accessible via API; Security log: **90 days** |
| Ruleset enforcement toggled (Active ↔ Disabled) | Personal security log (`repository_ruleset` event) | 90 days |
| Ruleset edited (checks added/removed) | Personal security log | 90 days |

For organization-owned repos, these events also appear in the organization audit log with **180-day retention** and streaming support.

---

## Dependabot needs no special bypass treatment

Dependabot PRs should flow through the identical checks as human-authored PRs. This is GitHub's recommended approach for public OSS repositories: dependency updates must pass CI, CodeQL, and quality gates before merging, catching breaking changes from upstream packages.

**With 0 required approvals, the auto-merge workflow is minimal.** Enable auto-merge at the repository level (Settings → General → Allow auto-merge), then add this GitHub Actions workflow:

```yaml
name: Dependabot auto-merge
on: pull_request
permissions:
  contents: write
  pull-requests: write
jobs:
  dependabot:
    runs-on: ubuntu-latest
    if: github.event.pull_request.user.login == 'dependabot[bot]'
    steps:
      - name: Fetch Dependabot metadata
        id: metadata
        uses: dependabot/fetch-metadata@v2
        with:
          github-token: "${{ secrets.GITHUB_TOKEN }}"
      - name: Enable auto-merge for patch and minor updates
        if: steps.metadata.outputs.update-type != 'version-update:semver-major'
        run: gh pr merge --auto --squash "$PR_URL"
        env:
          PR_URL: ${{ github.event.pull_request.html_url }}
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

This workflow enables auto-merge on every non-major Dependabot PR. Once all required status checks pass (gate, CodeQL, SonarQube), GitHub automatically squash-merges the PR. No approval is needed because required approvals is 0. Major version bumps are left for manual review.

Dependabot commits are **signed by GitHub's web-flow key** and display as "Verified." The squash merge also produces a signed commit. No signing friction exists in the Dependabot pipeline.

If you later increase required approvals to 1+, add an auto-approve step before the auto-merge step using `gh pr review --approve "$PR_URL"` — this programmatically approves the PR so the auto-merge can proceed.

---

## Conclusion

This configuration achieves defense-in-depth with minimal daily friction for a solo maintainer. The single most impactful decision is **disabling rebase merge** at the repository level: this one toggle eliminates the entire class of signed-commit compatibility problems while preserving linear history through squash merging. The phased rollout prevents the common pitfall of requiring a status check before the tool has ever reported — a mistake that silently blocks all merges with a perpetual "Waiting for status to be reported" state. And the "For pull requests only" bypass mode threads the needle between self-governance and emergency flexibility: every override requires a PR and leaves a trail in Rule Insights, but nothing prevents a hotfix from reaching production when CI infrastructure fails.

When the project gains contributors, three settings need updating: increase required approvals from 0 to 1, enable "Require approval of the most recent reviewable push" to prevent self-approval of late commits, and add a `CODEOWNERS` file with "Require review from Code Owners" enabled. Everything else in this configuration scales without modification.