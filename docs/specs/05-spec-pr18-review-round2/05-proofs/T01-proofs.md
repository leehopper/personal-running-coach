# T01 Proof Summary - CI Security and Reliability

## Task
Harden CI pipeline: SHA-pin all GitHub Actions, add eval test filtering, move DB password out of committed config.

## Proof Artifacts

| # | Type | Description | Status |
|---|------|-------------|--------|
| 1 | file | ci.yml contains SHA-pinned actions with version comments | PASS |
| 2 | file | ci.yml dotnet test step contains --filter "Category!=Eval" | PASS |
| 3 | file | appsettings.json contains no password values | PASS |
| 4 | cli  | dotnet build passes with 0 warnings | PASS |

## Changes Made

1. **SHA-pinned all 12 GitHub Actions** in `.github/workflows/ci.yml` to full 40-character commit SHAs with `# vX.Y.Z` version comments:
   - `actions/checkout@v6.0.2`
   - `dorny/paths-filter@v4.0.1`
   - `actions/setup-dotnet@v5.2.0`
   - `actions/setup-node@v4.4.0`
   - `codecov/codecov-action@v5.5.3`
   - `aquasecurity/trivy-action@v0.35.0`

2. **Added eval test exclusion filter** (`--filter "Category!=Eval"`) to the main `dotnet test` step in CI.

3. **Moved PostgreSQL connection string** from `appsettings.json` to `appsettings.Development.json`:
   - `appsettings.json` now contains a placeholder comment explaining the credentials pattern
   - `appsettings.Development.json` contains the local dev connection string (matches docker-compose)

4. **EVAL_CACHE_MODE: Replay** was already set as an environment variable on the backend test step (pre-existing).

## Verification

- `dotnet build backend/RunCoach.slnx` -- 0 warnings, 0 errors
- `dotnet test backend/RunCoach.slnx --filter "Category!=Eval"` -- 292 tests passed
