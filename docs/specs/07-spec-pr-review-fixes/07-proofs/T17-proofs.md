# T17 Proof Summary

## Task
FIX bug-1: Add stop_reason check and FinishReason mapping in AnthropicStructuredOutputClient

## Changes
- **MapToChatResponse**: Added `FinishReason` mapping via new `MapFinishReason` method that converts Anthropic `StopReason` to M.E.AI `ChatFinishReason` (EndTurn/StopSequence -> Stop, MaxTokens -> Length, ToolUse -> ToolCalls).
- **CallNativeStructuredAsync**: Added `StopReason.MaxTokens` guard that throws `InvalidOperationException`, matching the existing pattern in `ClaudeCoachingLlm.GenerateStructuredAsync`.

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | file | T17-01-file.txt | PASS |
| 2 | cli  | T17-02-cli.txt  | PASS |

## Result
All proofs PASS. Build succeeds with 0 errors/warnings. All 315 tests pass.
