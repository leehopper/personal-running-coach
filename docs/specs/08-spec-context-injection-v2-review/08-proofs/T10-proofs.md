# T10 Proof Summary: Sanitize PromptRenderer token replacement

## Task
Sanitize/escape `{{` and `}}` in user-provided token values to prevent template injection in `PromptRenderer.Render`.

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | test | T10-01-test.txt | PASS |
| 2 | file | T10-02-file.txt | PASS |

## Implementation Summary

Added `SanitizeTokenValue` method to `PromptRenderer` that uses a `while` loop to repeatedly collapse `{{` to `{` and `}}` to `}` until no double-brace sequences remain. This prevents substituted token values from introducing new template token placeholders that would be resolved by subsequent replacements.

The loop-based approach (vs single-pass) handles edge cases like triple braces (`{{{`) which produce new double-brace pairs after one collapse pass.

## Test Coverage

10 new tests covering:
- Template injection prevention (value containing `{{other_token}}`)
- Double brace collapsing in values
- Nested/malicious token patterns
- Empty double braces `{{}}`
- Single braces preserved (no over-sanitization)
- `SanitizeTokenValue` unit tests: empty, plain, multiple pairs, triple braces

## Files Modified
- `backend/src/RunCoach.Api/Modules/Coaching/Prompts/PromptRenderer.cs` (sanitization logic)
- `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Prompts/PromptRendererTests.cs` (10 new tests)
- `backend/src/RunCoach.Api/Modules/Coaching/ContextAssembler.cs` (comment update only)
