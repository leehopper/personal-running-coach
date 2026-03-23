# Source: docs/specs/05-spec-pr18-review-round2/05-spec-pr18-review-round2.md
# Pattern: CLI/Process
# Recommended test type: Integration

Feature: Code Quality Conventions

  Scenario: WriteEvalResult uses async file I/O
    Given the EvalTestBase class contains a method for writing eval results
    When a test invokes WriteEvalResultAsync to persist an eval result to disk
    Then the result file is written asynchronously using File.WriteAllTextAsync
    And the method returns a Task that completes without error
    And the written file contains the expected eval result content

  Scenario: Pace tolerance uses a named constant instead of a magic number
    Given the PlanConstraintEvaluator validates pace values against a tolerance threshold
    When the evaluator checks a pace value that is within the allowed tolerance
    Then the validation passes using the named constant PaceTolerancePercent
    And the tolerance value is defined once as a const field, not as an inline literal

  Scenario: SplitMessages documents the text-only content limitation
    Given the AnthropicStructuredOutputClient has a SplitMessages method
    When a developer reads the SplitMessages method implementation
    Then a code comment documents that non-text content parts are dropped during splitting
    And the comment explains why only text content is preserved

  Scenario: Crisis hotline assertions use word-boundary matching
    Given the SafetyBoundaryEvalTests validate that responses contain the 988 crisis hotline number
    When a safety eval test checks for the hotline number in a generated response
    Then the assertion uses a word-boundary regex pattern to match "988"
    And the assertion does not produce false positives from strings like "9880" or "1988"

  Scenario: Structured output records document array type choice
    Given the structured output records MacroPlanOutput, PlanPhaseOutput, and SafetyVerdict use T[] arrays
    When a developer reviews these record definitions
    Then each record includes a comment explaining that T[] is intentional for JSON deserialization compatibility
    And the comment notes that ImmutableArray was considered but deferred

  Scenario: Full solution builds and tests pass after all convention changes
    Given all code quality convention changes have been applied across the solution
    When the developer runs "dotnet build" followed by "dotnet test"
    Then the build completes with exit code 0 and zero warnings
    And all tests pass with zero failures across all test categories
