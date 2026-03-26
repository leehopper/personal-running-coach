# T10 Proof Summary: Add top-level permissions block to CI workflow

## Task
FIX sec-1: Add top-level `permissions: { contents: read }` block to `.github/workflows/ci.yml`

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | file | T10-01-file.txt | PASS |
| 2 | cli  | T10-02-cli.txt  | PASS |

## Verification Details

1. **T10-01-file.txt** - Git diff confirms exactly 3 lines added (the `permissions:` block with `contents: read`) between the `on:` and `jobs:` blocks. No other lines were modified.

2. **T10-02-cli.txt** - Python YAML parser confirms `permissions` is a top-level key with value `{'contents': 'read'}`. The workflow YAML is syntactically valid.

## Impact Analysis

- The top-level `permissions: { contents: read }` sets a restrictive default for all jobs
- Jobs `changes` and `security` already have explicit job-level permissions that will override the default
- Jobs `backend`, `frontend`, and `gate` now inherit `contents: read` (previously relied on GitHub's default)
- This follows the security best practice of least-privilege permissions for GitHub Actions workflows
