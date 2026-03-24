# Source: docs/specs/06-spec-poc1-productionize/06-spec-poc1-productionize.md
# Pattern: CLI/Process + State
# Recommended test type: Integration

Feature: Remove POC experiment artifacts

  Scenario: Experiment prompt YAML files are deleted
    Given the following files exist under "backend/src/RunCoach.Api/Prompts/":
      | file                                  |
      | context-injection-8k.yaml             |
      | context-injection-12k.yaml            |
      | context-injection-profile-end.yaml    |
      | context-injection-profile-middle.yaml |
    When the developer deletes all four experiment prompt YAML files
    Then listing "backend/src/RunCoach.Api/Prompts/" does not include any of those four filenames

  Scenario: Production prompt YAML files remain present and unchanged
    Given the experiment prompt YAML files have been deleted
    When the developer lists files in "backend/src/RunCoach.Api/Prompts/"
    Then exactly these 5 files are present:
      | file                        |
      | coaching-system.v1.yaml     |
      | coaching-system.v2.yaml     |
      | coaching-v1.yaml            |
      | coaching-v2.yaml            |
      | context-injection-v1.yaml   |

  Scenario: POC spec directories 01 through 05 are deleted
    Given the "docs/specs/" directory contains spec directories 01 through 05 with POC validation artifacts
    When the developer deletes spec directories 01 through 05 from "docs/specs/"
    Then listing "docs/specs/" does not include directories matching "01-spec-*" through "05-spec-*"
    And the spec directory "06-spec-poc1-productionize" is still present if it exists

  Scenario: Build succeeds after prompt file removals
    Given experiment prompt YAML files and POC spec directories have been deleted
    When the developer runs "dotnet build backend/RunCoach.slnx"
    Then the build exits with code 0
    And the build output contains "0 Warning(s)"
