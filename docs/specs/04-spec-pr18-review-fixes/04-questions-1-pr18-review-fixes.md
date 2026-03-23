# 04 — PR #18 Review Fixes — Clarifying Questions (Round 1)

## Context

9 review findings from PR #18 code review. User wants to include deferred items where they're easy-moderate wins.

## Questions

1. **Dual-constructor scope (#8):** The ContextAssembler parameterless constructor exists for backward compatibility with experiment infrastructure. Removing it would require updating ExperimentContextAssembler and related experiment code. Include this refactor or defer to a future cleanup?

2. **actions/checkout@v6 verification:** The review flagged setup-node@v6, but the CI file also uses actions/checkout@v6 throughout. Should we verify/fix checkout versions too, or only setup-node?

3. **YamlPromptStore race fix approach (#2):** Two options: (a) use `GetOrAdd` with a `Lazy<Task<T>>` for true single-flight, or (b) keep the current pattern since `TryAdd` + identical record content means the race is functionally harmless. Which approach?
