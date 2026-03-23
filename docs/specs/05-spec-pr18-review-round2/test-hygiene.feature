# Source: docs/specs/05-spec-pr18-review-round2/05-spec-pr18-review-round2.md
# Pattern: CLI/Process
# Recommended test type: Unit

Feature: Test Hygiene

  Scenario: ParseCacheMode accepts injected environment values without mutating process state
    Given the EvalTestBase class has a ParseCacheMode method that accepts an optional string parameter
    When a test calls ParseCacheMode with the value "Replay"
    Then the method returns the Replay cache mode
    And no call to Environment.SetEnvironmentVariable is made during the test

  Scenario: ParseCacheMode falls back to environment variable when no parameter is provided
    Given the process environment variable EVAL_CACHE_MODE is set to "Record"
    When a test calls ParseCacheMode with no arguments
    Then the method returns the Record cache mode
    And backward compatibility with production code paths is maintained

  Scenario: Tautological API key test is removed
    Given the EvalTestBaseCachingTests test class exists
    When the developer lists all test methods in EvalTestBaseCachingTests
    Then no test named "IsApiKeyConfigured_WithKey_ReturnsTrue" exists
    And the remaining tests in the class still pass

  Scenario: YamlPromptStore logs cache hits for already-completed tasks
    Given a YamlPromptStore instance with a prompt that has already been loaded and cached
    When GetPromptAsync is called for the same prompt identifier
    Then the prompt is returned from cache without reloading from disk
    And a cache hit is logged via the LogCacheHit method

  Scenario: Test classes are sealed to follow project conventions
    Given the project convention requires all leaf classes to be sealed
    When the compiler processes EvalTestBaseTests, PlanConstraintEvaluatorTests, and SafetyRubricEvaluatorTests
    Then all three classes compile successfully with the sealed modifier
    And no subclass of these test classes exists in the codebase

  Scenario: EvalTestBase tests pass without environment mutation
    Given all EvalTestBaseTests have been refactored to use parameter injection
    When the developer runs "dotnet test --filter FullyQualifiedName~EvalTestBaseTests"
    Then all tests in the EvalTestBaseTests class pass
    And the test output contains zero failures
