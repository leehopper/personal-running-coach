# T06: Restore Backend Coverage — Proof Summary

| # | Type | Description | Status |
|---|------|-------------|--------|
| 1 | cli  | coverlet.msbuild generates Cobertura coverage (88.12% line) | PASS |

## Changes
- Replaced coverlet.collector with coverlet.msbuild in Directory.Packages.props and .csproj
- CI test step uses /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=../../TestResults/
- Codecov upload step restored with backend/TestResults directory
