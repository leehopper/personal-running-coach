# Source: docs/specs/06-spec-poc1-productionize/06-spec-poc1-productionize.md
# Pattern: CLI/Process + State
# Recommended test type: Integration

Feature: Final verification and ROADMAP update

  Scenario: Full test suite passes with zero failures and zero warnings
    Given all cleanup changes from Units 1-3 have been applied
    When the developer runs "dotnet test backend/RunCoach.slnx"
    Then all tests pass with 0 failures
    And the test output contains "0 Warning(s)"

  Scenario: No references to RunCoach.Poc1.Console remain in the repository
    Given all cleanup changes from Units 1-3 have been applied
    When the developer searches the entire "backend/" directory for "RunCoach.Poc1.Console"
    Then zero matches are returned

  Scenario: No TestProfiles or TestProfile references remain under production source
    Given all cleanup changes from Units 1-3 have been applied
    When the developer searches "backend/src/" for "TestProfiles" or "TestProfile"
    Then zero matches are returned

  Scenario: ROADMAP.md reflects productionized and merged status
    Given the full test suite passes and all orphaned references are confirmed removed
    When the developer updates "ROADMAP.md" to reflect POC 1 productionized status
    Then "ROADMAP.md" no longer contains text indicating POC 1 is "ready for review" or "in progress"
    And "ROADMAP.md" contains text indicating POC 1 has been productionized or merged

  Scenario: README.md no longer contains POC-specific run instructions
    Given ROADMAP.md has been updated
    When the developer reviews and updates "README.md"
    Then "README.md" does not contain console app usage instructions
    And "README.md" does not reference "RunCoach.Poc1.Console"

  Scenario: All cleanup changes are committed with conventional commit format
    Given ROADMAP.md and README.md have been updated
    When the developer creates a commit with all cleanup changes
    Then the most recent commit message starts with "refactor:" or "chore:"
    And "git status" reports a clean working tree
