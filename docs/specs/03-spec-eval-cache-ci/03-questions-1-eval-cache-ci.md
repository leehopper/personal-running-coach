# 03-questions-1-eval-cache-ci

## Round 1: Design Decisions

### Q1: Replay mode cache miss behavior
**Answer:** No-op inner client that throws with descriptive message including scenario name. Pattern: `"Cache miss for scenario '{scenarioName}'. Run eval tests locally with EVAL_CACHE_MODE=Record and a valid API key to regenerate the cache, then commit the updated cache files."`

### Q2: GenerateExperimentResults.cs disposition
**Answer:** Delete it. Permanently-skipped utility whose outputs are gitignored.

### Q3: CancellationToken pattern
**Answer:** xUnit `TestContext.Current.CancellationToken` (v3). Requires xUnit v3 upgrade (added as Unit 1).

### Q4: SplitMessages multi-system-message fix
**Answer:** Concatenate with `\n\n` (double newline). Keeps sections visually distinct in Anthropic's system prompt, prevents trailing sentences from running into headings.

### Q5: xUnit version
**Answer:** Upgrade from xUnit v2 (2.9.3) to xUnit v3 as part of this spec (added as Unit 1 prerequisite).
