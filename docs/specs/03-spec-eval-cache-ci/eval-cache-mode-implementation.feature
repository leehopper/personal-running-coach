# Source: docs/specs/03-spec-eval-cache-ci/03-spec-eval-cache-ci.md
# Pattern: CLI/Process + Error Handling
# Recommended test type: Integration

Feature: EVAL_CACHE_MODE Implementation

  Scenario: EVAL_CACHE_MODE defaults to Auto when unset
    Given the EVAL_CACHE_MODE environment variable is not set
    When the eval test infrastructure initializes
    Then the active cache mode is Auto
    And test output indicates the mode is "Auto"

  Scenario: EVAL_CACHE_MODE accepts values case-insensitively
    Given the EVAL_CACHE_MODE environment variable is set to "replay"
    When the eval test infrastructure initializes
    Then the active cache mode is Replay
    And test output indicates the mode is "Replay"

  Scenario: Replay mode uses no-op client that throws on cache miss
    Given EVAL_CACHE_MODE is set to "Replay"
    And no cache file exists for scenario "UncachedScenario"
    When the eval test for "UncachedScenario" calls GetResponseAsync
    Then the call throws an exception with the message "Cache miss for scenario 'UncachedScenario'. Run eval tests locally with EVAL_CACHE_MODE=Record and a valid API key to regenerate the cache, then commit the updated cache files."

  Scenario: Replay mode serves cached responses without API calls
    Given EVAL_CACHE_MODE is set to "Replay"
    And valid cache files exist for all 17 eval scenarios
    When the developer runs "dotnet test --filter Category=Eval"
    Then all 17 tests pass with exit code 0
    And zero outbound API calls are made to Anthropic

  Scenario: Record mode requires a valid API key
    Given EVAL_CACHE_MODE is set to "Record"
    And a valid Anthropic API key is configured
    When the eval test calls GetResponseAsync
    Then the call reaches the real Anthropic API
    And the response is cached for future Replay use

  Scenario: Auto mode falls back to Replay when no API key is available
    Given EVAL_CACHE_MODE is set to "Auto"
    And no Anthropic API key is configured
    When the eval test infrastructure initializes
    Then the system behaves as Replay mode
    And test output indicates the mode is "Auto (Replay fallback - no API key)"

  Scenario: Auto mode behaves as Record when API key is available
    Given EVAL_CACHE_MODE is set to "Auto"
    And a valid Anthropic API key is configured
    When the eval test infrastructure initializes
    Then the system behaves as Record mode
    And test output indicates the mode is "Auto (Record - API key available)"

  Scenario: Current cache mode is displayed in test output
    Given EVAL_CACHE_MODE is set to "Replay"
    When the developer runs "dotnet test --filter Category=Eval"
    Then the test output contains the active cache mode name
    And the mode is visible in the test results log
