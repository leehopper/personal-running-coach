# T23 Proof Summary

## Task
FIX test-3: Add test for malformed YAML deserialization in YamlPromptStore

## Artifacts
| # | Type | File | Status |
|---|------|------|--------|
| 1 | test | T23-01-test.txt | PASS |
| 2 | cli  | T23-02-cli.txt  | PASS |

## Summary
Added three tests to `YamlPromptStoreTests.cs` covering the untested YAML deserialization
error paths in `YamlPromptStore.LoadAsync`:

1. `GetPromptAsync_MalformedYaml_ThrowsYamlException` -- verifies that syntactically invalid
   YAML (unbalanced brackets, bad indentation) propagates a `YamlDotNet.Core.YamlException`
   to the caller, since `LoadAsync` has no try-catch around `_deserializer.Deserialize`.

2. `GetPromptAsync_WrongStructureYaml_ReturnsEmptyTemplate` -- verifies that valid YAML with
   an unrecognized structure (no `static_system_prompt` or `context_template` keys) returns a
   `PromptTemplate` with empty strings and null metadata, because `IgnoreUnmatchedProperties()`
   silently skips unknown keys and all properties default to null.

3. `GetPromptAsync_MalformedYamlThenFixed_EvictsCacheAndReloads` -- verifies that after a
   `YamlException` faults the `Lazy<Task<>>` cache entry, the `catch` block in `GetPromptAsync`
   evicts the entry, allowing a subsequent call with corrected YAML to succeed.

All 325 tests pass. Build succeeds with 0 warnings.
