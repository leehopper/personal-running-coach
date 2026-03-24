# Source: docs/specs/06-spec-poc1-productionize/06-spec-poc1-productionize.md
# Pattern: CLI/Process + State
# Recommended test type: Integration

Feature: Tag and remove POC console app

  Scenario: Git tag poc1-complete is created on current HEAD before deletions
    Given the feature/poc1-context-injection-v2 branch with all POC code intact
    When the developer creates the git tag "poc1-complete" on the current HEAD
    Then running "git tag -l poc1-complete" outputs "poc1-complete"
    And the tagged commit contains the RunCoach.Poc1.Console directory

  Scenario: Console app project directory is deleted
    Given the git tag "poc1-complete" has been created
    When the developer deletes the "backend/src/RunCoach.Poc1.Console/" directory
    Then listing "backend/src/RunCoach.Poc1.Console/" returns "No such file or directory"
    And the files Program.cs, AssemblyMarker.cs, RunCoach.Poc1.Console.csproj, and appsettings.json no longer exist on disk

  Scenario: Console app project entry is removed from solution file
    Given the RunCoach.Poc1.Console directory has been deleted
    When the developer removes the RunCoach.Poc1.Console project entry from "backend/RunCoach.slnx"
    Then the contents of "backend/RunCoach.slnx" contain no reference to "RunCoach.Poc1.Console"

  Scenario: InternalsVisibleTo attribute for console app is removed
    Given the RunCoach.Poc1.Console project has been removed from the solution
    When the developer removes the InternalsVisibleTo Include for RunCoach.Poc1.Console from "backend/src/RunCoach.Api/RunCoach.Api.csproj"
    Then the contents of "backend/src/RunCoach.Api/RunCoach.Api.csproj" contain no reference to "RunCoach.Poc1.Console"

  Scenario: Solution builds with zero errors and zero warnings after console app removal
    Given the RunCoach.Poc1.Console project, solution entry, and InternalsVisibleTo reference have all been removed
    When the developer runs "dotnet build backend/RunCoach.slnx"
    Then the build exits with code 0
    And the build output contains "0 Warning(s)"
    And the build output contains "0 Error(s)"
