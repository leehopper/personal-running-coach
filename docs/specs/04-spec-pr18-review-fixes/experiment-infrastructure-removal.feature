# Source: docs/specs/04-spec-pr18-review-fixes/04-spec-pr18-review-fixes.md
# Pattern: CLI/Process + State
# Recommended test type: Integration

Feature: Experiment Infrastructure Removal

  Scenario: Experiment source directory is fully removed
    Given the backend source tree at backend/src/
    When the Modules/Coaching/Experiments/ directory and all files within it are deleted
    Then no files exist under any Experiments/ path in backend/src/
    And the dotnet build completes without referencing any experiment types

  Scenario: Experiment test files are fully removed
    Given the backend test tree at backend/tests/
    When the Experiments/ test directory and its 4 test files are deleted
    Then no files exist under any Experiments/ path in backend/tests/
    And the dotnet build completes without missing test references

  Scenario: ContextAssembler parameterless constructor is removed
    Given the ContextAssembler class in the coaching module
    When the parameterless constructor is deleted
    Then ContextAssembler has only the constructor that accepts dependencies
    And the project builds without constructor resolution errors

  Scenario: SystemPromptText hardcoded constant is removed from ContextAssembler
    Given the ContextAssembler class contains a SystemPromptText constant
    When the SystemPromptText constant is deleted
    Then no reference to SystemPromptText remains in backend source code
    And the project builds successfully

  Scenario: Synchronous Assemble method is removed leaving only AssembleAsync
    Given ContextAssembler has both a synchronous Assemble method and an async AssembleAsync method
    When the synchronous Assemble method is removed
    Then AssembleAsync is the sole entry point for context assembly
    And the project builds without any calls to the removed synchronous method

  Scenario: Eval tests use the async AssembleAsync path after removal
    Given EvalTestBase and PlanGenerationEvalTests previously called the synchronous Assemble method
    When those call sites are updated to use AssembleAsync with an IPromptStore
    Then all eval tests pass when executed via dotnet test
    And no eval test references the removed synchronous Assemble method

  Scenario: Full test suite passes after experiment removal
    Given all experiment files and the deprecated ContextAssembler members have been removed
    When dotnet test is executed against the full solution
    Then the test run completes with 0 failures
    And no build warnings are introduced by the removal
