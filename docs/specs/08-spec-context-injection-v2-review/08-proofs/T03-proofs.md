# T03 Proof Summary

## Task
TEST: Add prompt store failure propagation tests for AssembleAsync

## What Was Added
Three tests verifying that exceptions thrown by `IPromptStore` propagate through `ContextAssembler.AssembleAsync` without being swallowed:

1. `GetActiveVersion` throwing `KeyNotFoundException` (line 111 of ContextAssembler.cs)
2. `GetPromptAsync` throwing `KeyNotFoundException` (line 112)
3. `GetPromptAsync` throwing `FileNotFoundException` (line 112, models real YamlPromptStore behavior)

Each test constructs a dedicated `ContextAssembler` with a failing mock `IPromptStore`, invokes `AssembleAsync`, and asserts the expected exception type and message propagate.

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | test | T03-01-test.txt | PASS |
| 2 | cli  | T03-02-cli.txt  | PASS |

## Files Modified
- `backend/tests/RunCoach.Api.Tests/Modules/Coaching/ContextAssemblerTests.cs` (3 new tests)
