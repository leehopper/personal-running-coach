# Source: docs/specs/04-spec-pr18-review-fixes/04-spec-pr18-review-fixes.md
# Pattern: CLI/Process
# Recommended test type: Integration

Feature: CI & Build Fixes

  Scenario: CI workflow references only verified GitHub Action versions
    Given the CI workflow file at .github/workflows/ci.yml
    When the workflow is validated by listing all action references
    Then every "uses:" directive specifies a known stable major version
    And no action reference uses "@v6" for actions/setup-node
    And actions/setup-node is pinned to "@v4"

  Scenario: CI workflow passes dry-run validation
    Given the CI workflow file has been updated with verified action versions
    When the workflow is validated via a dry-run check
    Then the validation completes without errors
    And all job definitions are syntactically valid

  Scenario: All GitHub Action dependencies resolve to existing versions
    Given the CI workflow references actions/checkout, actions/setup-dotnet, actions/setup-node, codecov/codecov-action, aquasecurity/trivy-action, and dorny/paths-filter
    When each action version is checked against the GitHub Actions marketplace
    Then every referenced version exists and is a stable release
    And no action reference points to a non-existent tag
