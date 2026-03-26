# T12 Proof Summary: Document nested type exception to one-type-per-file rule

## Task
FIX conv-2: Document that internal nested serialization/deserialization types are an accepted exception to the one-type-per-file coding standard.

## Change
Updated `backend/CLAUDE.md` line 47 to add an explicit exception clause to the "One type per file" rule, covering `internal` nested types used solely as serialization/deserialization models (e.g., `YamlPromptStore.YamlPromptDocument` and `YamlPromptStore.YamlPromptMetadata`).

## Proof Artifacts

| # | Type | File | Status |
|---|------|------|--------|
| 1 | file | T12-01-file.txt | PASS |
| 2 | cli  | T12-02-cli.txt  | PASS |

## Verification
- Nested types confirmed at YamlPromptStore.cs lines 218 and 230
- Exception documented in backend CLAUDE.md with scoped criteria and concrete example
- Build passes with zero warnings
