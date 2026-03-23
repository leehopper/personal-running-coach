# T01: Upgrade to xUnit v3 — Proof Summary

## Results

| # | Type | Description | Status |
|---|------|-------------|--------|
| 1 | file | Directory.Packages.props contains xunit.v3, no v2 packages | PASS |
| 2 | cli  | `dotnet build` succeeds with 0 errors | PASS |
| 3 | cli  | `dotnet test` — 372 passed, 1 skipped, 0 failed | PASS |

## Migration Notes

- **Packages changed:** `xunit` 2.9.3 + `xunit.runner.visualstudio` 3.1.5 + `Microsoft.NET.Test.Sdk` 18.3.0 replaced by `xunit.v3` 3.2.2
- **OutputType=Exe:** Required by xUnit v3 (self-hosting runner)
- **TestingPlatformDotnetTestSupport=true:** Required for `dotnet test` to use MTP instead of VSTest
- **xUnit1051 suppressed:** 114 CancellationToken analyzer errors suppressed in NoWarn — will be fixed in T04
- **MTP filter syntax:** `dotnet test -- --filter-trait "Category=Eval"` (uses `--` separator for MTP args)
- **MTP0001 warning:** Harmless VSTest property warning from SDK; expected during transition
