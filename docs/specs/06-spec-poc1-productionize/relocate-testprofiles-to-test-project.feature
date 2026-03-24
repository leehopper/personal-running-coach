# Source: docs/specs/06-spec-poc1-productionize/06-spec-poc1-productionize.md
# Pattern: State
# Recommended test type: Integration

Feature: Relocate TestProfiles to test project

  Scenario: TestProfiles.cs is moved to the test project with updated namespace
    Given the file "backend/src/RunCoach.Api/Modules/Training/Profiles/TestProfiles.cs" exists in production source
    When the developer moves TestProfiles.cs to "backend/tests/RunCoach.Api.Tests/Modules/Training/Profiles/TestProfiles.cs"
    And updates its namespace to "RunCoach.Api.Tests.Modules.Training.Profiles"
    Then the file "backend/tests/RunCoach.Api.Tests/Modules/Training/Profiles/TestProfiles.cs" contains "namespace RunCoach.Api.Tests.Modules.Training.Profiles"
    And the file "backend/src/RunCoach.Api/Modules/Training/Profiles/TestProfiles.cs" no longer exists

  Scenario: TestProfile.cs is moved to the test project with updated namespace
    Given the file "backend/src/RunCoach.Api/Modules/Training/Profiles/TestProfile.cs" exists in production source
    When the developer moves TestProfile.cs to "backend/tests/RunCoach.Api.Tests/Modules/Training/Profiles/TestProfile.cs"
    And updates its namespace to "RunCoach.Api.Tests.Modules.Training.Profiles"
    Then the file "backend/tests/RunCoach.Api.Tests/Modules/Training/Profiles/TestProfile.cs" contains "namespace RunCoach.Api.Tests.Modules.Training.Profiles"
    And the file "backend/src/RunCoach.Api/Modules/Training/Profiles/TestProfile.cs" no longer exists

  Scenario: Consuming test files are updated with new namespace references
    Given TestProfiles.cs and TestProfile.cs have been moved to the test project
    When the developer updates using statements in EvalTestBase.cs, ContextAssemblerTests.cs, EvalTestBaseTests.cs, and TestProfilesTests.cs
    Then each of those four test files contains "using RunCoach.Api.Tests.Modules.Training.Profiles"
    And none of those four test files contains "using RunCoach.Api.Modules.Training.Profiles"

  Scenario: No production source files reference TestProfiles or TestProfile
    Given all file moves and namespace updates are complete
    When the developer searches for "TestProfiles" or "TestProfile" under "backend/src/"
    Then zero matches are returned

  Scenario: Full test suite passes after TestProfiles relocation
    Given all file moves and namespace updates are complete
    When the developer runs "dotnet test backend/RunCoach.slnx"
    Then all tests pass with 0 failures
    And the build produces 0 warnings
