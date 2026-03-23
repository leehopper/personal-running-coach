# Source: docs/specs/05-spec-pr18-review-round2/05-spec-pr18-review-round2.md
# Pattern: CLI/Process + State
# Recommended test type: Integration

Feature: Eval Cache TTL Fix

  Scenario: Cache fixture expiration is extended to prevent CI time bomb
    Given the poc1-eval-cache directory contains entry.json fixture files with near-term expiration dates
    When the developer runs the TTL extension script against the cache directory
    Then every entry.json file in poc1-eval-cache has its expiration set to "9999-12-31T23:59:59Z"
    And all other fields in each entry.json file are preserved unchanged

  Scenario: TTL extension script is idempotent
    Given the TTL extension script has already been run once on the cache directory
    When the developer runs the TTL extension script a second time
    Then every entry.json file still has expiration "9999-12-31T23:59:59Z"
    And no entry.json files are corrupted or duplicated

  Scenario: Binary response data files are marked in gitattributes
    Given the repository has a .gitattributes file
    When git processes files in the poc1-eval-cache directory
    Then files matching "backend/poc1-eval-cache/**/*.data" are treated as binary
    And git diff does not attempt text diffing on .data files

  Scenario: EvalTestBase documents the re-recording workflow
    Given the EvalTestBase class is used as the base for all eval tests
    When a developer opens EvalTestBase to understand the cache workflow
    Then a comment block documents when cache fixtures should be re-recorded
    And the comment explains how to run the recording process
    And the comment explains how to extend the TTL after recording

  Scenario: Extended cache fixtures survive CI replay over time
    Given all entry.json files have expiration set to "9999-12-31T23:59:59Z"
    And the eval tests are configured with EVAL_CACHE_MODE set to "Replay"
    When the eval test suite runs in replay mode
    Then all eval tests pass using the cached fixture data
    And no test fails due to an expired cache entry
