# FIX-REVIEW: Wire ReplayGuardChatClient — Proof Summary

| # | Type | Description | Status |
|---|------|-------------|--------|
| 1 | test | 391 tests pass in Replay mode with guard wired | PASS |

## Changes
1. `ReplayGuardChatClient` converted from bare `IChatClient` to `DelegatingChatClient` — passes through `GetService` for metadata, intercepts `GetResponseAsync` to throw
2. `CreateReplayConfig` now wraps `AnthropicStructuredOutputClient` with `ReplayGuardChatClient` before passing to `DiskBasedReportingConfiguration`
3. Constructor fail-fast: `EVAL_CACHE_MODE=Record` without API key throws `InvalidOperationException`
4. Updated unit test for new constructor signature, added Record-without-key test
