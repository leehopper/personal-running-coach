# CI Unblock Procedures

Operational reference for the `main-protection` repository ruleset and the
CI checks it requires. See DEC-043 in `docs/decisions/decision-log.md` for
the full design rationale.

## Required checks

| Check name | Wired by | Tool |
| --- | --- | --- |
| `CI Gate` | `ci.yml` (original scaffolding) | GitHub Actions composite gate |
| `Backend analysis` | Unit 5 (T05.1) | dotnet-sonarscanner |
| `Frontend analysis` | Unit 5 (T05.1) | SonarSource/sonarqube-scan-action v7.1 |
| `License & dependency review` | Unit 6 (T06.1) | actions/dependency-review-action v4.9 |

**Deferred until first successful run on main (add via Settings → Rules → Rulesets):**

| Check name | Wired by | Tool |
| --- | --- | --- |
| `Analyze (csharp)` | Unit 4 (T04.1) | github/codeql-action v4.35.1 |
| `Analyze (javascript-typescript)` | Unit 4 (T04.1) | github/codeql-action v4.35.1 |

Note: GitHub Actions check names are `{job name}` not `{workflow name} / {job name}` for these workflows. Verify exact names in the Actions tab after the first successful run before adding to the ruleset.

The SonarQube Cloud GitHub App also posts separate checks named
`[runcoach-backend] SonarCloud Code Analysis` and `[runcoach-frontend] SonarCloud Code Analysis`.
These are the quality gate verdicts (coverage, duplication, security hotspots) from the same
scan. They are advisory and not in the required-checks list.

## Re-recording eval cache fixtures

If a CodeRabbit or CodeQL suggestion requires editing a coaching prompt YAML
file (`backend/src/RunCoach.Api/Prompts/*.yaml`), the change will cascade
through eval cache keys and break `EVAL_CACHE_MODE: Replay` in CI.

```bash
# Ensure API key is configured
dotnet user-secrets list --project backend/src/RunCoach.Api | grep Anthropic

# Full re-record (calls Anthropic API, extends TTL, verifies Replay)
./backend/tests/scripts/rerecord-eval-cache.sh

# Commit the updated cache
git add backend/tests/eval-cache/
git commit -m "chore: re-record eval cache fixtures"
```

## CodeQL false-positive suppression

Suppress via the query config (`.github/codeql/codeql-config.yml`), not
inline comments. Add the query ID to the `query-filters` exclusion list:

```yaml
query-filters:
  - exclude:
      id: cs/hardcoded-credentials
  - exclude:
      id: cs/the-false-positive-query-id  # reason for suppression
```

If the false positive is file-scoped (e.g., a constant file that triggers
`cs/hardcoded-credentials`), prefer a `paths-ignore` entry in the config
over a query-level suppression.

## SonarQube Cloud quality-gate override

If SonarQube Cloud fails on a metric you've reviewed and accepted:

1. Open the SonarQube Cloud dashboard for the failing project.
2. Navigate to the failing quality gate condition.
3. Use "Mark as reviewed" or "Won't fix" with a justification comment.
4. If the quality gate itself needs adjustment, update it in the SonarQube
   Cloud project settings (not in committed files).

For persistent false positives, add the file pattern to
`sonar.exclusions` in the relevant `sonar-project.properties` file (or
the `/d:` flags in `sonarqube.yml` for the backend).

## Emergency bypass

The `main-protection` ruleset has **Repository admins** in the bypass list
with "For pull requests only" mode. This means the maintainer must still
open a PR but can bypass failing required checks to merge in an emergency.

**Procedure:**

1. Open a PR as normal.
2. On the merge screen, the "Merge without waiting for requirements to be
   met" option appears for admin bypass actors.
3. Merge with the bypass.
4. **Within 24 hours**, file a GitHub issue documenting:
   - Which check(s) were bypassed
   - Why the bypass was necessary
   - What follow-up is needed to restore the check to green
5. Link the issue in the PR description for audit trail.

GitHub's ruleset audit log records the bypass event automatically. The
24-hour issue requirement is an additional human-readable audit layer.

## Full ruleset rollback

If the tooling produces a sustained false-positive storm that cannot be
resolved quickly:

1. Navigate to **Settings → Rules → Rulesets → main-protection**.
2. Set enforcement status to **Disabled**.
3. File a GitHub issue documenting the rollback and the plan to re-enable.
4. Fix the underlying issue (update query config, adjust quality gate,
   update action versions).
5. Re-enable the ruleset and verify on a test PR.

## SONAR_TOKEN revocation

If the `SONAR_TOKEN` secret is compromised:

1. Revoke the token in SonarQube Cloud project settings.
2. Generate a new token.
3. Update the repo secret at **Settings → Secrets and variables → Actions →
   Repository secrets → SONAR_TOKEN**.
4. Re-run the SonarQube Cloud workflow to verify.
