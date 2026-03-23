# Source: docs/specs/04-spec-pr18-review-fixes/04-spec-pr18-review-fixes.md
# Pattern: Async + Error handling
# Recommended test type: Integration

Feature: Code Quality & Consistency Fixes

  Scenario: AnthropicStructuredOutputClient awaits with ConfigureAwait(false)
    Given AnthropicStructuredOutputClient has await calls at lines 55, 58, and 159
    When ConfigureAwait(false) is appended to each await call
    Then the project builds successfully
    And all tests exercising AnthropicStructuredOutputClient pass without deadlocks

  Scenario: YamlPromptStore uses single-flight loading to prevent race conditions
    Given YamlPromptStore.LoadAndCacheAsync can be called concurrently for the same key
    When multiple concurrent requests load the same prompt template simultaneously
    Then only one actual load operation executes for that key
    And all concurrent callers receive the same prompt template instance
    And no duplicate file reads occur for the same cache key

  Scenario: Unused ContextTemplate is documented with a TODO comment
    Given ContextAssembler.AssembleAsync loads a PromptTemplate that includes ContextTemplate
    When the template.ContextTemplate field is accessed but not yet wired into PromptRenderer
    Then a TODO comment is present explaining the future wiring intention
    And the project builds without unused variable warnings

  Scenario: PlanConstraintEvaluator enforces symmetric pace range tolerance
    Given PlanConstraintEvaluator.CheckPaceRanges validates easy pace with upper and lower bounds
    When a plan is evaluated with a fast pace that exceeds the upper tolerance
    Then the evaluator flags the fast pace as out of range
    And the tolerance check for fast pace mirrors the existing easy pace tolerance logic

  Scenario: Eval test output uses xUnit test output instead of Trace.WriteLine
    Given EvalTestBase logs cache mode information during test execution
    When the logging mechanism is changed from Trace.WriteLine to xUnit test output
    Then cache mode log messages appear in the xUnit test output stream
    And no calls to Trace.WriteLine remain in EvalTestBase

  Scenario: Cache key inputs are validated to prevent separator collisions
    Given YamlPromptStore.BuildCacheKey uses "::" as a separator between key components
    When a prompt name or version containing "::" is used to build a cache key
    Then the system either rejects the input or sanitizes it to avoid ambiguity
    And distinct prompt name/version combinations always produce distinct cache keys

  Scenario: Dead ExtractJson method is removed from eval tests
    Given PlanGenerationEvalTests contains an ExtractJson method that is no longer needed
    When the ExtractJson method and its call site in GenerateStructuredAsync are removed
    Then no reference to ExtractJson remains in the eval test files
    And the project builds and all tests pass without the removed method

  Scenario: Full test suite passes after all code quality fixes
    Given all code quality and consistency fixes have been applied
    When dotnet test is executed against the full solution
    Then the test run completes with 0 failures
    And no new build warnings are introduced
