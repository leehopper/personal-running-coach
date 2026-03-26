# T04 Proof Summary: Conversation Truncation at >10 Turns

## Task
Add test verifying that conversation history with more than 10 turns is truncated to the last 10.

## Issue
Existing tests used 0, 2, or 10 conversation turns. Since the truncation condition in
`ContextAssembler.BuildEndSections` is `> 10` (strictly greater than), exactly 10 turns
never triggered the truncation path. No test exercised the `[^MaxConversationTurns..]`
range slice.

## Fix
Added `AssembleAsync_FifteenConversationTurns_TruncatesToLastTen` test that:
1. Creates input with 15 conversation turns (exceeds `MaxConversationTurns = 10`)
2. Verifies turns 1-5 (the oldest) are NOT present in the conversation section
3. Verifies turns 6-15 (the most recent 10) ARE present in the conversation section
4. Counts `[User]:` markers to confirm exactly 10 turns remain

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | test | T04-01-test.txt | PASS |
| 2 | cli  | T04-02-cli.txt  | PASS |

## Result
PASS - All proof artifacts verified successfully.
