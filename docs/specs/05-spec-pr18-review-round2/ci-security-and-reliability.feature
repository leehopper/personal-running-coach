# Source: docs/specs/05-spec-pr18-review-round2/05-spec-pr18-review-round2.md
# Pattern: CLI/Process + State
# Recommended test type: Integration

Feature: CI Security and Reliability

  Scenario: SHA-pinned GitHub Actions resist tag mutation attacks
    Given the CI workflow file ci.yml exists
    When the CI pipeline is triggered on a push to the feature branch
    Then every "uses:" directive in the workflow resolves to a full 40-character commit SHA
    And each SHA-pinned line includes a version comment matching the intended release tag

  Scenario: Eval tests are excluded from the main CI test run
    Given the CI workflow is configured with a dotnet test step
    When the main test step executes
    Then the test runner applies a filter that excludes tests in the "Eval" category
    And no eval test attempts a live API call during the CI run

  Scenario: Eval cache operates in replay mode during CI
    Given the CI workflow defines environment variables for the backend test step
    When the backend tests execute in CI
    Then the EVAL_CACHE_MODE environment variable is set to "Replay"
    And eval tests replay from committed fixture files without network requests

  Scenario: PostgreSQL connection string is absent from the root appsettings
    Given the ASP.NET Core application loads configuration from appsettings.json
    When the application starts in a non-Development environment without user-secrets
    Then appsettings.json does not contain any password values for the database connection
    And a placeholder comment in appsettings.json explains that credentials come from environment variables or user-secrets

  Scenario: Development configuration loads the local database connection
    Given appsettings.Development.json contains the local PostgreSQL connection string
    And the ASP.NET Core environment is set to "Development"
    When the application starts
    Then the configuration system loads the connection string from appsettings.Development.json
    And the application connects to the local PostgreSQL instance without error

  Scenario: Project builds cleanly after CI configuration changes
    Given all CI security and configuration changes have been applied
    When the developer runs "dotnet build" against the solution
    Then the build completes with exit code 0
    And zero warnings are reported
