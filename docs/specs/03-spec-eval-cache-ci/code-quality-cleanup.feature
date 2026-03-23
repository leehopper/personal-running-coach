# Source: docs/specs/03-spec-eval-cache-ci/03-spec-eval-cache-ci.md
# Pattern: CLI/Process + Error Handling
# Recommended test type: Integration

Feature: Code Quality Cleanup

  Scenario: SplitMessages concatenates multiple system messages
    Given a chat request contains three system messages with content "A", "B", and "C"
    When SplitMessages processes the request
    Then the resulting single system message contains "A", "B", and "C" separated by double newlines
    And no system message content is dropped

  Scenario: ConvertSchema throws on null deserialization instead of using null-forgiving operator
    Given a JSON schema element that deserializes to null
    When ConvertSchema processes the element
    Then an InvalidOperationException is thrown
    And the exception message is "Schema deserialization returned null for the provided JSON schema element."

  Scenario: Eval test helper methods pass CancellationToken through to API calls
    Given an eval test is running with a CancellationToken from TestContext.Current
    When the test exceeds its timeout and the token is cancelled
    Then the GetResponseAsync call is cancelled promptly
    And the test does not hang waiting for an API response

  Scenario: GenerateExperimentResults dead code is removed
    Given the file GenerateExperimentResults.cs previously existed as a skipped test
    When the developer runs "dotnet build" on the test project
    Then the build succeeds with zero warnings
    And no reference to GenerateExperimentResults exists in the compiled assembly

  Scenario: All 17 eval tests pass after cleanup changes
    Given the SplitMessages, ConvertSchema, and CancellationToken fixes are applied
    And GenerateExperimentResults.cs has been deleted
    When the developer runs "dotnet test --filter Category=Eval"
    Then all 17 eval tests pass with exit code 0
    And no compiler warnings are emitted
